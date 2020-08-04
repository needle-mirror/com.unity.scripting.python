from __future__ import division, print_function

import sys
import os
import threading

# _thread was called thread in Python 2.7
# threading.get_ident was called thread.get_ident in Python 2.7
if sys.version_info.major < 3:
    import thread as _thread
    threading.get_ident = _thread.get_ident
else:
    import _thread

# When testing it's easy to end up with us not having our package root set up.
try:
    import unity_python
except ImportError:
    path = os.path.normpath(os.path.dirname(os.path.abspath(__file__)) + "/../..")
    sys.path = [path] + sys.path

# Imports from our package; do this after fixing the path.
import rpyc
import unity_python.server.settings as settings

class UnityClientService(rpyc.Service):
    """
    The client service provides the absolute minimum needed for
    new clients to Unity.

    Any client for Unity must provide the following services:
    * client_name
    * on_server_shutdown(bool)

    If you want Unity to be able to access a service called `my_service`,
    create a function called `exposed_my_service`.
    """
    __slots__ = ['_unity_side', '_modules']

    def on_connect(self, conn):
        self._unity_side = conn.root
        self._modules = {}

    def on_disconnect(self, conn):
        self._unity_side = None
        self._modules = {}

    def import_module(self, name):
        if name not in self._modules:
            self._modules[name] = self._unity_side.import_module(name)
        return self._modules[name]

    @property
    def UnityEngine(self):
        return self.import_module('UnityEngine')

    @property
    def UnityEditor(self):
        return self.import_module('UnityEditor')

    def exposed_client_name(self):
        """
        Called when we connect to Unity.

        Return a name (a string) to identify this client.

        This is required in order for C# scripts in Unity to call this client.
        The name should be unique; otherwise only the first client to connect
        to Unity will be reachable.

        It's also useful for debugging even if C# scripts never need to call
        this client.
        """
        return 'Unity Client'

    def exposed_on_server_shutdown(self, invite_retry):
        """
        Called when Unity shuts down the server.

        With `invite_retry` True, Unity is doing a domain reload and will be back shortly.

        With `invite_retry` False, Unity is shutting down for good and we probably want to
        also shut down.

        By default, `on_server_shutdown` sends a KeyboardInterrupt to the main
        thread and exits the current thread. Override to do something more
        interesting.
        """
        if invite_retry:
            reason = "domain reload"
        else:
            reason = "shutdown"
        print("server shutting down for {}".format(reason))
        _thread.interrupt_main()
        sys.exit(0)

def connect(Service = UnityClientService):
    """
    Create a thread that connects to Unity.

    Return the connection.
    """
    config = { 'propagate_SystemExit_locally': True, 'propagate_KeyboardInterrupt_locally': True }
    connection = rpyc.utils.factory.unix_connect(settings.unity_server_path, Service, config = config)
    connection_thread = threading.Thread(target = connection.serve_all, name = "Connection to Unity")
    connection_thread.start()
    connection.thread = connection_thread
    return connection
