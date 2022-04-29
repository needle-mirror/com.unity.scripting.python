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
    public string interpreter {get;}
    public string pythonPath {get;}

    public PyTestVenv()
    {
        using (Py.GIL())
        {
        // Create a temporary Python virtual environment by spawning a subprocess

        path = Path.Combine(Path.GetTempPath(), "py_venv_test");

        dynamic spawn_process = Py.Import("unity_python.common.spawn_process");
        dynamic shlex = Py.Import("shlex");

        var argsStr = new PyString($"-m venv \"{path}\"");
        var args = shlex.split(argsStr);
        dynamic proc = spawn_process.spawn_process_in_environment(
                PythonRunner.PythonInterpreter,
                args,
                wantLogging: true);
        proc.communicate(); // wait for process to be over
#if UNITY_EDITOR_WIN
        pythonPath = Path.Combine(path, "Lib", "site-packages");
        interpreter = Path.Combine(path, "Scripts", "python.exe");
#else
        pythonPath = Path.Combine(path, "lib", "site-packages", "python3.9", "site-packages");
        interpreter = Path.Combine(path, "bin", "python3");
#endif
        // Install pip-tools into the py venv
        // FIXME: we need to use `--use-deprecated=legacy-resolver` otherwise we get a error about non-conform
        // html headers
        argsStr = new PyString("-m pip install --use-deprecated=legacy-resolver pip-tools");
        args = shlex.split(argsStr);
        proc = spawn_process.spawn_process_in_environment(interpreter, 
                args,
                wantLogging: false
        );
        proc.communicate();
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

