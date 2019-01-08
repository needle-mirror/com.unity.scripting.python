using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.Scripting.Python;
using Python.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace rpycTests
{
/// <summary>
    /// The following tests may seem a bit complicated, but since we need to
    /// test with other processes, this takes time and thus we need to 
    /// implement waiting methods. We leverage the UnityTest attribute 
    /// for our waits. Contrary to regular C#, we can nest `yield return` calls
    ///  and get the "intuitive" behaviour.
    /// </summary>
    public class rpycUnitTests
    {
        // The following three variables are for the "standard" test client. 
        // Unless you're testing a special scenario, this client should be used.
        const string TestsPath = "Packages/com.unity.scripting.python/Tests/rpyc";
        const string InitModulePath = TestsPath + "/rpyc_test_client_init.py";
        const string ClientName = "com.unity.scripting.python.tests.rpyc";

        // Not ideal but we have to live with it. StartClient returns an 
        // IEnumerator and can't have out parameters, but we need the process 
        // object to test later if the client process has quit properly.
        dynamic m_lastStartedProcess = null;

        // Value to be modified by connecting clients
        // Use this instead of expecting/waiting a Log.
        static public bool ClientContactFlag {get;set;} = false;

        static System.Random Randomizer = new System.Random();

        bool HasClientMadeContact()
        {
            return ClientContactFlag;
        }

        [SetUp]
        public void SetUp()
        {
            ClientContactFlag = false;
        }

        [TearDown]
        public void TearDown()
        {

            // Assert no clients are connected. If you fail here, make sure your
            // test cleans up the clients. 
            // (and make sure you yield return StopClientAndWaitForProcessEnd())
            Assert.That(PythonRunner.NumClientsConnected(), Is.Zero);
            // Reduce crashes by collecting the GC now.
            System.GC.Collect();
            using (Py.GIL())
            {
                dynamic gc = Py.Import("gc");
                gc.collect();
            }
            System.GC.Collect();
        }

        /// <summary>
        /// Waits for a started client to be connected to the server
        /// </summary>
        /// <param name="clientName">The name of the client to wait on</param>
        /// <param name="timeout">The maximum length of time for to wait the
        /// client to be connected, in seconds</param>
        /// <returns></returns>
        IEnumerator WaitForConnection(string clientName, double timeout = 10.0)
        {
            // Waits for connection for up to timeout seconds. May not acutally 
            // be connected
            yield return PythonRunner.WaitForConnection(clientName, timeout);
            Assert.That(PythonRunner.IsClientConnected(clientName), Is.True);
        }

        /// <summary>
        /// Starts a client (and the server if needed). Use as an IEnumerable or a coroutine
        /// </summary>
        /// <param name="clientID">Client's second name. Specified by the caller</param>
        /// <returns></returns>
        public IEnumerator StartClient(string clientID, string clientName = ClientName)
        {
            Debug.Log("spawning");

            // Start the client.
            m_lastStartedProcess = PythonRunner.SpawnClient(InitModulePath, wantLogging: true, clientID);

            // Wait up to 10s for the client to connect.
            yield return WaitForConnection(clientName);
            Debug.Log("connected");

            // Verify it's the client we expect.
            using (Py.GIL())
            {
                dynamic value = PythonRunner.CallServiceOnClient(clientName, "get_argv", 1);
                Assert.That(value.ToString(), Is.EqualTo(clientID));
                value = PythonRunner.CallServiceOnClient(clientName, "get_restart_count");
                Assert.That((int)value, Is.EqualTo(0));
            }

        }

        /// <summary>
        /// Stops a currently conencted client.
        /// </summary>
        /// <param name="clientName">The name of the client to stop.</param>
        void StopClient(string clientName = ClientName)
        {
            try
            {
                // Now close that client (synchronous to make sure it's gone before we run the next test).
                PythonRunner.CallServiceOnClient(clientName, "on_server_shutdown", false);
            }
            catch (PythonException ex) 
            {
                // The client drops off before we get an answer so we normally get an EOFError here.
                Assert.That(ex.Message, Does.Match(@"EOFError"));
            }
        }

        /// <summary>
        /// Stops the server, then restart it, and wait for client to reconnect.
        /// </summary>
        /// <param name="clientID">Client ID used to see if the reconnected 
        /// client is the one we expect. Only used if the `shouldReconnect` 
        /// parameter is `true`</param>
        /// <returns></returns>
        public IEnumerator RestartServer(string clientID = "")
        {
            Debug.Log("closing server");

            // Stop the server.
            PythonRunner.StopServer(inviteReconnect: true);

            // Verify it's stopped.
            Assert.That(!PythonRunner.IsClientConnected(ClientName));

            // Restart the server.
            PythonRunner.StartServer();

            Debug.Log("reconnecting");

            // Give the client another 10s to reconnect.
            yield return WaitForConnection(ClientName);
            
            Debug.Log("reconnected");
            using (Py.GIL())
            {
                dynamic value = PythonRunner.CallServiceOnClient(ClientName, "get_argv", 1);
                Assert.That(value.ToString(), Is.EqualTo(clientID));
                value = PythonRunner.CallServiceOnClient(ClientName, "get_restart_count");
                Assert.That((int)value, Is.EqualTo(1));
            }
        }

        /// <summary>
        /// Because UnityTests can only yield return null (or an IEnumerator 
        /// yield returning null), we cannot use Unity's WaitForSeconds. Make
        /// our own version.
        /// If a condition function was given, if the condition never evaluated
        /// to True during the loop, raises a Assert.Fail
        /// </summary>
        /// <param name="waitTime">The interval of time to wait for, in seconds.</param>
        /// <param name="condition">A function returning a boolean. If the function returns true, exit early.</param>
        /// <returns></returns>
        public IEnumerator WaitForSecondsDuringUnityTest(double waitTime, Func<bool> condition = null)
        {
            double initTime = EditorApplication.timeSinceStartup;
            double elapsedTime = 0.0;
            while ( elapsedTime < waitTime)
            {
                elapsedTime = EditorApplication.timeSinceStartup - initTime;
                if(condition != null && condition())
                {
                    yield break;
                }
                yield return null;
            }

            if(condition != null)
            {
                Assert.Fail("Condition in the loop never evaluated to True");
            }

        }

        /// <summary>
        /// Returns an IEnumerator to await the end of process. Asserts if the
        /// timeout is reached.
        /// </summary>
        /// <param name="process">The process to wait on. A python popen object</param>
        /// <param name="timeout">The maximum length of time for to wait the
        /// process to end, in seconds</param>
        /// <returns></returns>
        IEnumerator WaitForProcessEnd(dynamic process, double timeout = 5.0)
        {
            double initTime = EditorApplication.timeSinceStartup;
            double elapsedTime = 0.0;
            while (elapsedTime < timeout)
            {
                elapsedTime = EditorApplication.timeSinceStartup - initTime;
                using(Py.GIL())
                {
                    int? retcode = process.poll();
                    // popen.poll() returns None if process hasn't finished yet
                    if(retcode != null)
                    {
                        yield break;
                    }
                }
                yield return null;
            }
            Assert.That(elapsedTime, Is.LessThan(timeout));
        }

        /// <summary>
        /// Stops the client and waits for the process to end.
        /// </summary>
        /// <param name="process">The process to wait on. A python popen object</param>
        /// <param name="clientName">The name of the python client to wait on.</param>
        /// <param name="timeout">The maximum length of time for to wait the
        /// process to end, in seconds</param>
        /// <returns></returns>
        IEnumerator StopClientAndWaitForProcessEnd(dynamic process, string clientName = ClientName, double timeout = 5.0)
        {
            StopClient(clientName);

            yield return WaitForProcessEnd(process, timeout);
        }

        /// <summary>
        /// Tests that we can:
        /// - spawn a client
        /// - call a service on it and get a result
        /// - shut down the server inviting a reconnect
        /// - reopen the server
        /// - find the same client and have it return proof that it knows the server shut down
        /// </summary>
        /// <returns>The and stop server.</returns>
        [UnityTest]
        public IEnumerator StartAndStopServer()
        {
            var randomValueString = Randomizer.Next().ToString("x");

            // Start-restart test
            yield return StartClient(randomValueString);
            yield return RestartServer(randomValueString);
            // Stop it for good
            yield return StopClientAndWaitForProcessEnd(m_lastStartedProcess, ClientName);
        }

        /// <summary>
        /// Tests that we can:
        /// - spawn a client
        /// - call a service on it asynchronously and have it play well with a coroutine
        /// - have the client call into Unity to manipulate the scene (create and rename an object)
        /// </summary>
        [UnityTest]
        public IEnumerator CoroutineClientToServerCall()
        {
            // Start the client.
            var clientID = "CoroutineClient";

            yield return StartClient(clientID);

            // Make sure there is no cylinder in the scene yet
            var go = GameObject.Find("myCylinder");
            Assert.IsNull(go);

            // Create it, asynchronously since otherwise we'd deadlock.
            var iter = PythonRunner.CallCoroutineServiceOnClient(ClientName, "create_cylinder", "myCylinder");
            while (iter.MoveNext())
            {
                yield return null;
            }
            // There should be a cylinder now
            go = GameObject.Find("myCylinder");
            Assert.IsNotNull(go);
            yield return StopClientAndWaitForProcessEnd(m_lastStartedProcess, ClientName);
        }

        /// <summary>
        /// Note: nUnit should support asynchronous tests, but since unity is on a custom version
        /// there is something that prevents it from playing nice. "Await" manually on it
        /// </summary>
        [UnityTest]
        public IEnumerator AsyncClientToServerCall()
        {
            // Start the client.
            var clientID = "AsyncClient";

            yield return StartClient(clientID);

            // Make sure there is no cube in the scene yet
            var go = GameObject.Find("myCube");
            Assert.IsNull(go);

            // Create it, asynchronously since otherwise we'd deadlock.
            var task = PythonRunner.CallAsyncServiceOnClient(ClientName, "create_cube", "myCube");
            // wait for it to be completed
            while(!task.IsCompleted)
            {
                yield return null;
            }
                        
            // There should be a cube now
            go = GameObject.Find("myCube");
            Assert.IsNotNull(go);
            yield return StopClientAndWaitForProcessEnd(m_lastStartedProcess, ClientName);
        }

        /// UT-1449 FREEZE: Entering play mode during bootstrapping freezes UnityTests
        /// If the client is performing a "long" operation and the server 
        /// stops midway, Unity freezes
        [UnityTest]
        public IEnumerator UT1449StopServerWhileClientIsBusy()
        {
            var clientID = "busyClient";
            // Start the server
            yield return StartClient(clientID);
        
            // Execute the "long" client script
            var task = PythonRunner.CallAsyncServiceOnClient(ClientName, "busy_work");

            // Give some time for the script to start on the client
            // client sleeps for 8 seconds, wait for 4 seconds to interrupt
            // "mid-computation"
            yield return WaitForSecondsDuringUnityTest(4);

            // Stop the server. Unity should not hang
            PythonRunner.StopServer(false);
            Assert.That(!PythonRunner.IsClientConnected(ClientName));

            // no timeout
            yield return WaitForProcessEnd(m_lastStartedProcess);
        }

        [UnityTest]
        /// <summary>
        /// Tests that when a client is killed, the server survives
        /// </summary>
        public IEnumerator TestKillClient()
        {
            // Don't use the StartClient function here, we need a bit more control.
            
            // Start and wait for the client to connect.
            dynamic processToKill = PythonRunner.SpawnClient(InitModulePath, wantLogging: true, "testKill" );
            yield return WaitForConnection(ClientName);
            
            using (Py.GIL())
            {
                dynamic value = PythonRunner.CallServiceOnClient(ClientName, "get_argv", 1);
                Assert.That(value.ToString(), Is.EqualTo("testKill"));
                // Kill the process
                processToKill.kill();
                // dynamic retval = processToKill.poll();
                yield return WaitForSecondsDuringUnityTest(10, ()=>
                    {
                        using (Py.GIL())
                        {
                            return processToKill.poll() != null;
                        }
                    }
                );
            }
            Assert.That(PythonRunner.IsClientConnected(ClientName), Is.False);

            // Is the server still alive?
            string newProcessName = "testReconnect";
            dynamic newProcess = PythonRunner.SpawnClient(InitModulePath, wantLogging: true, newProcessName );
            
            yield return WaitForConnection(ClientName);

            using (Py.GIL())
            {
                dynamic value = PythonRunner.CallServiceOnClient(ClientName, "get_argv", 1);
                Assert.That(value.ToString(), Is.EqualTo(newProcessName));
                // Also kill the new one
                newProcess.kill();
                yield return WaitForSecondsDuringUnityTest(10, ()=>
                    {
                        using (Py.GIL())
                        {
                            return newProcess.poll() != null;
                        }
                    }
                );
            }
            Assert.That(PythonRunner.IsClientConnected(ClientName), Is.False);
        }

        [UnityTest]
        /// <summary>
        /// This test tries to simulate a crashed or frozen client, and we start
        /// a new one. Makes sure the default setting is communicating with the 
        /// newest client, not the oldest.
        /// </summary>
        public IEnumerator TestNewerClient()
        {
            string originalState = "";
            // We don't use the convenience methods defined above because we
            // need a bit more control

            // Spawn the first client
            dynamic processA = PythonRunner.SpawnClient(InitModulePath, wantLogging: true, "A" );
            yield return WaitForConnection(ClientName);
            
            using (Py.GIL())
            {
                // Test client has connected
                dynamic value = PythonRunner.CallServiceOnClient(ClientName, "get_argv", 1);
                Assert.That(value.ToString(), Is.EqualTo("A"));

                // Client sets the state to a default value when starting up...
                originalState = PythonRunner.CallServiceOnClient(ClientName, "get_state");
                // ...Then modify it
                string newState = "modified";
                PythonRunner.CallServiceOnClient(ClientName, "set_state", newState);
                string currentState = PythonRunner.CallServiceOnClient(ClientName, "get_state");
                Assert.That(currentState, Is.EqualTo(newState));
            }

            // Start the newer client
            dynamic processB = PythonRunner.SpawnClient(InitModulePath, wantLogging: true, "B" );
            // We can't rely on IsClientConnected because the underlying logic is
            // numberOfConnectedClients(clientName) != 0
            // But we can test for the "takeover" of the newer client.
            yield return WaitForSecondsDuringUnityTest(10, () =>
                {
                    using (Py.GIL())
                    {
                        string ret = PythonRunner.CallServiceOnClient(ClientName, "get_argv", 1);
                        return ret == "B";
                    }
                }
            );
            // Test there are really two clients
            Assert.That(PythonRunner.NumClientsConnected(ClientName), Is.EqualTo(2));

            string newClientState = PythonRunner.CallServiceOnClient(ClientName, "get_state");
            // Test it's equal to the default
            Assert.That(newClientState, Is.EqualTo(originalState));

            // Inverse order is important here, since we always talk to the last
            // client connected
            yield return StopClientAndWaitForProcessEnd(processB, ClientName);
            yield return StopClientAndWaitForProcessEnd(processA, ClientName);
        }

        /// <summary>
        /// Calls a service but there's no client.
        ///
        /// Verify it makes a reasonable error.
        /// </summary>
        [Test]
        public void CallServiceWithoutClient()
        {
            // Ensure the server is running.
            PythonRunner.StartServer();

            // Try to call a nonexistent client.
            Assert.Throws<PythonException>(() => PythonRunner.CallServiceOnClient("Not a client, shhh", "hello_client"));
        }

        [UnityTest]
        public IEnumerator TestClientWithoutServices()
        {
            // Don't use the StartClient function, we need a bit more control.
            
            // Start and wait up to 10s for the client to connect.
            dynamic clientProcess = PythonRunner.SpawnClient(Path.Combine(TestsPath, "empty_client.py"), wantLogging: true, "testEmpty" );
            Debug.Log(Path.Combine(TestsPath, "empty_client.py"));
            // "Unity Client" is the default client name, as seen in unity_client.py
            string defaultClientName = "Unity Client";
            WaitForConnection(defaultClientName);
            
            Debug.Log("connected");
            // Wait for 15 secs seconds maximum. Client waits 2 seconds and calls Debug.Log
            yield return WaitForSecondsDuringUnityTest(15, HasClientMadeContact);

            yield return StopClientAndWaitForProcessEnd(clientProcess, defaultClientName);
        }

        [UnityTest]
        public IEnumerator TestServedExposedFunctions()
        {
            yield return StartClient("test_exposed");
            // Calls the exposed_* functions of the server and does some correctness tests
            var iter = PythonRunner.CallCoroutineServiceOnClient(ClientName, "test_server");
            while(iter.MoveNext())
            {
                yield return null;
            }
            
            using (Py.GIL())
            {
                // Casting (C#) object to non-object types is hard
                PyObject obj = iter.Current as PyObject;
                bool passed = obj.IsTrue();
                Assert.That(passed, Is.True);
            }

            yield return StopClientAndWaitForProcessEnd(m_lastStartedProcess);
        }
        
        [UnityTest]
        /// <summary>
        /// This tests the exposed functions on the base client class.
        /// </summary>
        /// <returns></returns>
        public IEnumerator TestBaseClient()
        {
            string baseClientPath = Path.Combine(TestsPath, "test_base_client.py");
            dynamic clientProcess = PythonRunner.SpawnClient(baseClientPath, wantLogging: true);

            string defaultClientName = "Unity Client";
            yield return WaitForConnection(defaultClientName);
            string name = null;
            using (Py.GIL())
            {
                name = PythonRunner.CallServiceOnClient(defaultClientName, "client_name");
            }
            Assert.That(name, Is.EqualTo(defaultClientName));

            // Fake the server going away
            try
            {
                PythonRunner.CallServiceOnClient(defaultClientName, "on_server_shutdown", true);
                // Normally unreachable
                Assert.Fail("on_server_shutdown method did not throw");
            }
            catch (PythonException ex) 
            {
                // This is entirely expected; The client drops off before we get 
                // an answer so we normally get an EOFError here.
                Assert.That(ex.Message, Does.Match(@"EOFError"));
            }

            yield return WaitForProcessEnd(clientProcess);
        }
    }
}
