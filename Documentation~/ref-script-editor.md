# Python Script Editor

Use the Python Script Editor to [start scripting](get-started.md) in Python in Unity.

To open the Python Script Editor window, from the main menu of the Editor, select **Window** > **General** > **Python Script Editor**.

![Using the Python Script Editor](images/python-console-example.png)

## Window layout

|  | **Name** | **Description** |
|:---|:---|:---|
| **A** | Input area | Text area to write your Python script in. |
| **B** | Output area | Read-only text area displaying the output of your script execution.<br />This area has a limit of 10,000 characters. |
| **C** | Action menu bar | A set of [action buttons](#action-buttons) to manage the content of the input and output text areas. |

## Action buttons

| **Button** | **Description** |
|:---|:---|
| Load | Loads an existing Python script from disk.<br />The script appears in the input area (A). |
| Save | Saves the script from the input area (A) to a Python script file. |
| Save & Create Shortcut | Saves the script from the input area (A) to a Python script file and adds a menu item to run the saved script file from the Unity Editor main menu. |
| Execute | Executes the script from the input area (A) and outputs any `print` statements in the output area (B), as well as in the [Editor.log](https://docs.unity3d.com/Manual/LogFiles.html).<br />If the script execution generates errors, they appear in the Unity Console.<br /><br />**Tip:** To execute only a portion of code within the script, select the targeted lines in the input area (A) and press Ctrl/Cmd+Enter on your keyboard. |
| Clear Code | Clears the input area (A).<br />You can't undo this action. |
| Clear Output | Clears the output area (B).<br />You can't undo this action. |
| Clear All | Clears both the input area (A) and output area (B).<br />You can't undo this action. |

## Additional resources

To implement more complex Python-based tools in Unity, use the API to [invoke Python code from C#](python-from-csharp.md) that runs in the Unity process.
