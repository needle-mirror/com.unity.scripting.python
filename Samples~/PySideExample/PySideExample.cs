using System.IO;
using UnityEditor;
using UnityEditor.Scripting.Python;
using UnityEngine;
using Python.Runtime;

namespace PythonExample
{
    /// <summary>
    /// This is a simplified example of how to create a menu item to launch a
    /// python client that then connects back to Unity.
    /// </summary>
    public class PySideExample
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

        /// <summary>
        /// Menu to launch the client
        /// </summary>
        [MenuItem("Python/Examples/PySide Example")]
        public static void OnMenuClick()
        {
            PythonRunner.SpawnClient(
                    file: $"{__DIR__()}/PySideExample.py", 
                    wantLogging: true);
        }

        /// <summary>
        /// Called by the client when it's been spawned, to register to the hierarchyChanged event.
        /// </summary>
        static public void Subscribe()
        {
            // Prevent double subscription.
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        /// <summary>
        /// Called by the editor when the scene hierarchy changed (an object got created, deleted, or reparented).
        /// </summary>
        public static void OnHierarchyChanged()
        {
            // Notify the client that the hierarchy has changed.
            //
            // Asynchronous call, since we don't need to wait for the response.
            //
            // Being asynchronous prevents a deadlock: the PySide client calls
            // into the server to read the scene, but in a synchronous call the
            // server would be waiting for the client!
            PythonRunner.CallAsyncServiceOnClient("PySide Example", "on_hierarchy_changed");
        }
    }
}
