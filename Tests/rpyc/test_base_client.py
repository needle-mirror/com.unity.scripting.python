from __future__ import division, print_function
from unity_python.client import unity_client


if __name__ == '__main__':
    try:
        s = unity_client.UnityClientService()
        c = unity_client.connect(s)
        time.sleep(2)
        s.UnityEngine.Debug.Log("Hello test")
        c.thread.join()
        c = None
    except KeyboardInterrupt:
        pass