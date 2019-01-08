"""
A simple client doesn't do much.
"""
from __future__ import division, print_function
import os
import sys
import time
import inspect
from unity_python.client import unity_client

client_name = 'empty_client'


class DummyClient(unity_client.UnityClientService):
    def __init__(self, *args, **kwargs):
        super(DummyClient, self).__init__(*args, **kwargs)

if __name__ == '__main__':
    try:
        s = DummyClient()
        c = unity_client.connect(s)
        time.sleep(2)
        c.root.execute(inspect.cleandoc(
            """
            import clr
            clr.AddReference('RpycTests')
            from rpycTests import rpycUnitTests
            rpycUnitTests.ClientContactFlag = True
            """))
        c.thread.join()
        c = None
    except KeyboardInterrupt:
        pass
