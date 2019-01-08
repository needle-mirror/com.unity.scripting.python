using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using UnityEditor.Scripting.Python;
using Python.Runtime;
using UnityEditor;
using UnityEngine.TestTools;

using UnityEngine;

namespace Tests
{
    internal class evalExecTests
    {
        IEnumerator WaitForConnection(string clientName, double timeout = 10.0)
        {
            yield return PythonRunner.WaitForConnection(clientName, timeout);
            // Waits for connection for up to timeout seconds. May not acutally 
            // be connected
            Assert.That(PythonRunner.IsClientConnected(clientName), Is.True);
        }

        // Execute some statement on the client
        void TestRoutine(string clientName, int testValue)
        {
            using (Py.GIL())
            {
                PythonRunner.CallServiceOnClient(clientName, "exec", $"x={testValue}");
                //Verify the side-effect
                dynamic dict = PythonRunner.CallServiceOnClient(clientName, "globals");
                bool test = dict.__contains__("x");
                Assert.That(test, Is.True);
                int val = dict[new PyString("x")];
                Assert.That(val, Is.EqualTo(testValue));
                
                // Test the evaluation
                int value = PythonRunner.CallServiceOnClient(clientName, "eval", "x");
                Assert.That(value, Is.EqualTo(testValue));

                // Eval something that is not defined
                Assert.Throws<PythonException>( () => PythonRunner.CallServiceOnClient(clientName, "eval", "y"));
            }
        }

        [UnityTest]
        public IEnumerator TestEvalExec()
        {
            string evalExecClientPath = Path.Combine(
                Path.GetFullPath("Packages/com.unity.scripting.python"), 
                "Python", 
                "site-packages", 
                "unity_python", 
                "client", 
                "evalexec_client.py");
            string clientName = "com.unity.scripting.python.clients.evalexec";
            UnityEngine.Debug.Log(evalExecClientPath);
            PythonRunner.StartServer();
            dynamic clientProcess = PythonRunner.SpawnClient(evalExecClientPath);
            yield return WaitForConnection(clientName);
            Assert.That(PythonRunner.IsClientConnected(clientName), Is.True);

            // Run the test routine with a test value of 5
            TestRoutine(clientName, 5);

            PythonRunner.StopServer(false);
            // Test that the process is gone.
            using(Py.GIL())
            {
                int? ret = clientProcess.wait();
                Assert.That(ret, Is.Not.Null);
            }
        }

        [UnityTest]
        public IEnumerator TestEvalExecDerived()
        {
            // The use case for this is a client using it for something like a 
            // live link but also wants to add a custom service on top of it.
            string evalExecClientPath = Path.Combine(
                Path.GetFullPath("Packages/com.unity.scripting.python/"),
                "Tests",
                "rpyc",
                "derived_evalexec.py");
            string clientName = "com.unity.scripting.python.clients.evalexec.derived";
            
            UnityEngine.Debug.Log(evalExecClientPath);
            PythonRunner.StartServer();
            dynamic clientProcess = PythonRunner.SpawnClient(evalExecClientPath);
            yield return WaitForConnection(clientName);
            Assert.That(PythonRunner.IsClientConnected(clientName), Is.True);

            // Run the test routine with a test value of 5
            TestRoutine(clientName, 5);

            // Then call the custom service
            string specialString = PythonRunner.CallServiceOnClient(clientName, "special_service");
            Assert.That(specialString, Is.EqualTo("I'm a special service but all I got is this lousy log"));

            // Test the client can survive reconnection events
            PythonRunner.StopServer(true);
            PythonRunner.StartServer();
            yield return WaitForConnection(clientName);
            Assert.That(PythonRunner.NumClientsConnected(clientName), Is.EqualTo(1));

            // Re-run the test routine, but this time with a value of 7
            TestRoutine(clientName, 7);

            // Then call again the custom service
            specialString = PythonRunner.CallServiceOnClient(clientName, "special_service");
            Assert.That(specialString, Is.EqualTo("I'm a special service but all I got is this lousy log"));

            PythonRunner.StopServer(false);
            // Test that the process is gone.
            using(Py.GIL())
            {
                int? ret = clientProcess.wait();
                Assert.That(ret, Is.Not.Null);
            }
        }
    }
}