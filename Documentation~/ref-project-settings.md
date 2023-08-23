# Python Scripting Project Settings

Use the Python Scripting project settings to:
* Know the Python and Python for .NET versions used by the current Python Scripting package version.
* Extend Python abilities in your Unity project by [installing extra Python modules and packages](install-python-modules.md).

To open the Python Scripting Project Settings, from the Unity Editor main menu, select **Edit** > **Project Settings**, then select **Python Scripting**.

![Python Scripting Settings](images/project-settings.png)

## Versions

| **Property** | **Description** |
|:---|:---|
| **Package** | The version of Unity's Python Scripting package currently installed. |
| **Python** | The version of the Python library currently installed and used by Unity's Python Scripting package. You can't change it. |
| **Python for .NET** | The version of the Python for .NET (pythonnet) library currently installed and used by Unity's Python Scripting package. You can't change it.<br /><br />**Note:** This version of the pythonnet library is a custom build from a fork owned by Unity. |

## Site Packages

| **Property** | **Description** |
|:---|:---|
| **Package Directories** | Use this list and its controls to add paths to folders that contain your own Python modules and packages to [extend Python abilities in your Unity project](install-python-modules.md#access-your-own-python-modules-and-packages). |
| **Apply** | Makes the Unity Editor take into account any change you made in the list. |
| **Revert** | Reverts any changes that you didn't apply yet. |

## Terminal

| **Property** | **Description** |
|:---|:---|
| **Launch Terminal** | Opens a Terminal set up with the Python executable provided with the Python Scripting package.<br><br>You can use it to [install Python modules and packages with pip](install-python-modules.md#manually-install-a-python-package-with-pip) for example. |

>[!NOTE]
>* In your Unity project, the Python executable is in `Library/PythonInstall/python.exe`.
>* You can't use a virtual environment manager like `venv` or `conda`.
