###
# Derived minimal example of the EvalExec client for test purposes.
# The use case for this is a client using it for something like a live link but
# also wants to add a custom service on top of it.
###
from __future__ import division, print_function
from unity_python.client import unity_client
from unity_python.client import evalexec_client
from unity_python.server.utils import log as async_log
import os
import socket
import sys
import threading
import time

client_name = "com.unity.scripting.python.clients.evalexec.derived"
try_to_connect = True

class DerivedEvalExecClientService(evalexec_client.EvalExecClientService):

    def __init__(self):
        super(DerivedEvalExecClientService, self).__init__()

    def exposed_client_name(self):
        return client_name

    def exposed_special_service(self):
        return "I'm a special service but all I got is this lousy log"

    def exposed_on_server_shutdown(self, invite_retry):
        if invite_retry:
            for i in range(120):
                time.sleep(1)
                c = None
                try:
                    # Try to reconnect...
                    c = unity_client.connect(self)
                except socket.error:
                    pass
                except EOFError:
                    sys.exit("Unity has quit or the server closed unexpectedly.")
                except Exception as e:
                    print(e)
                    raise
                else:
                    print("reconnected..")
                    break
        else:
            try_to_connect = False
            super(DerivedEvalExecClientService, self).exposed_on_server_shutdown(invite_retry)

if __name__ == '__main__':
    c = unity_client.connect(DerivedEvalExecClientService)
    while try_to_connect:
        time.sleep(1)
