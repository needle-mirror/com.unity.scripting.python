# Use Python APIs in C#

Use Python APIs in C# with [Python for .NET](https://pythonnet.github.io/).

## Requirements and syntax

Before writing your Python code, you have to:

1. Ensure that Python is initialized with `PythonRunner.EnsureInitialized()`.

2. Grab the CPython [Global Interpreter Lock (GIL)](https://docs.python.org/3.10/glossary.html#term-global-interpreter-lock).

>[!NOTE]
>Using `Py.GIL` requires a reference to the `Python.Runtime` assembly: [create a new assembly reference in Unity](https://docs.unity3d.com/Manual/ScriptCompilationAssemblyDefinitionFiles.html#create-asmref) and set the **Assembly Definition** property to `com.unity.scripting.python.editor`.

In practice, the code structure in C# must look like this:

```
PythonRunner.EnsureInitialized();
using (Py.GIL())
{
    ...
    your code goes here
    ...
}
```

## Example: log the current Python version in the Console

### Code

The following C#/Python code creates a menu item that logs the current Python version in the Unity Console.

```
using Python.Runtime;
using UnityEditor;
using UnityEditor.Scripting.Python;

public class MyPythonScript
{
    [MenuItem("MyPythonScripts/Log Python Version")]
    public static void LogPythonVersion()
    {
        PythonRunner.EnsureInitialized();
        using (Py.GIL())
        {
            try
            {
                dynamic sys = Py.Import("sys");
                UnityEngine.Debug.Log($"python version: {sys.version}");
            }
            catch(PythonException e)
            {
                UnityEngine.Debug.LogException(e);
            }
        }
    }
}
```

### Test

From the main menu of the Editor, select **MyPythonScripts** > **Log Python Version**.

The Python version currently used in your project should appear in the Unity Console.

## Additional references

* [Call Python instructions from C#](csharp-run-string.md)
* [Call Python script files from C#](csharp-run-file.md)
