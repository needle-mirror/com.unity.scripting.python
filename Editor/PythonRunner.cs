using UnityEditor;
using UnityEngine;
using Python.Runtime;
using System;
using System.IO;
using System.Collections.Generic;
using System.Collections;
using System.Threading.Tasks;
using System.Threading;

namespace UnityEditor.Scripting.Python
{
    /// <summary>
    /// Exception thrown when Python is installed incorrectly so we can't
    /// run.
    /// </summary>
    public class PythonInstallException : System.Exception
    {
        /// <summary>
        /// Constructor with message
        /// </summary>
        /// <param name="msg">The message of the exception</param>
        public PythonInstallException(string msg) : base(msg) { }

        /// <summary>
        /// Constructor with message and the exception that triggered this exception
        /// </summary>
        /// <param name="msg">The message of the exception</param>
        /// <param name="innerException">The exception that triggered this exception</param>
        public PythonInstallException(string msg, Exception innerException) : base(msg, innerException) { }

        /// <summary>
        /// The exception's string
        /// </summary>
        public override string Message => $"Python for Unity: {base.Message}\nPlease check the Python for Unity package documentation for the install troubleshooting instructions.";
    }

    /// <summary>
    /// This class encapsulates methods to run Python strings, files and
    /// clients inside of Unity.
    /// </summary>
    public static class PythonRunner
    {
        /// <summary>
        /// The Python version we require.
        ///
        /// Changing this to 3 isn't going to magically make it work, the constant is just to help find some parts that matter.
        /// </summary>
        public const string PythonRequiredVersion = "2.7";

        /// <summary>
        /// The version of the in-process Python interpreter.
        /// </summary>
        /// <value>A string representing the version.</value>
        public static string InProcessPythonVersion
        {
            get
            {
                EnsureInProcessInitialized();
                using (Py.GIL())
                {
                    dynamic sys = PythonEngine.ImportModule("sys");
                    return sys.version.ToString();
                }
            }
        }

        /// <summary>
        /// Runs Python code in the Unity process.
        /// </summary>
        /// <param name="pythonCodeToExecute">The code to execute.</param>
        public static void RunString(string pythonCodeToExecute)
        {
            EnsureInProcessInitialized();
            using (Py.GIL ())
            {
                PythonEngine.Exec(pythonCodeToExecute);
            }
        }

        /// <summary>
        /// Runs a Python script in the Unity process.
        /// </summary>
        /// <param name="pythonFileToExecute">The script to execute.</param>
        public static void RunFile(string pythonFileToExecute)
        {
            EnsureInProcessInitialized();
            if (null == pythonFileToExecute)
            {
                throw new System.ArgumentNullException("pythonFileToExecute", "Invalid (null) file path");
            }

            // Ensure we are getting the full path.
            pythonFileToExecute = Path.GetFullPath(pythonFileToExecute);

            // Forward slashes please
            pythonFileToExecute = pythonFileToExecute.Replace("\\","/");
            if (!File.Exists (pythonFileToExecute))
            {
                throw new System.IO.FileNotFoundException("No Python file found at " + pythonFileToExecute, pythonFileToExecute);
            }

            using (Py.GIL ())
            {
                PythonEngine.Exec(string.Format("execfile('{0}')", pythonFileToExecute));
            }
        }

        /// <summary>
        /// When Unity starts up the domain relaods multiple times, but only
        /// after the "last one" when Unity becomes ready that EditorApplication.update
        /// is called. Use this to know we're good to go.
        /// </summary>
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            EditorApplication.delayCall += DoInitialization;
        }

        static void DoInitialization()
        {
            // Do once, then remove self.
            StartServer();
#if UNITY_2019_1_OR_NEWER
            // Add stream redirection for the console
            RunFile("Packages/com.unity.scripting.python/Python/site-packages/redirecting_stdout.py");
#endif
        }

        /// <summary>
        /// Starts the Python server and the job-processing loop. Calling this 
        /// is idempotent: if the server has already started, this call has no 
        /// effect.
        /// </summary>
        public static void StartServer()
        {
            EnsureOutOfProcessInitialized();
            using (Py.GIL())
            {
                dynamic server_module = PythonEngine.ImportModule("unity_python.server.server");
                if (server_module.start_server())
                {
                    EditorApplication.update += OnUpdate;
                    EditorApplication.quitting += OnQuit;
                    PythonEngine.AddShutdownHandler(OnReload);
                }
            }
        }

        /// <summary>
        /// Stops the Python server and the job processing loop. Calling this is
        /// idempotent: if the server is already closed, this call has no effect.
        /// </summary>
        /// <param name="inviteReconnect">If true, signal the clients the server 
        /// will be restarted.</param>
        [PyGIL]
        public static void StopServer(bool inviteReconnect)
        {
            // If we haven't initialized, there's nothing to stop.
            if (!s_IsOutOfProcessInitialized)
            {
                return;
            }

            using (Py.GIL())
            {
                dynamic server_module = PythonEngine.ImportModule("unity_python.server.server");
                if (server_module.close_server(inviteReconnect))
                {
                    EditorApplication.update -= OnUpdate;
                    EditorApplication.quitting -= OnQuit;
                    PythonEngine.RemoveShutdownHandler(OnReload);
                }
            }
        }

        /// <summary>
        /// Reinitialize the server e.g. after a crash.
        ///
        /// This is *not* idempotent.
	///
	/// It will close the server, delete the socket file, then start the
	/// server again.
        ///
        /// Does not throw exceptions (but logs them).
        /// </summary>
        public static void ForceRestart()
        {
            try
            {
                StopServer(inviteReconnect: true);

                // Delete the socket file.
                System.IO.File.Delete(GetSocketPath());

                // Restart.
                StartServer();
            }
            catch(Exception e)
            {
                Debug.LogException(e);
            }
        }


        /// <summary>
        /// Return the path to the socket file.
        /// </summary>
        public static string GetSocketPath()
        {
            EnsureInProcessInitialized();
            using (Py.GIL())
            {
                dynamic settings = PythonEngine.ImportModule("unity_python.server.settings");
                dynamic path = settings.unity_server_path;
                return path;
            }
        }

        /// <summary>
        /// Tests if a client is connected to the server.
        /// </summary>
        /// <param name="clientName">The name of the client.</param>
        /// <returns>True if the client is connected, False otherwise.</returns>
        public static bool IsClientConnected(string clientName)
        {
            return NumClientsConnected(clientName) != 0;
        }

        /// <summary>
        /// Returns the number of clients of the same name connected to the 
        /// server.
        /// </summary>
        /// <param name="clientName">The name of the client.</param>
        /// <returns>The number of clients connected.</returns>
        public static int NumClientsConnected(string clientName)
        {
            EnsureInProcessInitialized();
            using (Py.GIL())
            {
                dynamic server_module = PythonEngine.ImportModule("unity_python.server.server");
                return server_module.num_clients_connected(clientName);
            }
        }

        /// <summary>
        /// Returns the total number of clients connected to the server.
        /// </summary>
        /// <returns></returns>
        public static int NumClientsConnected()
        {
            EnsureInProcessInitialized();
            using (Py.GIL())
            {
                dynamic server_module = PythonEngine.ImportModule("unity_python.server.server");
                return server_module.get_total_connected_clients();
            }
        }

        /// <summary>
        /// Returns the names of the connected clients. If there are multiple 
        /// instances of a client, returns only one copy of the name.
        /// </summary>
        /// <returns>An array of string that contains the connected clients.</returns>
        [PyGIL]
        public static string[] GetConnectedClients()
        {
            EnsureInProcessInitialized();
            dynamic server_module = PythonEngine.ImportModule("unity_python.server.server");
            return server_module.get_connected_clients();
        }

        /// <summary>
        /// Returns the version of RPyC currently in use.
        /// </summary>
        /// <returns>A human-readable string representing the version of RPyC.</returns>
        [PyGIL]
        public static string GetRPyCVersion()
        {
            EnsureInProcessInitialized();
            dynamic server_module = PythonEngine.ImportModule("unity_python.server.server");
            string RPyCVersion = server_module.get_rpyc_version();
            return RPyCVersion.Replace(',', '.');
        }

        [PyGIL]
        internal static dynamic InternalCallOnClient(bool async, string clientName, string serviceName, params object[] args)
        {
            EnsureInProcessInitialized();
            dynamic server_module = PythonEngine.ImportModule("unity_python.server.server");
            dynamic callable = async ? server_module.call_service_on_client_async : server_module.call_service_on_client;
            if (args == null || args.Length == 0)
            {
                return callable(clientName, -1, serviceName);
            }

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
            return callable(clientName, -1, serviceName, pyArgs);
        }

        /// <summary>
        /// Convenience function to call a service on a client.
        ///
        /// This is a wrapper around unity_python.server.server.call_service_on_client. If you want keyword
        /// arguments, call the Python directly with:
        ///
        ///            dynamic server_module = PythonEngine.ImportModule("unity_python.server.server");
        ///            return server_module.call_service_on_client(clientName, 0, serviceName, args, kwargs);
        ///
        /// Where args is a tuple or list and kwargs is a dict.
        /// </summary>
        /// <param name="clientName">The name of the client to call the service on.</param>
        /// <param name="serviceName">The name of the service to call.</param>
        /// <param name="args">Arguments to be passed to the service. Must be basic
        ///  types (strings, int, bool) or PyObject.</param>
        /// <returns>Null if the service returns None (or has no explicit return),
        ///  else a PyObject.</returns>
        [PyGIL]
        public static dynamic CallServiceOnClient(string clientName, string serviceName, params object[] args)
        {
            return InternalCallOnClient(async: false, clientName, serviceName, args);
        }

        /// <summary>
        /// Convenience function to call a method on a client, asynchronously.
        /// Call this if the client is expected to call back the server.
        ///
        /// Use as a coroutine in a GameObject:
        /// StartCoroutine(PythonRunner.CallCoroutineServiceOnClient("foo", "bar"))
        /// 
        /// Or iterate over the enumerator in a Unity coroutine:
        ///     var pycall = PythonRunner.CallCoroutineServiceOnClient("foo", "bar");
        ///     while (pycall.MoveNext()) {
        ///       yield return null; /* throws here if the result arrives and is an error */
        ///     }
        ///     /* do something with pycall.Current if we care about the return value */
        ///
        /// This is a wrapper around unity_python.server.server.call_service_on_client_async. If you want keyword
        /// arguments, call the Python directly with:
        ///
        ///            dynamic server_module = PythonEngine.ImportModule("unity_python.server.server");
        ///            return server_module.call_service_on_client_async(clientName, 0, serviceName, args, kwargs);
        ///
        /// Where args is a tuple or list and kwargs is a dict.
        /// </summary>
        /// <param name="clientName">The name of the client to call the service on.</param>
        /// <param name="serviceName">The name of the service to call.</param>
        /// <param name="args">Arguments to be passed to the service. Must be basic
        ///  types (strings, int, bool) or PyObject.</param>
        /// <returns>An IEnumerator.</returns>
        public static IEnumerator CallCoroutineServiceOnClient(string clientName, string serviceName, params object[] args)
        {
            dynamic iterator = InternalCallOnClient(async: true, clientName, serviceName, args);
            bool done = false;
            while(!done)
            {
                using (Py.GIL())
                {
                    done = iterator.ready;
                }
                if (!done)
                {
                    yield return null;
                }
            }

            // Once here, we have a value or an exception.
            PyObject returnValue;
            using(Py.GIL())
            {
                // This will throw if it's an exception.
                returnValue = iterator.value;
            }
            yield return returnValue;
        }

        /// <summary>
        /// Task to be awaited for while the async request is done.
        /// Usually used with the return value of CallCoroutineServiceOnClient, or to use a 
        /// Unity coroutine with the async/await semantics.
        /// </summary>
        /// <param name="iter">The iterator to wait on.</param>
        /// <returns>The awaited task.</returns>
        private static Task<dynamic> AwaitOnIterator(IEnumerator iter, int pollingInterval = 20)
        {
            return Task.Factory.StartNew<dynamic>( () =>
            {
                bool moving = true;
                while(moving)
                {
                    using (Py.GIL())
                    {
                        moving = iter.MoveNext();
                    }
                    Thread.Sleep(pollingInterval);
                }
                return iter.Current;
            });
        }

        /// <summary>
        /// Method to use with C# async/await semantics.
        /// Call this if the client is expected to call back the server.
        /// If the call to this method is not awaited, the execution of this method will
        /// continue on its own.
        /// </summary>
        /// <param name="clientName">name of the client to make the call to.</param>
        /// <param name="serviceName">name of the service to be called.</param>
        /// <param name="args">the arguments to be passed on to the called service.</param>
        /// <returns>The task that is the asynchronous call to the service. Wait for it to finish with Task.wait() or 
        /// discard the return value if none is expected.</returns>
        public static async Task<dynamic> CallAsyncServiceOnClient(string clientName, string serviceName, params object[] args)
        {
            return await AwaitOnIterator(CallCoroutineServiceOnClient(clientName, serviceName, args));
        }

        /// <summary>
        /// OnUpdate callback called back during the Editor Idle events.
        /// Used to process the Python jobs queue.
        /// </summary>
        [PyGIL]
        static void OnUpdate()
        {
            EnsureInProcessInitialized();
            dynamic server_module = PythonEngine.ImportModule("unity_python.server.server");
            server_module.process_jobs();
        }

        /// <summary>
        /// OnQuit callback called back when the Unity editor Quits. Disconnects
        /// all clients with inviteReconnect set to false.
        /// </summary>
        static void OnQuit()
        {
            StopServer(inviteReconnect: false);
        }

        /// <summary>
        /// OnReload callback called back when a domain reload is triggered. 
        /// Disconnects all clients with inviteReconnect set to true.
        /// </summary>
        static void OnReload()
        {
            StopServer(inviteReconnect: true);
        }

        /// <summary>
        /// Spawns a new client by launching a new Python interpreter and having it execute the file.
        ///
        /// Returns immediately after spawning the new Python process. If you need Unity to coordinate
        /// with the client, you will need to wait for the client to connect to the Unity server.
        /// If the client script fails to run, check the logs to see exactly what was executed and
        /// try to run the script by hand in a shell terminal to find the errors.
        ///
        /// The Python interpreter chosen is the one in the Python Settings.
        /// </summary>
        /// <param name="file">The file to be executed.</param>
        /// <param name="wantLogging">If true, turns on debug logging for the Python client startup.
        ///  If false, silences all messages and errors during startup.</param>
        /// <param name="arguments">The arguments to be passed to the script.</param>
        /// <returns>The Popen Python object that is the newly spawned client.</returns>
        public static dynamic SpawnClient(string file, bool wantLogging = true, params string[] arguments)
        {
            StartServer();
            var args = new List<string>(arguments);
            args.Insert(0, Path.GetFullPath(file));
            Debug.Log($"[com.unity.scripting.python]: Starting {Path.GetFullPath(file)}");
            using (Py.GIL())
            {
                dynamic unity_server = PythonEngine.ImportModule("unity_python.server.server");
                dynamic process = unity_server.spawn_subpython(args,
                        python_executable: PythonSettings.PythonInterpreter,
                        wantLogging: wantLogging);
                return process;
            }
        }

        /// <summary>
        /// Closes or reset a client by calling the "on_server_shutdown" service.
        /// </summary>
        /// <param name="clientName">The name of the client to close or reset.</param>
        /// <param name="inviteRetry">If true, send on_server_shutdown(true). If false, send on_server_shutdown(false)</param>
        public static void CloseClient(string clientName, bool inviteRetry = false)
        {
            try
            {
                CallServiceOnClient(clientName, "on_server_shutdown", inviteRetry);
            }
            catch (PythonException exc)
            {
                // EOF error is due to the client closing the port before we
                // can read the reply. This is expected
                if (!exc.Message.Contains("EOFError"))
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Waits at most `timeout` seconds for a client to be connected. To be
        /// used as a Unity coroutine.
        /// </summary>
        /// <param name="clientName">The name of the client to wait for.</param>
        /// <param name="timeout">The maximum time to wait on the client, in seconds.</param>
        /// <returns>The IEnumerator to iterate upon. Always yields null.</returns>
        public static IEnumerator WaitForConnection(string clientName, double timeout = 10.0)
        {
            double initTime = EditorApplication.timeSinceStartup;
            while (!PythonRunner.IsClientConnected(clientName))
            {
                if (EditorApplication.timeSinceStartup - initTime > timeout)
                {
                    break;
                }
                yield return null;
            }
        }

        /// <summary>
        /// Ensures the in-process Python API is initialized.
        ///
        /// Safe to call frequently.
        ///
        /// Throws if there's an installation error.
        /// </summary>
        public static void EnsureInProcessInitialized()
        {
            if (s_IsInProcessInitialized)
            {
                return;
            }
            try
            {
                s_IsInProcessInitialized = true;
                DoEnsureInProcessInitialized();
            }
            catch
            {
                s_IsInProcessInitialized = false;
                throw;
            }
        }
        static bool s_IsInProcessInitialized = false;

        /// <summary>
        /// This is a helper for EnsureInProcessInitialized; call that function instead.
        ///
        /// This function assumes the API hasn't been initialized, and does the work of initializing it.
        /// </summary>
        static void DoEnsureInProcessInitialized()
        {
            ///////////////////////
            // Tell the Python interpreted not to generate .pyc files. Packages
            // are installed in read-only locations on some OSes and if package
            // developers forget to remove their .pyc files it could become
            // problematic. This can be changed at runtime by a script.
            System.Environment.SetEnvironmentVariable("PYTHONDONTWRITEBYTECODE", "1");

#if UNITY_EDITOR_OSX
            // On OSX, Python for .NET initializes with the system Python (/usr/bin/python). 
            // However, if there are other Python installations on the 
            // system (e.g. /usr/local/bin/python), the resulting sys.path might be 
            // favoring the local installation instead of the system one
            // Here we force the Python for .NET interpreter (the system's) to use the right 
            // Python home

            PythonEngine.PythonHome = "/usr";
#endif // UNITY_EDITOR_OSX

            // Validate the Python install and/or PYTHONHOME
            var pythonhomeFound = false;
            var paths = PythonEngine.PythonPath.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                if ( File.Exists($"{path}/site.py")
                 || (Path.GetExtension(path).ToLower() == ".zip" && File.Exists(path) ) )
                {
                    pythonhomeFound = true;
                    break;
                }
            }

            if (!pythonhomeFound)
            {
                throw new PythonInstallException("'site.py' could no be found. Verify that your Python 2.7 installation has not been moved or deleted, or that PYTHOMHOME points to the correct location.");
            }

            ///////////////////////
            // Initialize the engine if it hasn't been initialized yet.
            PythonEngine.Initialize();

            // Verify that we are running the right version of Python.
            if (!PythonEngine.Version.Trim().StartsWith(PythonRequiredVersion, StringComparison.Ordinal))
            {
                throw new PythonInstallException($"Python {PythonRequiredVersion} is required but your system Python is {PythonEngine.Version}.");
            }

            ///////////////////////
            // Add the packages we use to the sys.path, and put them at the head.
            // TODO: remove duplicates.
            using (Py.GIL())
            {
                // Start coverage here for in-process coverage
                if(!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("COVERAGE_PROCESS_START")))
                {
                    // Assume that if we are trying to run coverage, the module
                    // is in the path. Try to import it to give a better error message
                    dynamic coverage = null;
                    try
                    {
                         coverage = PythonEngine.ImportModule("coverage");
                    }
                    catch (PythonException e)
                    {
                        // Throw a more understandable exception if we fail.
                        if (e.Message.Contains("ImportError"))
                        {
                            throw new PythonInstallException(
                                $"Environment variable for code coverage is defined but no coverage package can be found. \n{e.Message}"
                                );
                        }
                        else
                        {
                            throw;
                        }
                    }
                    coverage.process_startup();
                }

                ///////////////////////
                // Add the packages we use to the sys.path, and put them at the head.
                // TODO: remove duplicates.

                // Get the builtin module, which is 'builtins' on Python3 and __builtin__ on Python2
                dynamic builtins = PythonEngine.ImportModule("__builtin__");
                // prepend to sys.path
                dynamic sys = PythonEngine.ImportModule("sys");
                dynamic syspath = sys.GetAttr("path");
                dynamic sitePackages = GetExtraSitePackages();
                dynamic pySitePackages = builtins.list();
                foreach(var sitePackage in sitePackages)
                {
                    pySitePackages.append(sitePackage);
                }
                pySitePackages += syspath;
                sys.SetAttr("path", pySitePackages);

                // Log what we did. TODO: just to the editor log, not the console.
                var sysPath = sys.GetAttr("path").ToString();
                Console.Write($"Python for Unity initialized:\n  version = {PythonEngine.Version}\n  sys.path = {sysPath}\n");
            }
        }

        /// <summary>
        /// Returns a list of the extra site-packages that we need to prepend to sys.path.
        ///
        /// These are absolute paths.
        /// </summary>
        static List<string> GetExtraSitePackages()
        {
            // The site-packages that contains rpyc need to be first. Others, we don't
            // have a well-reasoned order.
            var sitePackages = new List<string>();

            // 1. Our package's Python/site-packages directory. This needs to be first.
            {
                string packageSitePackage = Path.GetFullPath("Packages/com.unity.scripting.python/Python/site-packages");
                packageSitePackage = packageSitePackage.Replace("\\", "/");
                sitePackages.Add(packageSitePackage);
            }

            // 2. The present project's Python/site-packages directory, if it exists.
            if (Directory.Exists("Assets/Python/site-packages"))
            {
                var projectSitePackages = Path.GetFullPath("Assets/Python/site-packages");
                projectSitePackages = projectSitePackages.Replace("\\", "/");
                sitePackages.Add(projectSitePackages);
            }

            // 3. TODO: Do we want to iterate over the Package Manager to add their Python/site-packages?
            //    For now, we don't. But we might want to revisit that for later.

            // 4. The packages from the settings.
            foreach(var settingsPackage in PythonSettings.SitePackages)
            {
                if (!string.IsNullOrEmpty(settingsPackage))
                {
                    var settingsSitePackage = settingsPackage;
                    // C# can't do tilde expansion. Do a very basic expansion.
                    if (settingsPackage.StartsWith("~"))
                    {
                        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        // Don't use Path.Combine here. If settingsPackage starts with a '/', then 
                        // settingsPackage will be returned. As per documented behaviour.
                        settingsSitePackage = homeDirectory + "/" + settingsPackage.Substring(1);
                    }
                    settingsSitePackage = Path.GetFullPath(settingsSitePackage);
                    settingsSitePackage = settingsSitePackage.Replace("\\", "/");
                    sitePackages.Add(settingsSitePackage);
                }
            }

            return sitePackages;
        }

        /// <summary>
        /// Ensures the out of process API is initialized.
        ///
        /// Safe to call frequently.
        ///
        /// Throws if there's an installation error.
        /// </summary>
        public static void EnsureOutOfProcessInitialized()
        {
            if (s_IsOutOfProcessInitialized)
            {
                return;
            }
            try
            {
                s_IsOutOfProcessInitialized = true;
                DoEnsureOutOfProcessInitialized();
            }
            catch
            {
                s_IsOutOfProcessInitialized = false;
                throw;
            }
        }
        static bool s_IsOutOfProcessInitialized = false;

        static dynamic CheckForModule(string module, PyDict environmentOverride = null)
        {
            if (environmentOverride == null)
            {
                environmentOverride = new PyDict();
            }
            using (Py.GIL())
            {
                // retval = unity_rpyc.unity_server.UnityServer.spawn_subpython(["-c", "import {}".format(module)]).wait()
                dynamic UnityServer = PythonEngine.ImportModule("unity_python.server.server");
                PyList args = new PyList();
                args.Append(new PyString("-c"));
                args.Append(new PyString($"import {module}"));
                dynamic sub = UnityServer.spawn_subpython(args, wantLogging: false, python_executable: PythonSettings.PythonInterpreter, env_override: environmentOverride);
                if (sub == null)
                {
                    // Failing due to rpyc missing is one thing, but not even
                    // running means something worse is happening. Make it log.
                    UnityServer.spawn_subpython(args, wantLogging: true, python_executable: PythonSettings.PythonInterpreter);
                    throw new PythonInstallException($"Check the prior log; unable to run client Python '{PythonSettings.PythonInterpreter}'.");
                }
                sub.wait();
                return sub;
            }
        }

        /// <summary>
        /// Helper for EnsureOutOfProcessInitialized; call that function instead.
        ///
        /// This function assumes the API hasn't been initialized, and does the work of initializing it.
        /// </summary>
        static void DoEnsureOutOfProcessInitialized()
        {
            // We need Python in-process to run the out-of-process API.
            EnsureInProcessInitialized();

            // TODO: support per-client preferences.
            // Validate that the Python we're going to run actually works.
            if (string.IsNullOrEmpty(PythonSettings.ValidatePythonInterpreter()))
            {
                throw new PythonInstallException($"Check the Python Settings and verify the Python Interpreter points to a valid Python {PythonRequiredVersion} installation.\nPython Interpreter is currently '{PythonSettings.PythonInterpreter}'");
            }

            // We need rpyc on the Unity (server) side.
            // It's normally installed with the Python for Unity package, so this should always work,
            // if it fails it means there's a distribution bug or the user broke their package cache.
            using (Py.GIL())
            {
                var rpyc = PythonEngine.ImportModule("rpyc");
                if (!rpyc.IsTrue())
                {
                    throw new PythonInstallException($"rpyc can't be found. Try clearing your package cache and installing com.unity.scripting.python again.");
                }
            }

            // Start by testing for coverage. Failures due to coverage not 
            // isntalled or a non-existing config or invalid configuration 
            // passes as a failure to find RPyC
            // If user enabled coverage, check if the client can also find it
            bool clientFoundCoverage = false;
            if(!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("COVERAGE_PROCESS_START")))
            {
                // Fool the client in thinking coverage is not enabled. This way,
                // sitecustomize won't try to import it
                var environmentOverride = new PyDict();
                // The roundabout, awkward way to PyNone:
                environmentOverride[new PyString("COVERAGE_PROCESS_START")] = PyObject.FromManagedObject(null);
                dynamic coverageCheck = CheckForModule("coverage", environmentOverride);
                dynamic retval = coverageCheck.wait();
                clientFoundCoverage = !retval.IsTrue();

                if(!clientFoundCoverage)
                {
                    throw new PythonInstallException($"Coverage python package can't be found. Please install Coverage in {PythonSettings.PythonInterpreter}");
                }

                // Test once again to make sure the configuration file can be 
                // found and is valid. This is done in sitecustomize
                coverageCheck = CheckForModule("coverage");
            }

            // Verify the spawned Python can find rpyc. If it can't, the client would die silently, unable to connect.
            // Best to discover that here and raise an exception already.
            bool clientFoundRpyc = false;
            dynamic rpycCheck = CheckForModule("rpyc");
            using (Py.GIL())
            {
                dynamic retval = rpycCheck.wait();
                // retval 0 is success (a True retval would be some non-zero error code)
                clientFoundRpyc = !retval.IsTrue();
            }
            
            if (!clientFoundRpyc)
            {
                // Normally spawn_subpython will prepend rpyc to the sys.path - if we can't find rpyc something is weird.
                throw new PythonInstallException($"Please install rpyc in {PythonSettings.PythonInterpreter}");
            }

            // TOTAL SUCCESS!
            // We want to stop the client and server on application exit
            EditorApplication.quitting += OnQuit;
        }
    }
}
