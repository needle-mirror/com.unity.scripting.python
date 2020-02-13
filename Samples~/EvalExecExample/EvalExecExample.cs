using UnityEditor;
using UnityEditor.Scripting.Python;
using UnityEngine;
using Python.Runtime;

/// <summary>
/// Example class to showcase the base EvalExec client.
/// </summary>
public class EvalExecExample
{
    /// <summary>
    /// The client's unique name, which will be referred to in calls from Unity, as well as used for logging.
    /// </summary>
    const string ClientName = "com.unity.scripting.python.clients.evalexec";

    /// <summary>
    /// Example method that sends an exec command to a running EvalExec client.
    /// Similar to calling the `exec` builtin Python function.
    /// </summary>
    [MenuItem("Python/Examples/Eval-exec Example/Send exec")]
    public static void SendExec()
    {
        Debug.Log("running 'exec(\"x = 6\")'");
        // A client must exist for the service to successfully be run. See the SpawnClient() method and associated comments.
        PythonRunner.CallServiceOnClient(ClientName, "exec", "x = 6");
        Debug.Log("ran 'exec(\"x = 6\")'");
    }


    /// <summary>
    /// Example method that sends an eval command to a running EvalExec client.
    /// Similar to calling the `eval` builtin Python function.
    /// </summary>
    [MenuItem("Python/Examples/Eval-exec Example/Send eval")]
    public static void SendEval()
    {
        Debug.Log("running 'eval(\"x\")'");
        // A (Py.GIL()) block must be used to ensure stability by holding the Python global interpreter lock.
        using (Py.GIL()) {
            // If it is not needed, the return value can safely be discarded.
            dynamic x = PythonRunner.CallServiceOnClient(ClientName, "eval", "x");
            Debug.Log($"ran 'eval(\"x\")' and got {x}");
        }
    }

    /// <summary>
    /// Starts the EvalExec client.
    /// </summary>
    [MenuItem("Python/Examples/Eval-exec Example/Spawn client")]
    public static void SpawnClient()
    {
        // The client here spawned could also be launched from the command line.
        PythonRunner.SpawnClient("Packages/com.unity.scripting.python/Python/site-packages/unity_python/client/evalexec_client.py",
                wantLogging: true);
    }
}
