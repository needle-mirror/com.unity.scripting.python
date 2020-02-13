# Changes in Python for Unity

RELEASE NOTES

## [2.0.1-preview.2] - 2020-02-13

This is a bugfix release for 2.0.0-preview.

FIXES
* Improved handling of a Python installation that can't find its home. Unity now displays an error rather than crashing.
* Fixed repeated registration of an OnUpdate callback.
* Prevented the Python top-level menu being added by default. Unpack the sample clients manually from the Package Manager instead.
* The tilde character (~) in Python path and site-packages is now interpreted as the user home directory on all platforms.
* In the Python Console, Ctrl-enter with no selection now executes the entire script (Cmd-enter on macOS).
* Fixed the year in 2.0.0-preview.6 release date below. It's already 2020 apparently.

## [2.0.0-preview.6] - 2020-01-08

This is the first public release of Python for Unity.

SECURITY FIX
* Earlier versions opened an internet socket that could let anyone connect. This has been replaced with a Unix domain socket, so file permissions are used. As a side effect, out-of-process performance is much faster.

## [2.0.0-preview.3] - 2019-10-25

BREAKING CHANGE
* The out-of-process API has been entirely rewritten. The new API supports multiple clients, and asynchronous calls. Clients written to the previous API will need to be updated.

NEW FEATURES
* Python Console; find it in [menu]
* Python for .NET updated to version 2.4.0

## [1.3.2-preview] - 2019-06-10

FIXES
- Fixed Python initialization problem on OSX

RELEASE NOTES
## [1.3.0-preview] - 2019-04-24

NEW FEATURES
* Updated documentation
* Add Python project settings in Unity
* Improved support for installing on Mac and Linux
* Improved logging to help troubleshooting
* Include RPyC and dependencies in Package
* Add option to use different Python on client

## [1.2.0-preview] - 2019-03-13

NEW FEATURES
* Automatically adding Python/site-packages to the PYTHONPATH for the current project and the Python packages
* Added ability to log the Python client messages into a file
* More robust reconnection on domain reload

## [1.1.4-preview] - 2018-12-21

NEW FEATURES
* Added a sample: PySideExample
* Added documentation for In-Process and Out-of-Process APIs
* Better exception logging on the client when an exception is raised on init
* Better error messages when the Python installation is not valid
* RPyC client now automatically starts on server start

## [1.1.3-preview] - 2018-12-14

NEW FEATURES
- This version provides tidier assemblies and APIs

## [1.1.2-preview] - 2018-12-07
NEW FEATURES
- Added a Python example using the RPyC architecture and PySide in the client process
- The RPyC client process now terminates when Unity exits
- The RPyC client can now be stopped and restarted
- Better logging of Python exceptions in the Unity console
- Improved error message when the Python interpreter is not properly configured
- Added a Python/Debug menu that allows to
- - Start the RPyC server
- - Stop the RPyC server
- - Start the RPyC client
- - Start the RPyC server and the client

## [1.1.1-preview] - 2018-11-26

NEW FEATURES
- Added methods to PythonRunner for 
  - Running Python on the RPyC client
  - Starting and stopping the RPyC server
  - Preventing .pyc files from being generated

FIXES
- Fixed deadlocks when closing the RPyC server and client

## [1.1.0-preview] - 2018-11-13

NEW FEATURES
- Added RPyC architecture (under Python/site-packages/unity_rpyc)
- Updated Python for .NET to include:
  - A fix to a crash when finalizing the Python interpreter on domain unload
  - A C# callback on Python for .NET shutdown

KNOWN ISSUES
- There might be scenarios that still crash/hang Unity when running Python after reloading assemblies. 
  - If your tools are affected by domain reload, consider using the RPyC architecture. Refer to the documentation for an example on how to use the RPyC architecture.

## [1.0.0] - 2018-10-05

NEW FEATURES
- added Python support in Unity for Windows and Mac

## [All Versions] 
- Trying to call UnityEngine.Debug.Log (or its variants) with a python string that contains non-ANSI characters, will cause the following error: `Python.Runtime.PythonException: TypeError : No method matches given arguments for Log`. For example this can happen on a French language version of Windows when a socket connection fails with `error: [Errno 10061] Aucune connexion n’a pu être établie car l’ordinateur cible l’a expressément refusée`.
