from __future__ import division, print_function

# Import this first; it'll fix our sys.path if needed.
# It also adds threading.get_ident if it's missing.
from unity_python.client import unity_client

import logging
import os
import rpyc
import sys
import time
import traceback

# A simple client to demo the Unity Python server.
# REPL stands for "read, eval, print loop"
#       First, connect to the Unity server.
#       Then:
#               Read a line of code from stdin.
#               Evaluate that code on the server.
#               Print the resulting global variables.
#               Loop
#       Shut down when the server shuts down.
# This demonstrates writing code that drives Unity from Python, or that gets
# information out of Unity, but where Unity isn't driving the Python side at
# all.

class ReplClientService(unity_client.UnityClientService):
    def exposed_client_name(self): return "com.unity.scripting.python.clients.repl"

def get_input(prompt):
    if sys.version_info.major < 3:
        sys.stdout.write(prompt)
        sys.stdout.flush()
        return sys.stdin.readline()
    else:
        return input(prompt)

def send_log(remote_log, message, loglevel):
    remote_log(message, loglevel, "".join(traceback.format_stack()))

def repl_loop(connection):
    t = time.time()
    remote_log = rpyc.async_(connection.root.log)
    print("got async log function in {:.4}s".format(time.time() - t))

    prompt = '>|> '
    evalglobals = connection.root.dict()
    while True:
        try:
            line = get_input(prompt)
        except EOFError:
            break
        t = time.time()
        if line.startswith('log '):
            send_log(remote_log, line[4:], logging.INFO)
        elif line.startswith('warn '):
            send_log(remote_log, line[5:], logging.WARNING)
        elif line.startswith('error '):
            send_log(remote_log, line[6:], logging.ERROR)
        else:
            try:
                connection.root.execute(line, globals = evalglobals)
                for k,v in evalglobals.items():
                    if k == '__builtins__': continue
                    print("{}\t: {}".format(str(k),str(v)))
            except:
                traceback.print_exc()
        print("handled in {:.4}s".format(time.time() - t))

if __name__ == '__main__':
    t = time.time()
    c = unity_client.connect(ReplClientService)
    print("connected in {:.4}s".format(time.time() - t))
    try:
        repl_loop(c)
    except KeyboardInterrupt:
        try:
            c.close()
        except: pass
        # Print a blank line so we get some feedback that something happened.
        print('')
