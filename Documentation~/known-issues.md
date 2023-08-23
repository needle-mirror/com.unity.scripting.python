
# Known issues and limitations

#### Editor-only feature not for runtime builds

The Python Scripting package is a feature designed to work only in the Unity Editor. It isn't available in runtime builds.

#### Script editor window output limitation

The Python Script Editor output section (top section) has a limit of 10,000 characters.

#### Python for .NET with Unity limitations

When you access the Unity API in Python it is not actual bindings of our C# API. This is possible thanks to Python for .NET that allows you to call the functionnalities of any loaded C# assemblies in Python dirrectly. This can sometimes cause unexpected issues due to Python for .NET limitations regarding Unity's usage of C#.

For example it is currently recommended to use list comprehension to convert Unity C# list-type data structure to python list instead of a simple cast:
* `myPythonList = list(Selection.activeGameObjects)`: this can cause Unity to close unexpectedly
* `myPythonList = [gameObject for gameObject in Selection.activeGameObjects]`: this is OK

#### Project settings limitations

* You cannot change the internal Python version
* You cannot use a virtual environment manager like `venv` or `conda`.
* Paths are treated as verbatim strings. You need to expand environment variables or use the `~` (on macOS and linux) denoting the home directory.

#### PySide and Apple Silicon
PySide2 Python package is not compatible with Apple Silicon. This affects the sample "PySide Camera Selector" that is currently not usable on any Apple computer with an Apple Silicon processor.

**Workaround**:
You can use PySide6, PyQt5, or PyQt6 Python package instead of PySide2.
However, this might require small changes in the sample to make it work.
