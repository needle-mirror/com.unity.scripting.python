# Using the In-Process API

The In-Process API is meant for scripts that are stateless, which means that
they do not keep information between runs. This is because Unity often reloads
assemblies (during domain reload), and the Python for Unity assembly will
re-initialize the Python interpreter when its assembly gets reloaded. This means
anything that is stored in the Python interpreter will eventually get destroyed
by the garbage collector.

Examples of scripts that are good candidates for the In-Process API are:
- Scripts processing scene data like creating, deleting or modifying assets
- Scripts using external scene description (EDL, json) in order to assemble a
scene
- Validation scripts before pushing to production
- ...

Scripts that need to keep information, like scripts providing UI elements built
with SDKs like PySide should use the [Out-of-Process API](outOfProcessAPI.md)
since these scripts need to survive assembly reloads.

## Python Script Editor

The [Python Script Editor](PythonScriptEditor.md) makes it easy to test
in-process Python scripts and to create a menu item to allow users to easily
access the script.

## PythonRunner.RunString
The following C# code will create a menu item that prints "hello world" in the
Unity console:

```
using UnityEditor.Scripting.Python;
using UnityEditor;

public class HelloWorld
{
    [MenuItem("Python/Hello World")]
    static void PrintHelloWorldFromPython()
    {
        PythonRunner.RunString(@"
import UnityEngine;
UnityEngine.Debug.Log('hello world')
");
    }
}
```

You can use any assembly that is available in C# by simply importing it with the
Python `import` statement.

## PythonRunner.RunFile
Instead of inlining your Python code inside of a C# script, you can execute a
whole Python script using the `PythonRunner.RunFile` method. For example, this
Python script loops over all the GameObjects in a scene and makes sure all the
names end up with an underscore:

```
import UnityEngine

all_objects = UnityEngine.Object.FindObjectsOfType(UnityEngine.GameObject)
for go in all_objects:
    if go.name[-1] != '_':
        go.name = go.name + '_'
```

Script files can be located anywhere on your computer, and in this example we
chose to put it under `Assets/ensure_naming.py`. You can run
this Python script from C# the following way:

```
using UnityEditor.Scripting.Python;
using UnityEditor;
using UnityEngine;
using System.IO;

public class EnsureNaming
{
    [MenuItem("Python/Ensure Naming")]
    static void RunEnsureNaming()
    {
        string scriptPath = Path.Combine(Application.dataPath,"ensure_naming.py");
        PythonRunner.RunFile(scriptPath);
    }
}
```

## Directly using Python for .NET

You can directly use Python for .NET, which wraps the CPython API. Using the C#
`dynamic` type, you can write C# that looks like Python. Refer to the [Python
for .NET documentation](https://pythonnet.github.io/).

There are some caveats:
* Make sure Python is properly initialized by calling `PythonRunner.EnsureInProcessInitialized()`.
* Always grab the CPython [Global Interpreter Lock (GIL)](https://docs.python.org/2/glossary.html#term-global-interpreter-lock).

Sample code:
```
using Python.Runtime;
using UnityEditor.Scripting.Python;

public class MyPythonScript
{
    [MenuItem("MyPythonScript/Run")]
    public static void Run()
    {
        PythonRunner.EnsureInProcessInitialized();
        using (Py.GIL()) {
            try {
                dynamic sys = PythonEngine.ImportModule("sys");
                UnityEngine.Debug.Log($"python version: {sys.version}");
            } catch(PythonException e) {
                UnityEngine.Debug.LogException(e);
            }
        }
    }
}
```

## Limitations

* Some important packages implemented in native code are not usable with the
  in-process API because they do not finalize and re-initialize cleanly. After
  a domain reload, they may be unusable or even cause a crash. Examples include
  `numpy` and `PySide`. If you need these packages, use the
  [Out-of-Process API](outOfProcessAPI.md).
* On macOS, if you add a new environment variable after Python initializes,
  Unity will likely crash on the next domain reload. This is due to a [bug
  in Python](https://bugs.python.org/issue37931).
* In the current version of Python for Unity, threads that call into python --
  including threads spawned by Python's threading module -- do not run as often
  as might be expected. This is because the main thread holds the GIL too
  often. The issue will be remediated in a future version.
