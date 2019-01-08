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
    internal class MultiServerTest
    {
        [UnityTest]
        public IEnumerator OnlyOneServerAllowed()
        {
            // Start our own server.
            // Then start another server process. It should exit immediately with an error.
            PythonRunner.StartServer();
            var server_path = "Packages/com.unity.scripting.python/Python/site-packages/unity_python/server/server.py";
            dynamic p = PythonRunner.SpawnClient(server_path);

            // Wait for 10 seconds for the server to notice it can't run.
            // (It should only take milliseconds but on CI machines it can take a while.)
            double initTime = EditorApplication.timeSinceStartup;
            double timeout = 10;
            while(true)
            {
                using (Py.GIL())
                {
                    dynamic retcode = p.poll();
                    if (retcode != null)
                    {
                        Assert.AreNotEqual((int)p.returncode, 0);
                        break;
                    }
                }
                if (EditorApplication.timeSinceStartup - initTime > timeout)
                {
                    Assert.Fail("timed out");
                }
                yield return null;
            }
        }
    }
}
