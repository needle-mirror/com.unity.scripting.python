# Run Python instructions from C#

Use the `PythonRunner.RunString` method to execute a single Python instruction from C#.

## Example: create a menu item that executes a simple Python instruction

### C# code

The following C# code creates a menu item that logs "Hello World!" in the Unity Console:

```
using UnityEditor.Scripting.Python;
using UnityEditor;

public class HelloWorld
{
    [MenuItem("MyPythonScripts/Log Hello World")]
    static void PrintHelloWorldFromPython()
    {
        PythonRunner.RunString(@"
                import UnityEngine;
                UnityEngine.Debug.Log('Hello World!')
                ");
    }
}
```

### Test

From the main menu of the Editor, select **MyPythonScripts** > **Log Hello World**.

The text `Hello World!` should appear in the Unity Console.

## Additional references

* [Call Python script files from C#](csharp-run-file.md)
* [Call Python APIs from C#](csharp-advanced.md)
