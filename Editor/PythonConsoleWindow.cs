#if UNITY_2019_1_OR_NEWER

using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.Scripting.Python
{
    [System.Serializable]
    public class PythonConsoleWindow : EditorWindow
    {
#region Window

        // Menu item which calls the window.
        [MenuItem("Window/General/Python Console")]
        public static void ShowWindow()
        {
            s_window = GetWindow<PythonConsoleWindow>();
            s_window.titleContent = new GUIContent("Python Script Editor");

            // Handle window sizing.
            s_window.minSize = new Vector2(550, 300);
        }

        public void OnEnable()
        {
            // Creation and assembly of the window.

            var root = rootVisualElement;

            // Construct toolbar. // Currently not handled by uxml (2019.1.1f1).
            var toolbar = new Toolbar();
            root.Add(toolbar);
            var tbButtonLoad = new ToolbarButton { text = "Load" };
            toolbar.Add(tbButtonLoad);
            var tbButtonSave = new ToolbarButton { text = "Save" };
            toolbar.Add(tbButtonSave);
            var tbButtonSaveMenu = new ToolbarButton { text = "Save & Create Shortcut" };
            toolbar.Add(tbButtonSaveMenu);
            var tbSpacer = new ToolbarSpacer();
            toolbar.Add(tbSpacer);
            var tbButtonRun = new ToolbarButton { text = "Execute" };
            toolbar.Add(tbButtonRun);
            var tbSpacer2 = new ToolbarSpacer();
            toolbar.Add(tbSpacer2);
            var tbButtonClearCode = new ToolbarButton { text = "Clear Code" };
            toolbar.Add(tbButtonClearCode);
            var tbButtonClearOutput = new ToolbarButton { text = "Clear Output" };
            toolbar.Add(tbButtonClearOutput);
            var tbButtonClearAll = new ToolbarButton { text = "Clear All" };
            toolbar.Add(tbButtonClearAll);


            // Assemble and construct visual tree.
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.unity.scripting.python/Styles/pythonconsole_uxml.uxml");
            visualTree.CloneTree(root);
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.scripting.python/Styles/pythonconsole_uss.uss"));


            // Fetch references to UXML objects (UQuery).
            m_textFieldCode = root.Query<TextField>("textAreaCode").First();
            m_textFieldOutput = root.Query<TextField>("textAreaOutput").First();

            m_holderofOutputTextField = root.Query<ScrollView>("holderOutput").First();

            m_scrollerOutput = root.Query<Scroller>(className: "unity-scroller--vertical").First();
            m_scrollerCode = root.Query<Scroller>(className: "unity-scroller--vertical").Last();

            // Implement event handlers.
            m_textFieldCode.RegisterCallback<ChangeEvent<string>>(OnCodeInput);
            m_textFieldCode.Q(TextField.textInputUssName).RegisterCallback<KeyDownEvent>(OnExecute);

            tbButtonLoad.RegisterCallback<MouseUpEvent>(OnLoad);
            tbButtonSave.RegisterCallback<MouseUpEvent>(OnSave);
            tbButtonSaveMenu.RegisterCallback<MouseUpEvent>(OnSaveShortcut);

            tbButtonRun.RegisterCallback<MouseUpEvent>(OnExecute);

            tbButtonClearCode.RegisterCallback<MouseUpEvent>(OnClearCode);
            tbButtonClearOutput.RegisterCallback<MouseUpEvent>(OnClearOutput);
            tbButtonClearAll.RegisterCallback<MouseUpEvent>(OnClearAll);


            // Handle reserialization.
            m_textFieldCode.value = m_codeContents;
            m_textFieldOutput.value = m_outputContents;
        }
#endregion


#region Class Variables

        // Keep a reference on the Window for two reasons:
        // 1. Better performance
        // 2. AddToOutput is called from thread other than the main thread
        //    and it triggers a name seasrch, which can only be done in the 
        //    main thread
        static PythonConsoleWindow s_window = null;

        TextField m_textFieldCode;
        TextField m_textFieldOutput;
        ScrollView m_holderofOutputTextField;

        [SerializeField]
        string m_codeContents;
        [SerializeField]
        string m_outputContents;

        // Sizing utility variables
        int m_borderBuffer_WindowBottom = 50;
        int m_borderBuffer_SplitHandle = 2;

        Scroller m_scrollerOutput;
        Scroller m_scrollerCode;
#endregion


        #region Event Functions

        // Text is inputed into the Code text area.
        void OnCodeInput(ChangeEvent<string> e)
        {
            m_codeContents = m_textFieldCode.value;
        }

        // Were the right key pressed? This variable is used by the subsequent code to keep track of the two events generated on key press.
        bool m_wereActionEnterPressed;
        // Key(s) are pressed while the Code area is in focus. 
        void OnExecute(KeyDownEvent e)
        {
            // Verify that the Action (Control/Command) and Return (Enter/KeypadEnter) keys were pressed, or that the KeypadEnter was pressed.
            // This 'catches' the first event. This event carries the keyCode(s), but no character information.
            // Here we execute the Python code. The textField itself is left untouched.
            if (e.keyCode == KeyCode.KeypadEnter || (e.actionKey == true && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)) )
            {
                m_wereActionEnterPressed = true;
                if (!string.IsNullOrEmpty(GetSelectedCode()))
                {
                    PartialExecute();
                }
                else
                {
                    ExecuteAll();
                    e.PreventDefault();
                }
            }

            // If the right keys were pressed, prevent the KeyDownEvent's default behaviour.
            // This 'catches' the second event. It carries the character information (in this case, "\n"), but has a keyCode of None.
            // Since it is responsible for writing into the textField, we here prevent its default proceedings.
            if (e.keyCode == KeyCode.None && m_wereActionEnterPressed == true)
            {
                e.PreventDefault();
                m_wereActionEnterPressed = false;
            }
        }

        // 'Execute' is pressed.
        void OnExecute(MouseUpEvent e)
        {
            ExecuteAll();
        }

        // 'Load' is pressed.
        void OnLoad(MouseUpEvent e)
        {
            // Let the user select a file.
            var fullFilePath = EditorUtility.OpenFilePanel("Load Python Script", "", "py");
                
            // Verify that the resulting file string is not empty (in case the user hit 'cancel'). If it is, return.
            if (string.IsNullOrEmpty(fullFilePath))
            {
                return;
            }

            // Once a file has been chosen, clear the console's current content.
            OnClearCode(e);
                
            // Read and copy the file's content into the m_codeContents variable.
            m_codeContents = System.IO.File.ReadAllText(fullFilePath);

            // Fill code text area.
            m_textFieldCode.value = m_codeContents;
        }

        // 'Save' is pressed.
        void OnSave(MouseUpEvent e)
        {
            // Let the user select a save path.
            var savePath = EditorUtility.SaveFilePanelInProject("Save Current Script", "new_python_script", "py", "Save location of the current script.");

            //Make sure it is valid (the user did not cancel the save menu).
            if (!string.IsNullOrEmpty(savePath))
            {
                // Write current console contents to file.
                System.IO.File.WriteAllText(savePath, GetCode());
            }
        }

        // "Save & Create Shortcut' is pressed.
        void OnSaveShortcut(MouseUpEvent e)
        {
            CreateMenuItemWindow.ShowWindow(GetCode());
        }

        // 'Clear Code' is pressed.
        void OnClearCode(MouseUpEvent e)
        {
            // Set the current content variable to null.
            m_codeContents = "";
            // Update the textfield's value.
            m_textFieldCode.value = m_codeContents;
        }

        // 'Clear Output' is pressed.
        void OnClearOutput(MouseUpEvent e)
        {
            // Set the current content variable to null.
            m_outputContents = "";
            // Update the textfield's value.
            m_textFieldOutput.value = m_outputContents;
        }

        // 'Clear All' is pressed.
        void OnClearAll(MouseUpEvent e)
        {
            OnClearCode(e);
            OnClearOutput(e);
        }
#endregion


#region Utility Functions
        
        // Fetch and return the current console content as a string.
        string GetCode()
        {
            m_codeContents = m_textFieldCode.value;
            return m_codeContents;
        }

        // Fetch and return the current code selection as a string.
        string GetSelectedCode()
        {
            // The current text selection of a TextField is not available through the public API in Unity 2019.1.
            // We can optain it through its TextEditor, which itself must be accessed by reflection.
            var textEditorProperty = m_textFieldCode.GetType().GetProperty("editorEngine", BindingFlags.Instance | BindingFlags.NonPublic);
            var textEditor = textEditorProperty.GetValue(m_textFieldCode) as TextEditor;

            string selectedText = textEditor.SelectedText;
            return selectedText;
        }

        // Set the output field's displayed content to the associated variable.
        void SetOutputField()
        {
            m_textFieldOutput.value = m_outputContents;
        }

        // Add the inputed string to the output content.
        static public void AddToOutput(string input)
        {
            if(s_window)
            {
                s_window.InternalAddToOutput(input);
            }
        }

        void InternalAddToOutput(string input)
        {
            if (!string.IsNullOrEmpty(input))
            {
                m_outputContents += input;
                SetOutputField();
            }
        }

        void Execute (string code)
        {
            PythonRunner.RunString(code);
        }

        // Execute only the current selection.
        void PartialExecute()
        {
            Execute(GetSelectedCode());
        }

        void ExecuteAll ()
        {
            Execute(m_codeContents);
        }
       
#endregion

#region Update Functions

        // For code review: should this use the simple Update() ? Either of them don't seem to make a difference in how quickly the textField is redrawn.
        private void OnGUI()
        {
            // Handle current size of the Code text field.
            var textFieldCode_CurrentSize = rootVisualElement.contentRect.height - m_holderofOutputTextField.contentRect.height - m_borderBuffer_WindowBottom;
            m_textFieldCode.style.minHeight = textFieldCode_CurrentSize;

            // Handle current minimum size of the Output text field.
            var textFieldOutput_CurrentSize = m_holderofOutputTextField.contentRect.height - m_borderBuffer_SplitHandle;
            m_textFieldOutput.style.minHeight = textFieldOutput_CurrentSize;
            m_holderofOutputTextField.style.minHeight = textFieldOutput_CurrentSize;

            // Ajust vertical scroller sizes.
            // This is done pretty roughly atm. Need to optimize.
            m_scrollerOutput.style.bottom = 7;
            m_scrollerCode.style.bottom = 7;
        }
#endregion
    }

    // This is the pop up used in the creation of menu shortcuts upon script saving.
    [System.Serializable]
    public class CreateMenuItemWindow : EditorWindow, IDisposable
    {
        #region Class Variables
        TextField m_textfieldMenuName;
        IMGUIContainer m_helpboxContainer;
        Button m_buttonCommitMenuName;

        private bool m_isMenuNameValid = true;
        private string m_codeToSave;
        #endregion

        #region Window
        public static void ShowWindow(string codeToSave)
        {
            CreateMenuItemWindow window = CreateInstance(typeof(CreateMenuItemWindow)) as CreateMenuItemWindow;
            window.titleContent.text = "Create Menu Shortcut";
            window.ShowUtility();

            // Handle window sizing.
            window.minSize = new Vector2(540, 86);
            window.maxSize = new Vector2(1000, 86);

            // Local storage of the code to save.
            window.m_codeToSave = codeToSave;
        }

        public void OnEnable()
        {
            // Creation and assembly of the window.
            var root = rootVisualElement;

            // Construct its contents.
            m_textfieldMenuName = new TextField { label = "Submenu Name: ",
                                                value = "Python Scripts/New Python Script" };
            root.Add(m_textfieldMenuName);
            m_helpboxContainer = (new IMGUIContainer(OnValidation));
            root.Add(m_helpboxContainer);
            m_buttonCommitMenuName = new Button { text = "Create" };
            root.Add(m_buttonCommitMenuName);

            // Assign style sheet.
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.scripting.python/Styles/pythonpopup_uss.uss"));

            // Implement event handles.
            m_textfieldMenuName.RegisterCallback<ChangeEvent<string>>(OnPathEdit);
            m_buttonCommitMenuName.RegisterCallback<MouseUpEvent>(OnCommit);
        }

        // Needed to display a help box corresponding to those used in the default Unity UI. 
        // Additionally handles whether the button should be active.
        private void OnValidation()
        {
            if (!m_isMenuNameValid)
            {
                EditorGUILayout.HelpBox("The current menu input is not valid.", MessageType.Error);
                m_buttonCommitMenuName.SetEnabled(false);
            }
            else
            {
                EditorGUILayout.HelpBox("Note: the menu must be at least 2 levels deep. The option itself must have a unique name.", MessageType.Info);
                m_buttonCommitMenuName.SetEnabled(true);
            }
        }
        void OnInspectorUpdate()
        {
            Repaint();
        }

        #endregion

        #region Utility Functions
        void OnPathEdit(ChangeEvent<string> e)
        {
            m_textfieldMenuName.value = m_textfieldMenuName.value.Replace("\\", "/");
            m_textfieldMenuName.value = m_textfieldMenuName.value.Replace("//", "/");

            m_isMenuNameValid = ValidateMenuName();
        }

        void OnCommit(MouseUpEvent e)
        {
            if (ValidateMenuName())
            {
                // Let the user select a save path.
                string pySavePath = EditorUtility.SaveFilePanelInProject("Save Current Script & Create Menu Shortcut", "new_python_script", "py", "Save location of the current script, as well as that of its menu shortcut.");

                //Make sure it is valid (the user did not cancel the save menu).
                if (!string.IsNullOrEmpty(pySavePath))
                {
                    // Write current console contents to file.
                    System.IO.File.WriteAllText(pySavePath, m_codeToSave);

                    // Create the associated menu item's script file.
                    WriteShortcutFile(pySavePath, m_textfieldMenuName.value);
                }
                else
                    this.Close();
            }
        }

        bool ValidateMenuName()
        {
            string value = m_textfieldMenuName.value;
            string[] namedLevels = m_textfieldMenuName.value.Split('/');

            // Verify that the menu name/path contains at least one sublevel, and does not begin nor end with a slash.
            if (!value.Contains("/")) { return false; }
            if (value[0] == '/') { return false; }
            if (value[value.Length - 1] == '/') { return false; }

            // Verify that each level has an adequate name.
            foreach (string subName in namedLevels)
            {
                string cleanSubName = Regex.Replace(subName, "[^A-Za-z0-9]", "");
                if (string.IsNullOrEmpty(cleanSubName)) { return false; }
            }

            // Verify that the corresponding script name would not be null.
            if (string.IsNullOrEmpty(GetScriptName(value))) { return false; }

            return true;
        }

        // Handle script name.
        string GetScriptName(string menuName)
        {
            int i = menuName.LastIndexOf('/') + 1;
            string scriptName = menuName.Substring(i);
            scriptName = Regex.Replace(scriptName, @"[\W]", "");

            return scriptName;
        }

        // Rewrite the template menu shortcut script as needed.
        void WriteShortcutFile(string pySavePath, string menuName)
        {
            // Convert m_savePath file from Python to C#.
            string csSavePath = pySavePath.Replace(".py", ".cs");

            // Get scriptName from menuName.
            string scriptName = GetScriptName(menuName);

            // Create className from scriptName.
            string className = $"MenuItem_{scriptName}_Class";

            string scriptContents = "using UnityEditor;\n"
                                  + "using UnityEditor.Scripting.Python;\n"
                                  + "\npublic class " + className + "\n"
                                  + "{\n"
                                  + "   [MenuItem(\"" + menuName + "\")]\n"
                                  + "   public static void " + scriptName + "()\n"
                                  + "   {\n"
                                  + "       PythonRunner.RunFile(\"" + pySavePath + "\");\n"
                                  + "       }\n"
                                  + "};\n";

            try
            {
                // Write the resulting .cs file in the same location as the saved Python script.
                System.IO.File.WriteAllText(csSavePath, scriptContents);

                // Reset and close the popup window.
                m_textfieldMenuName.value = "Python Scripts/New Python Script";
                this.Close();

                AssetDatabase.Refresh();
            }

            catch (Exception e)
            {
                Debug.LogError("Failure to create the menu item for this Python script.\n" + e.Message);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                m_helpboxContainer.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
#endif