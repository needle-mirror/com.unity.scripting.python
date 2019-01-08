using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using Python.Runtime;
using UnityEditor;
using UnityEditor.Scripting.Python;
using UnityEngine;
using UnityEngine.TestTools;

class MockTests
{
    class ClientsLock : IDisposable
    {
        private dynamic serverModule = null;
        private dynamic clientsLock = null;

        public ClientsLock()
        {
            using (Py.GIL())
            {
                serverModule = PythonEngine.ImportModule("unity_python.server.server");
                clientsLock = serverModule.clients_lock;
                clientsLock.__enter__();
            }
        }

        public void Dispose()
        {
            using (Py.GIL())
            {
                clientsLock.__exit__();
            }
        }
    }

    /// <summary>
    /// Adds a new path to sys.path. On disposal, remove the added path
    /// </summary>
    class PathGuard : IDisposable
    {
        PyString path;
        dynamic sysModule = null;

        public PathGuard(string pathToAdd)
        {
            path = new PyString(pathToAdd);
            using (Py.GIL())
            {
                sysModule = PythonEngine.ImportModule("sys");
                sysModule.path.append(path);
            }
        }

        public void Dispose()
        {
            using (Py.GIL())
            {
                sysModule.path.remove(path);
            }
        }
    }

    /// <summary>
    /// Creates a new dictionary with a MockClient and sets it as the client 
    /// dict in the server. On Dispose, restore the original dictionary
    /// </summary>
    class MockClientGuard : IDisposable
    {
        dynamic serverModule = null;
        dynamic originalClientDict = null;
        string extraSysPath = Path.GetFullPath("Packages/com.unity.scripting.python/Tests/Mocking");
        PathGuard sysPathGuard = null;

        public dynamic client = null;

        public MockClientGuard()
        {
            using (Py.GIL())
            {
                // We need to add this folder to sys.path so we find the mocks
                sysPathGuard = new PathGuard(extraSysPath);
                string mockClientName = "mock_client";
                dynamic mocksModule = PythonEngine.ImportModule("test_mocks");
                dynamic clientHolder = mocksModule.MockClientHolder();
                client = clientHolder._conn.root;
                serverModule = PythonEngine.ImportModule("unity_python.server.server");

                using (new ClientsLock())
                {
                    originalClientDict = serverModule.clients;
                    dynamic clientList = new PyList();
                    clientList.append(clientHolder);
                    serverModule.clients = new PyDict();
                    serverModule.clients[new PyString(mockClientName)] = clientList;
                }
            }
        }

        public void Dispose()
        {
            using (Py.GIL())
            {
                using (new ClientsLock())
                {
                    serverModule.clients = originalClientDict;
                }
                sysPathGuard.Dispose();
            }
        }
    }

    [Test]
    public void Test_call_service_on_client()
    {
        using (Py.GIL())
        {
            string mockClientName = "mock_client";
            dynamic serverModule = PythonEngine.ImportModule("unity_python.server.server");

            using (new MockClientGuard())
            {
                // Call an existing client with an existing service
                serverModule.call_service_on_client(mockClientName, 0, "mock_service");
                LogAssert.Expect(LogType.Log, "I'm a bogus service");

                // Call with a non-existing client
                PythonException exc = Assert.Throws<PythonException>( () => serverModule.call_service_on_client("i_don_t_exist", 0, "mock_service"));
                Assert.That(exc.Message, Does.Match(@"KeyError"));

                // Call an existing client at a non-exisent index
                exc = Assert.Throws<PythonException>( () => serverModule.call_service_on_client(mockClientName, 666, "mock_service"));
                Assert.That(exc.Message, Does.Match(@"IndexError"));
            }
        }
    }

    [Test]
    public void Test_call_service_on_client_async()
    {
        using (Py.GIL())
        {
            string mockClientName = "mock_client";
            dynamic serverModule = PythonEngine.ImportModule("unity_python.server.server");

            using (new MockClientGuard())
            {
                // Because we mock, we expect an exception: RPyC async requests
                // requires a Netref, and these expect a connection and these ...
                // Test our code until RPyC raises.
                PythonException exc = Assert.Throws<PythonException>( () =>serverModule.call_service_on_client_async(mockClientName, 0, "mock_service"));
                Assert.That(exc.Message, Does.Match(@"must be a Netref"));

                // The following throws the same exceptions.
                // Call with a non-existing client
                exc = Assert.Throws<PythonException>( () => serverModule.call_service_on_client_async("i_don_t_exist", 0, "mock_service"));
                Assert.That(exc.Message, Does.Match(@"KeyError"));

                // Call an existing client at a non-exisent index
                exc = Assert.Throws<PythonException>( () => serverModule.call_service_on_client_async(mockClientName, 666, "mock_service"));
                Assert.That(exc.Message, Does.Match(@"IndexError"));
            }
        }
    }

    [Test]
    public void Test_handle_getattr()
    {
        // Server should be closed, we need to look at the job queue
        PythonRunner.StopServer(false);
        using (Py.GIL())
        {
            using(new PathGuard(Path.GetFullPath("Packages/com.unity.scripting.python/Tests/Mocking")))
            {
                dynamic serverModule = PythonEngine.ImportModule("unity_python.server.server");
                dynamic mocksModule = PythonEngine.ImportModule("test_mocks");
                dynamic builtins = PythonEngine.ImportModule("__builtin__");
                dynamic fakeService = serverModule.UnityService();
                // PyDict and convoluted code because converting a 
                // Dictionary<string, bool> to a Python dictionary is hard for
                // Python for .NET
                // Same configuration as when we start the server
                PyDict config = new PyDict();
                config[new PyString("allow_public_attrs")] = true.ToPython();
                config[new PyString("allow_setattr")] = true.ToPython();
                dynamic mockConnection = serverModule.UnityConnection(fakeService, mocksModule.MockChannel(), config: config.ToPython());

                // Test calling on the Connection's root
                dynamic dict = mockConnection._handle_getattr(fakeService, "dict");
                Assert.That(PyDict.IsDictType(dict()), Is.True);

                // handle fast calls (see UnityConnection's definition)
                bool test = mockConnection._fast_callattr_types.__contains__(builtins.list);
                Assert.That(test, Is.True);
                PyList l = new PyList();
                dynamic attr = mockConnection._handle_getattr(l, "pop");
                // A bit convoluted, but string conversion is hard for Python for .NET
                string attrName = attr.__name__.__str__();
                string popName = builtins.list.pop.__name__.__str__();
                Assert.That(attrName, Is.EqualTo(popName));

                // handle slow calls (What's not in the fast calls; must be
                // called on main thread.
                dynamic unityEngine = PythonEngine.ImportModule("UnityEngine");
                test = mockConnection._fast_callattr_types.__contains__(unityEngine.GameObject);
                Assert.That(test, Is.False);
                // Create a game object
                dynamic go = unityEngine.GameObject();
                go.name = "MyTestGameObject";
                // Called immediately since we're already on the main thread
                attr = mockConnection._handle_getattr(go, "name");
                string requestedGoName = attr.__str__();
                string goName = go.name;
                Assert.That(goName, Is.EqualTo(requestedGoName));
            }
        }
    }
    
    [Test]
    public void Test_call_on_main_thread()
    {
        // Stop the server we'll operate the job processing manually.
        PythonRunner.StopServer(false);

        using(new PathGuard(Path.GetFullPath("Packages/com.unity.scripting.python/Tests/Mocking")))
        {
            dynamic serverModule = null;
            dynamic mocksModule = null;
            dynamic func = null;
            int jobsInQueue = -1;
            using (Py.GIL())
            {
                serverModule = PythonEngine.ImportModule("unity_python.server.server");
                mocksModule = PythonEngine.ImportModule("test_mocks");
                func = mocksModule.mock_function;
                
                serverModule.call_on_main_thread(func);
                // Assert job was immediately processed.
                jobsInQueue = serverModule.jobs.qsize();
                Assert.That(jobsInQueue, Is.Zero);
                LogAssert.Expect(LogType.Log, "In the mock function");
            }

            // release the GIL so the task can do something; but tread carefully
            // (pun intented) as it's quite easy to deadlock. Use Python's
            // time.sleep while the task posts the job in the job queue
            // to avoid crashes and deadlocks.

            Task outOfThread = Task.Run(() => 
                {
                    using (Py.GIL())
                    {
                        dynamic result = serverModule.call_on_main_thread(func);
                    }
                }
            );

            dynamic time = PythonEngine.ImportModule("time");

            // Wait for the job to be successfully posted ...
            double initTime = EditorApplication.timeSinceStartup;
            while(jobsInQueue != 1)
            {
                if (EditorApplication.timeSinceStartup - initTime > 10)
                {
                    //but wait at most 10 seconds.
                    Assert.Fail("The task failed to start. Unity may freeze on the next domain reload/shutdown");
                    break;
                }
                using (Py.GIL())
                {
                    time.sleep(0.1);
                    jobsInQueue = serverModule.jobs.qsize();
                }
            }

            // ... Then process it.
            using (Py.GIL())
            {
                serverModule.process_jobs();
            }

            LogAssert.Expect(LogType.Log, "In the mock function");
            Assert.That(outOfThread.IsCompleted, Is.True);
            using (Py.GIL())
            {
                jobsInQueue = serverModule.jobs.qsize();
            }
            Assert.That(jobsInQueue, Is.Zero);
        }
    }

    void StartServer()
    {
        using (Py.GIL())
        {
            dynamic serverModule = PythonEngine.ImportModule("unity_python.server.server");
            serverModule.start_server();
            dynamic server = serverModule.server;
            // We do create the server, right?
            Assert.That(server, Is.Not.Null);
            int numClients = serverModule.clients.__len__();
            // Starting the server should have no clients
            Assert.That(numClients, Is.Zero);
            // Also test this
            bool isActive = serverModule.is_server_active();
            Assert.That(isActive, Is.True);
        }
    }

    void StopServer (bool shouldRestart)
    {
        using (Py.GIL())
        {
            dynamic serverModule = PythonEngine.ImportModule("unity_python.server.server");
            // Stop the server
            serverModule.close_server(is_rebooting: shouldRestart);
            // No more server
            dynamic server = serverModule.server;
            server = serverModule.server;
            // nUnit gets confused whith dynamic object and Is.Null constraints
            Assert.That(server == null, Is.True);
            int numClients = serverModule.clients.__len__();
            // stopping the server creates a new (empty) dict
            Assert.That(numClients, Is.Zero);
            // Even if we say we restart the server, it should be inactive.
            bool isActive = serverModule.is_server_active();
            Assert.That(isActive, Is.False);
        }

    }

    [Test]
    public void Test_start_stop_server()
    {
        // Server needs to be stopped before the test
        PythonRunner.StopServer(false);

        // Start the server, without any clients because it creates a new clients dict
        StartServer();
        using (MockClientGuard clientGuard = new MockClientGuard())
        {

            // Stop the server
            StopServer(false);

            // Make sure the client got informed the server is not coming back
            using (Py.GIL())
            {
                bool? shouldReconnect = clientGuard.client.shutdown_status;
                Assert.That(shouldReconnect, Is.False);
            }
        }
    }

    [Test]
    public void Test_start_restart_stop_server()
    {
        // Server needs to be stopped before the test
        PythonRunner.StopServer(false);
        
        // Start the server, without any clients because it creates a new clients dict
        StartServer();
        using (MockClientGuard clientGuard = new MockClientGuard())
        {
            // Stop with the intention of restarting
            StopServer(true);
            // Make sure the client got informed the server is coming back
            using (Py.GIL())
            {
                bool? shouldReconnect = clientGuard.client.shutdown_status;
                Assert.That(shouldReconnect, Is.True);
            }
        }
        // Restart the server 
        StartServer();
        // And make sure it can be stopped
        StopServer(false);
    }
    
    [Test]
    public void Test_process_jobs()
    {
        // Stop the server, we need to control the job queue and the rate of
        // job processing
        PythonRunner.StopServer(false);
        using(new PathGuard(Path.GetFullPath("Packages/com.unity.scripting.python/Tests/Mocking")))
        {
            using (Py.GIL())
            {
                dynamic serverModule = PythonEngine.ImportModule("unity_python.server.server");
                dynamic jobsQueue = serverModule.jobs;
                dynamic mocksModule = PythonEngine.ImportModule("test_mocks");
                dynamic mockFunction = mocksModule.mock_busy_function;

                // Precondition: jobs queue is empty.
                int jobsInQueue = serverModule.jobs.qsize();
                Assert.That(jobsInQueue, Is.Zero, "Some jobs are left in the jobs queue. The queue must be empty for this test.");

                // put more jobs in the queue than we'll process. This is to 
                // measure the behaviour of process_jobs. For each batch of 
                // processing, we test that the expected number of jobs got 
                // processed.
                int maxJobs = 20;
                for(int i = 0; i < maxJobs; i++)
                {
                    jobsQueue.put(mockFunction);
                }

                // Test for a number of jobs
                int jobsToProcess = 7;
                double jobLength = 0.1; // same as defined in the mock_function()
                double interval = jobLength * jobsToProcess;
                
                double initTime = EditorApplication.timeSinceStartup;
                serverModule.process_jobs(batch_time: interval);
                double timeItTook = EditorApplication.timeSinceStartup - initTime;

                jobsInQueue = jobsQueue.qsize();
                Debug.Log($"processed {maxJobs - jobsInQueue} jobs in {timeItTook} seconds");
                
                // CI machines are *slow* : we can't rely on timings, so just 
                // making sure it did not process more jobs than it was supposed 
                // to is Good Enough™
                // Assert that:
                // 1) We did process a batch (more than one job)
                // 2) We did not process more than the maximum jobs we expected to
                //    so we know the timeout works
                Assert.That(jobsInQueue, Is.InRange(maxJobs - jobsToProcess, maxJobs -2));

                // Give a batch time lower than the job (which takes 100 ms), to
                // execute only one job.
                jobsInQueue = jobsQueue.qsize();
                serverModule.process_jobs(batch_time: 0.01);
                int jobsLeftInQueue = jobsQueue.qsize();
                Assert.That(jobsInQueue-jobsLeftInQueue, Is.EqualTo(1));

                // Call with the default argument value. Since it's 1/90 by
                // default, it should process only one job.
                jobsInQueue = jobsQueue.qsize();
                serverModule.process_jobs();
                jobsLeftInQueue = jobsQueue.qsize();
                Assert.That(jobsInQueue-jobsLeftInQueue, Is.EqualTo(1));

                // Giving None to Queue.queue.get() will wait until something
                // is available in the queue. At the time of writing, passing
                // None to process_jobs will not process any jobs and raise a
                // TypeError. Ensure this behavior
                Debug.Log("This test may hang Unity. See the comments in this test in cases of hangs after this message");
                jobsInQueue = jobsQueue.qsize();
                var exc = Assert.Throws<PythonException>(() => serverModule.process_jobs(batch_time: null));
                Assert.That(exc.Message, Does.Match(@"TypeError"));
                jobsLeftInQueue = jobsQueue.qsize();
                Assert.That(jobsInQueue, Is.EqualTo(jobsLeftInQueue));

                // Since we can't clear the queue, replace it
                dynamic queueModule = PythonEngine.ImportModule("Queue");
                serverModule.jobs = queueModule.Queue();
                jobsInQueue = serverModule.jobs.qsize();
                Assert.That(jobsInQueue, Is.Zero);
            }
        }
    }
}
