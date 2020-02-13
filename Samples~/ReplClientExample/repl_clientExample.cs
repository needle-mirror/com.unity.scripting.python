#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX

using System.IO;
using UnityEditor;
using UnityEditor.Scripting.Python;
using UnityEngine;
using Python.Runtime;

namespace PythonExample
{
    public class ReplClientExample
    {
        /// <summary>
        /// Hack to get the current file's directory
        /// </summary>
        /// <param name="fileName">Leave it blank to the current file's directory</param>
        /// <returns></returns>
        private static string __DIR__([System.Runtime.CompilerServices.CallerFilePath] string fileName = "")
        {
            return Path.GetDirectoryName(fileName);
        }

        [MenuItem("Python/Examples/Repl Client Example")]
        public static void OnMenuClick()
        {
            string replClientFile = $"{__DIR__()}/repl_clientExample.py";
#if UNITY_EDITOR_WIN
            PythonRunner.SpawnClient(
                    file: replClientFile, 
                    wantLogging: true);
#elif UNITY_EDITOR_LINUX
            // PythonRunner.SpawnClient starts the server. So we should too
            PythonRunner.StartServer();
            dynamic unity_server = PythonEngine.ImportModule("unity_python.server.server");
            string[] args = {"-e", "python", replClientFile};
            dynamic builtins = PythonEngine.ImportModule("__builtin__");
            dynamic pyArgs = builtins.list();
            foreach(var arg in args)
            {
                if (arg is string)
                {
                    pyArgs.append(new PyString((string)arg));
                }
                else
                {
                    pyArgs.append(arg);
                }
            }
            dynamic process = unity_server.spawn_subpython(pyArgs,
                    python_executable: "xterm", //subvert!
                    wantLogging: true);
#endif
        }
    }
}

#endif