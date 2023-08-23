# Run Python script files from C#

Use the `PythonRunner.RunFile` method to execute a Python script file from C#.

## Example: ensure GameObject names in the Scene

### Python script

The following Python script loops over all the GameObjects in a Scene and makes sure all their names end up with an underscore.

```
import UnityEngine

all_objects = UnityEngine.Object.FindObjectsOfType(UnityEngine.GameObject)
for go in all_objects:
    if go.name[-1] != '_':
        go.name = go.name + '_'
```

Save this code as `Assets/ensure_naming.py`.

### C# code

The following C# code creates a new menu item that calls the Python script file you just created.

```
using UnityEditor.Scripting.Python;
using UnityEditor;
using UnityEngine;

public class EnsureNaming
{
    [MenuItem("MyPythonScripts/Ensure Naming")]
    static void RunEnsureNaming()
    {
        PythonRunner.RunFile($"{Application.dataPath}/ensure_naming.py");
    }
}
```

### Test

From the main menu of the Editor, select **MyPythonScripts** > **Ensure Naming**.

All the GameObjects in the Hierarchy should now be suffixed with an underscore.

## Additional references

* [Call Python instructions from C#](csharp-run-string.md)
* [Call Python APIs from C#](csharp-advanced.md)
