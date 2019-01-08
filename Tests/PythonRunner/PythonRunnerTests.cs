using NUnit.Framework;
using System;
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Scripting.Python;
using Python.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests
{
    internal class PythonRunnerTests
    {

        private static string TestsPath = Path.Combine(Path.GetFullPath("Packages/com.unity.scripting.python"), "Tests", "PythonRunner");
        private static Regex PythonExceptionRegex = new Regex(@"Python\.Runtime\.PythonException");

        [Test]
        public void TestRunString()
        {
            // Something valid
            string goName = "Bob";
            PythonRunner.RunString($"import UnityEngine;obj = UnityEngine.GameObject();obj.name = '{goName}'");
            var obj = GameObject.Find(goName);
            Assert.That(obj, Is.Not.Null);

            // Same code, with obvious error
            Assert.Throws<PythonException>( () => 
                {
                    PythonRunner.RunString($"import UnityEngineobj = UnityEngine.GameObject();obj.name = '{goName}'");
                } );
        }

        [Test]
        public void TestRunFile()
        {
            string validFileName = Path.Combine(TestsPath, "testPythonFile.py");
            string fileWithErrorsName = Path.Combine(TestsPath, "testPythonFileWithError.py");
            string nonExistantFile = Path.Combine(TestsPath, "doesNotExist.py");
            string notAPythonFile = Path.Combine(TestsPath, "notAPythonFile.txt");

            // null file
            Assert.Throws<ArgumentNullException>( () => 
                {
                    PythonRunner.RunFile(null);
                } );

            // does not exist
            Assert.Throws<FileNotFoundException>( () => 
                {
                    PythonRunner.RunFile(nonExistantFile);
                } );

            // not a python file. Throws syntax error. File must not be empty
            Assert.Throws<PythonException>( () => 
                {
                    PythonRunner.RunFile(notAPythonFile);
                } );
            
            // Indentation error
            Assert.Throws<PythonException>( () => 
                {
                    PythonRunner.RunFile(fileWithErrorsName);
                } );

            // finally, a good, valid, file
            PythonRunner.RunFile(validFileName);
            // should create a game object named Alice
            var go = GameObject.Find("Alice");
            Assert.That(go, Is.Not.Null);
            GameObject.DestroyImmediate(go);
        }

        
    }
}