using UnityEngine;
using Python.Runtime;
using System.IO;
using System.Diagnostics;

using UnityEditor.Scripting.Python;

namespace UnityEditor.Scripting.Python.Tests
{
    /// <summary>
    ///   Create a temp python virtual env
    /// </summary>
    public class PyTestVenv : System.IDisposable
    {
    public string path {get;}

    public PyTestVenv()
    {
        using (Py.GIL())
        {
        // Create a temporary Python virtual environment by spawning a subprocess

        path = Path.Combine(Path.GetTempPath(), "py_venv_test");

        dynamic spawn_process = PythonEngine.ImportModule("unity_python.common.spawn_process");
        dynamic shlex = PythonEngine.ImportModule("shlex");

        var argsStr = new PyString($"-m venv --copies \"{path}\"");
        var args = shlex.split(argsStr);
        dynamic proc = spawn_process.spawn_process_in_environment(
                PythonRunner.PythonInterpreter,
                args,
                wantLogging: true);
        proc.communicate(); // wait for process to be over

#if UNITY_EDITOR_OSX
        // On macOS, also need to copy the content of the lib folder,
        // where are stored python3 and openssl dynamic libraries.
        using (Process copyProcess = new Process())
        {
            copyProcess.StartInfo.UseShellExecute = false;
            copyProcess.StartInfo.FileName = @"/bin/zsh";

            var sourceLibPath = Path.GetFullPath(Path.Combine(PythonSettings.kDefaultPythonDirectory, "lib"));
            var venvLibPath = path;

            copyProcess.StartInfo.Arguments = $"-c 'cp -r \"{sourceLibPath}\" \"{venvLibPath}\"'";
            copyProcess.Start();
            copyProcess.WaitForExit();
        }
        // And we also need to uninstall copied pip packages, to have a fresh new environment.
        using (Process uninstallProcess = new Process())
        {
            uninstallProcess.StartInfo.UseShellExecute = false;
            uninstallProcess.StartInfo.FileName = @"/bin/zsh";

            var venvPyBin = Path.Combine(path, "bin", "python3");
            uninstallProcess.StartInfo.Arguments = $"-c 'for pak in `{venvPyBin} -m pip freeze`; do {venvPyBin} -m pip uninstall -y $pak; done'";
            uninstallProcess.Start();
            uninstallProcess.WaitForExit();
        }
#endif
        }
    }

    public void Dispose()
    {
        // remove temp python virtual env folder from filesystem
        try {
        System.IO.Directory.Delete(path, true);
        }
        catch (System.Exception exc)
        {
        UnityEngine.Debug.Log($"Deletion of the temporary Python virtual environment at {path} failed. Reason: {exc.Message}");
        }

    }
    }
}

