# Installation Requirements

* [Python 2.7.5 (64 bits) or later](https://www.python.org/downloads/release/python-2716/). The package does not work with Python 3.

* [Unity 2019.3](https://unity3d.com/get-unity/download). We recommend installing the latest version of Unity 2019 via the Unity Hub; 2019.3 is the minimum.

* Optional: To run the PySide example, you will need the [PySide](https://wiki.qt.io/PySide) package.

## Windows

You must use Windows 10, patched to build 1803 or later.

Install the software listed above in the default locations.

When installing Python, make sure to check the option to add to the path is on.

To get PySide after installing Python, open a command terminal and run:
```
pip install pyside
```

## Mac

Install the Unity Hub and Unity in the default location.

### System Python
Python for Unity will use the system Python packaged by Apple within the Unity process.

### Python with PySide
For the out-of-process API with PySide, installation is more complicated because [PySide](https://stackoverflow.com/questions/41472350/installing-pyside-on-mac-is-there-a-working-method) support is lacking. There are a few workarounds.

The key goal:
* Within Unity, go to `Edit -> Project Settings -> Python` and set the `Out of process Python` to point to a Python that includes PySide support.
* Verify installation by [running the PySide example](pysideExampleWalkthrough.html).

We tested three options to install PySide in a form usable from Unity.

#### MacPorts

* Install [MacPorts](https://macports.org)
* Install Python and PySide by pasting in the Terminal:
```
sudo port install python27 py27-pyside
```
* Within Unity, go to `Edit -> Project Settings -> Python` and set the out of process Python setting to read
```
/opt/local/bin/python2.7
```
* Restart Unity.

#### Using Python from Autodesk® Shotgun®

* Install the Autodesk® Shotgun® Desktop app
* Within Unity, go to `Edit -> Project Settings -> Python` and set the out of process Python setting to read
```
/Applications/Shotgun.app/Contents/Resources/Python/bin/python
```
* Restart Unity.

#### Using Python from Autodesk® Maya®

* Install Autodesk® Maya®
* Within Unity, go to `Edit -> Project Settings -> Python` and set the out of process Python setting to read
```
/Applications/Autodesk/maya2019/Maya.app/Contents/bin/mayapy
```
* Restart Unity.

## On CentOS7
Python is part of the distribution and is compatible with the integration.

To install PySide paste in a terminal:
```
yum install python2-pyside
```
It is also possible to install PySide from pip, but it requires the `qt-devel` package to be installed:
```
yum install qt-devel
pip install pyside
```
