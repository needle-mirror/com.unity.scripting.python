"""
A simple client that allows starting and stopping.
"""
from __future__ import division, print_function
import os
import sys
import time
import logging
from unity_python.client import unity_client

client_name = 'com.unity.scripting.python.tests.rpyc'

shutdown_count = 0
should_connect = True

class UnitTestClientService(unity_client.UnityClientService):
    def __init__(self, *args, **kwargs):
        super(UnitTestClientService, self).__init__(*args, **kwargs)
        print("created {}".format(self.exposed_client_name()))
        self.state = ""

    def exposed_client_name(self):
        return client_name

    def exposed_get_argv(self, index):
        print("asked for argv {}".format(index))
        return sys.argv[index]

    def exposed_get_restart_count(self):
        print("asked for restart count: {}".format(shutdown_count))
        return shutdown_count

    def exposed_create_cylinder(self, name):
        print("asked to create a cylinder named '{}'".format(name))
        go = self.UnityEngine.GameObject.CreatePrimitive(self.UnityEngine.PrimitiveType.Cylinder)
        go.name = name
        self.UnityEngine.Debug.Log("created a cylinder named '{}'".format(name))

    def exposed_create_cube(self, name):
        print("asked to create a cube named '{}'".format(name))
        go = self.UnityEngine.GameObject.CreatePrimitive(self.UnityEngine.PrimitiveType.Cube)
        go.name = name
        self.UnityEngine.Debug.Log("created a cube named '{}'".format(name))

    def exposed_on_server_shutdown(self, invite_retry):
        global should_connect, shutdown_count
        if not invite_retry:
            # shut down
            print("got hard shutdown")
            should_connect = False
            super(UnitTestClientService, self).exposed_on_server_shutdown(invite_retry)
        else:
            print("got soft shutdown, will reconnect")
            shutdown_count += 1
            should_connect = True
    
    def exposed_busy_work(self):
        time.sleep(8)

    def exposed_set_state(self, new_state):
        self.state = new_state

    def exposed_get_state(self):
        return self.state

    def exposed_test_server(self):
        import rpyc.core.netref as netref
        import inspect
        root = self._unity_side
        l = root.list()
        # fetch the netref type
        netlist = netref.builtin_classes_cache['list', '__builtin__']
        if not (netlist == type(l)):
            root.log("Server's exposed_list() function does not return the expected type. Returned:{}  Expected{}".format(type(l), str(netlist)),
            logging.ERROR)
            return False

        d = root.dict()
        netdict = netref.builtin_classes_cache['dict', '__builtin__']
        if not (netdict == type(d)):
            root.log("Server's exposed_dict() function does not return the expected type. Returned:{}  Expected{}".format(type(d), str(netdict)),
            logging.ERROR)
            return False

        # Just import
        mod = root.import_module("sys")

        d['x'] = 5
        root.execute(inspect.cleandoc(
            """
            output = x+5
            """
            ), d)
        if "output" not in d or d["output"] != 10:
            root.log("Server's execute() function failed. Check the log",
            logging.ERROR)
            return False

        return True

if __name__ == '__main__':
    try:
        sleep_count = 0
        while should_connect:
            time.sleep(1)
            print ("connecting")
            c = None
            try:
                c = unity_client.connect(UnitTestClientService)
            except ConnectionRefusedError: pass
            if c:
                print ("connected")
                c.thread.join()
                c = None
            else:
                sleep_count += 1
                if sleep_count > 100:
                    print ("too many connection fails, quitting")
                    should_connect = False
    except KeyboardInterrupt:
        pass
