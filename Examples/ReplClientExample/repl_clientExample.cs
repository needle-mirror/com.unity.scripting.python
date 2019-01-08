#if UNITY_EDITOR_WIN || UNITY_EDITOR_LINUX

using System.Diagnostics;
using UnityEditor;
using UnityEditor.Scripting.Python;
using UnityEngine;
using Python.Runtime;

namespace PythonExample
{
    public class ReplClientExample
    {
        [MenuItem("Python/Examples/Repl Client Example")]
        public static void OnMenuClick()
        {
            string replClientFile = "Packages/com.unity.scripting.python/Examples/ReplClientExample/repl_clientExample.py";
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