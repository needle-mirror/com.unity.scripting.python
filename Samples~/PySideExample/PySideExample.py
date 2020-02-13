"""
This is an example of using the Python for Unity out-of-process API to create a
PySide window that has a live link with Unity.

This example shows the available cameras in the scene and updates the list
automatically as new cameras are added.

When the user selects a camera in the PySide view and clicks "Use Camera",
Unity switches to using that camera in the Scene View.

The user can stat the client in two ways:
1. Within Unity, via the Python/Samples/PySide Example menu.
2. From the command-line, by running this script.
"""

# Best practice: Import unity_client first, because it sets some settings
# appropriately. That allows running from the command-line.
import unity_python.client.unity_client as unity_client

try:
    from PySide2 import QtCore, QtUiTools, QtWidgets
    print("Example running with PySide2.")
except Exception as e:
    try: #Else, is PySide available?
        from PySide import QtCore, QtGui, QtUiTools
        import PySide.QtGui as QtWidgets
        print("Example running with PySide.")
    except Exception as e:
        print("No PySide found.")

import inspect
import logging
import os
import sys
import time
import traceback
import socket

### Globals
# The UI class
_PYSIDE_UI = None

### UI class
class PySideTestUI():
    def __init__(self, connection, service):
        # Initialize the application and the dialog
        self._qApp   = None
        self._dialog = None
        self.service = service
        self.connection = connection

        try:
            # Create the application if required
            self._qApp = QtWidgets.QApplication.instance()
            if not self._qApp:
                self._qApp = QtWidgets.QApplication(sys.argv)
            # Create the dialog from our .ui file
            ui_path = os.path.join(os.path.dirname(__file__), 'PySideExample.ui')
            self._dialog = self.load_ui_widget(ui_path.replace("\\", "/"))

            # Initialisation that must be done on each new connection
            self.setup(connection)

            # Show the dialog
            self._dialog.show()

        except Exception as e:
            self.log('Got an exception while creating the dialog.',logging.ERROR, traceback.format_exc())
            raise e

    def setup(self, connection):
        """
        Connection-dependent initialization. Must be called each time the
        connection changes/is reset
        """

        self.connection = connection
        
        # Populate the camera list
        self.populate_camera_list()

        # Set up the delegate on Unity's side so we get called when the hierarchy changes
        # See PySideExample.cs
        self.connection.root.execute(inspect.cleandoc(
            """
            import clr
            clr.AddReference('PythonExample')
            from PythonExample import PySideExample
            PySideExample.Subscribe()
            """))

    def populate_camera_list(self):
        """
        Populates the list of cameras by asking Unity for all the cameras.

        This sends a request to Unity, so it can be slow.
        """
        # Create the dictionnary that will hold the variables on the remote execution
        vars = self.connection.root.dict()
        # Execute the statement
        self.connection.root.execute(inspect.cleandoc(
            """
            import UnityEngine
            cameras = [x.name for x in UnityEngine.Camera.allCameras]
            """), vars)
        # Retrieve the variable
        camera_list = vars["cameras"]

        # Populate the list
        list_widget = self._dialog.listWidget
        list_widget.clear()
        for cam in camera_list:
            list_widget.addItem(cam)

    def use_camera(self):
        if not self._dialog:
            return
        # Get the selected camera name
        selected_items = self._dialog.listWidget.selectedItems()
        if len(selected_items) != 1:
            return

        try:
            # You can conveniently call into the UnityEngine or UnityEditor C# 
            # APIs. Each '.' after the UnityEngine will spark a remote 'getattr' 
            # call; if performance is critical, consider using 'self.connection.root.execute'.
            camera = self.service.UnityEngine.GameObject.Find('{}'.format(selected_items[0].text()))
            # Apply camera selection
            self.select_camera(camera)

            # Since we don't need to send/retrieve variables here, no need to pass a variables dictionary
            self.connection.root.execute(inspect.cleandoc(
                """
                import UnityEditor
                UnityEditor.EditorApplication.ExecuteMenuItem('GameObject/Align View to Selected')
                """))
        except Exception as e:
            self.log('Got an exception trying to use the camera:{}'.format(selected_items[0].text()), logging.ERROR, traceback.format_exc())
            raise e

    def load_ui_widget(self, uifilename, parent=None):
        # Load the UI made in Qt Designer
        # As seen on Stack Overflow: https://stackoverflow.com/a/18293756
        loader = QtUiTools.QUiLoader()
        uifile = QtCore.QFile(uifilename)
        uifile.open(QtCore.QFile.ReadOnly)
        ui = loader.load(uifile, parent)
        uifile.close()

        # Connect the Button's signal
        ui.useCameraButton.clicked.connect(self.use_camera)

        return ui

    def select_camera(self, camera):
        id = camera.GetInstanceID()

        # Executing the camera selection on the server as it is not
        # currently possible to create the C# Array using generics
        # on the client (System module unavailable).
        self.connection.root.execute(inspect.cleandoc(
            """
            import UnityEditor
            import System
            selList = [{id}]
            selection = System.Array[int](selList)
            UnityEditor.Selection.instanceIDs = selection
            """.format(id=id)))

    def log(self, what, level=logging.INFO, traceback=None):
        """
        Short-hand method to log a message in Unity. At logging.DEBUG it prints 
        into the Editor's log file (https://docs.unity3d.com/Manual/LogFiles.html) 
        At level logging.INFO, logging.WARN and logging.ERROR it uses 
        UnityEngine.Debug.Log, UnityEngine.Debug.LogWarning and 
        UnityEngine.Debug.LogError, respectively.
        """
        self.connection.root.log(what, level, traceback)

class PySideTestClientService(unity_client.UnityClientService):
    """
    Custom rpyc service that overrides the default Unity client service.
    Makes it possible to make specific method calls from the server to the client
    """
    def exposed_client_name(self):
        """
        The client_name should be globally unique, so that
        the server can identify clients from different vendors.
        """
        return "PySide Example"

    def exposed_on_hierarchy_changed(self):
        """
        on_hierarchy_changed is a custom callback just for this client.

        See PySideExample.cs for where it's called.

        We rebuild our UI by asking Unity for all the cameras in the scene.

        This function must be called asynchronously from Unity, because we go
        straight back to Unity to ask it for information about the scene. If
        Unity is blocked waiting for this function to exit, and this function
        in turn blocks waiting for Unity to return the list of cameras, we'd be
        in a deadlock.
        """
        _PYSIDE_UI.populate_camera_list()

    def exposed_on_server_shutdown(self, invite_retry):
        global _PYSIDE_UI
        print("disconnecting.. should retry: {}".format(invite_retry))
        # Close the connection before (maybe) re-opening it
        _PYSIDE_UI.connection.close()
        # Is the server coming back online?
        if invite_retry:
            for i in range(120):
                # Wait.. or Unity locks up.
                # TODO: fix the bug in the server
                time.sleep(1)
                print("retrying...")
                c = None
                try:
                    # Try to reconnect...
                    c = unity_client.connect(self)
                    _PYSIDE_UI.setup(c)
                except socket.error:
                    pass
                except EOFError:
                    print("Connection lost. Exiting.")
                    sys.exit("Unity has quit or the server closed unexpectedly.")
                except Exception as e:
                    print(e)
                    raise
                else:
                    print("refreshing..")
                    break
        else:
            QtWidgets.QApplication.quit()
            super(PySideTestClientService, self).exposed_on_server_shutdown(invite_retry)

def main(connection, service):
    global _PYSIDE_UI

    if not _PYSIDE_UI:
        _PYSIDE_UI = PySideTestUI(connection, service)
        QtWidgets.QApplication.exec_()

if __name__ == '__main__':
    s = PySideTestClientService()
    c = unity_client.connect(s)
    main(c, s)
    c.close()
    sys.exit(0)
