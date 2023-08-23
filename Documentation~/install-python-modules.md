# Install Python modules and packages

Extend Python abilities in your Unity project with additional Python modules and packages:
* [Access your own Python modules and packages](#access-your-own-python-modules-and-packages).
* [Manually install extra Python packages with pip](#manually-install-a-python-package-with-pip).
* [Automatically install extra Python packages via a `requirements.txt` file](#automate-python-packages-installation).

## Access your own Python modules and packages

To make your own Python modules and packages accessible from the Unity Editor:

1. From the Unity Editor main menu, select **Edit** > **Project Settings**, then select **[Python Scripting](ref-project-settings.md)**.

2. In the **Package Directories** list, select the **+** (plus) button to add a new entry.

   ![Python Scripting Site Packages Settings](images/project-settings-site-packages.png)

3. In the new entry, set the path to the folder containing your Python modules or packages.

4. Select **Apply**.

Unity adds this list of paths to the `sys.path` variable and makes your own Python modules discoverable from the Unity Editor. You can then import them in a script, including via the [Python Script Editor](ref-script-editor.md).

>[!NOTE]
* If you modify or remove an existing path in this list, you need to restart the Unity Editor for it to take effect.
* Paths are treated as verbatim strings. You need to expand environment variables or use the `~` (on macOS and Linux) denoting the home directory.

## Manually install a Python package with pip

To manually install a Python package with pip through the terminal:

1. From the Unity Editor main menu, select **Edit** > **Project Settings**, then select **[Python Scripting](ref-project-settings.md)**.

2. Select **Launch Terminal**.

3. Use pip to install the desired Python package.  
   For example: `python -m pip install PySide2`.

Unity installs the Python package in the following folder of your Unity project: `Library/PythonInstall/Lib/site-packages`.

## Automate Python packages installation

Use this method for example when you need to share your Unity project with other co-workers and make sure everyone is using the same set of Python modules and packages with compatible versions.

To make the Unity Editor automatically install a predefined set of Python packages at start-up:

1. In your Unity project, in the `ProjectSettings` folder, create a new `requirements.txt` file.

2. List the desired Python packages in this new file as per the [pip requirement file](https://pip.pypa.io/en/stable/user_guide/#requirements-files) format.

3. Open (or restart) your Unity project to apply any changes to the `requirements.txt` file.

>[!WARNING]
>If you're using a `requirements.txt` file, be aware that Python packages installed individually from the terminal will be uninstalled the next time you open your Project unless they are also listed in the `requirements.txt` file.
