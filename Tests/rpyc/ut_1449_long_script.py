import unity_connection
import time

# Write a log in the console every second
duration = 5
for i in range(duration):
    unity_connection.get_module('UnityEngine').Debug.Log("Testing UT-1449: long script, %d/%d"%(i+1,duration))
    time.sleep(1)
