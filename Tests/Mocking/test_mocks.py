import UnityEngine
import time

class MockClient(object):
    """
    Mock client that actually runs locally.
    Services should not have the `exposed_` prefix
    """
    def __init__(self):
        super(MockClient, self).__init__()
        self.message = "I'm a bogus service"
        self.shutdown_status = None

    def mock_service(self):
        UnityEngine.Debug.Log(self.message)

    def on_server_shutdown(self, should_reconnect):
        self.shutdown_status = should_reconnect

class MockConnection(object):
    """
    Connection held by the ClientHolder
    """
    def __init__(self):
        super(MockConnection, self).__init__()
        self.root = MockClient()

class MockAsyncRequest(object):
    def __init__(self):
        super(MockAsyncRequest, self).__init__()
        self.value = None # This object has no value

    def set_expiry(self, timeout):
        pass

    def wait(self):
        pass

class MockClientHolder(object):
    """
    Representation of a client on the server
    """
    def __init__(self):
        super(MockClientHolder, self).__init__()
        self._conn = MockConnection()

    def async_shutdown(self, is_rebooting):
        self._conn.root.on_server_shutdown(is_rebooting)
        return MockAsyncRequest()
    
    def wait_for_thread(self):
        pass

# required by RPyC internals
class MockChannel(object):
    def __init__(self):
        super(MockChannel, self).__init__()
    
    def close(self):
        pass

    def fileno(self):
        return 0

    def send(self,something):
        # https://youtu.be/_wk-jT9rn-8?t=3
        pass

    def recv(self):
        return True

    def poll(self):
        return ""

def mock_function():
    UnityEngine.Debug.Log("In the mock function")

def mock_busy_function():
    time.sleep(.1)
