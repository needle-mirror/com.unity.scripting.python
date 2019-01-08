"""
Impersonate a Unix domain socket using ... a Unix domain socket.

Microsoft added support for AF_UNIX to Windows 10 in early 2018.
There's no python support as of Python 3.8: bpo-33408.

This file is a partial reimplementation of CPython's socketmodule.c

What's here is just what's needed for RPyC itself, and there's probably less
error-handling.

See also rpyc.lib.compat which is responsible for defining unix_socket to point
either to socket.socket or to win_unix_socket.WindowsUnixSocket depending on
the platform.
"""
from __future__ import division
import ctypes
import ctypes.wintypes
import socket
import time
import sys

socket.AF_UNIX = 1
POLLIN = 0x100 | 0x200
POLLOUT = 0x10
WSAEINTR = 10004
FIONBIO = 0x8004667e
MAX_PATH_LENGTH = 260
WSAEWOULDBLOCK = 10035
SOL_SOCKET = 65535
SO_ERROR = 4103
EISCONN = 10056
WSA_INVALID_PARAMETER = 87
FORMAT_MESSAGE_ALLOCATE_BUFFER = 0x100
FORMAT_MESSAGE_FROM_SYSTEM = 0x1000
FORMAT_MESSAGE_IGNORE_INSERTS = 0x200
FORMAT_MESSAGE_FLAGS = (FORMAT_MESSAGE_ALLOCATE_BUFFER
    | FORMAT_MESSAGE_FROM_SYSTEM
    | FORMAT_MESSAGE_IGNORE_INSERTS
)


# Get the windows API calls we need.
winsock = ctypes.windll.ws2_32
kernel = ctypes.windll.kernel32

# On startup (when we import this module), initialize winsock.
def init_winsock():
    class WSAData(ctypes.Structure):
        _fields_ = (
            ('wVersion', ctypes.wintypes.WORD),
            ('wHighVersion', ctypes.wintypes.WORD),
            ('foo', ctypes.c_char * 4000), # size unclear, this should be enough
        )
    data = WSAData()

    # Request API 2.2 which is available since win98/win2k, 20 years before
    # AF_UNIX support was finally added.
    winsock.WSAStartup(0x0202, data)
init_winsock()


# The SOCKET type is intptr.
SOCKET = ctypes.c_void_p

# sockaddr_un for storing unix addresses.
class sockaddr_un(ctypes.Structure):
    _fields_ = (
        ('sun_family', ctypes.c_ushort),
        ('sun_path', ctypes.c_byte * MAX_PATH_LENGTH),
    )

    @property
    def path(self):
        """
        Return the path as an ascii string.
        """
        return ctypes.cast(self.sun_path, ctypes.c_char_p).value

# for poll
class pollfd(ctypes.Structure):
    _fields_ = (
        ('fd', SOCKET),
        ('events', ctypes.c_short),
        ('revents', ctypes.c_short),
    )

#########################
# Import the calls we need

# rename to socket_fn to avoid clash with socket module
socket_fn = winsock.socket
socket_fn.argtypes = (
    ctypes.c_int, # address family
    ctypes.c_int, # type
    ctypes.c_int, # protocol
)

accept = winsock.accept
accept.argtypes = (
    SOCKET,
    ctypes.POINTER(sockaddr_un),
    ctypes.POINTER(ctypes.c_int),
)

bind = winsock.bind
bind.argtypes = (
    SOCKET,
    ctypes.POINTER(sockaddr_un),
    ctypes.c_int,
)

close = winsock.closesocket
close.argtypes = (
    SOCKET,
)

connect = winsock.connect
connect.argtypes = (
    SOCKET,
    ctypes.POINTER(sockaddr_un),
    ctypes.c_int,
)

getpeername = winsock.getpeername
getpeername.argtypes = (
    SOCKET,
    ctypes.POINTER(sockaddr_un),
    ctypes.POINTER(ctypes.c_int),
)

getsockname = winsock.getsockname
getsockname.argtypes = (
    SOCKET,
    ctypes.POINTER(sockaddr_un),
    ctypes.POINTER(ctypes.c_int),
)

getsockopt = winsock.getsockopt
getsockopt.argtypes = (
    SOCKET,
    ctypes.c_int, # level (eg SOL_SOCKET)
    ctypes.c_int, # optname (eg SO_ERROR)
    ctypes.c_void_p, # optval
    ctypes.POINTER(ctypes.c_int), # optlen
)

listen = winsock.listen
listen.argtypes = (
    SOCKET,
    ctypes.c_int, # backlog
)

recv = winsock.recv
recv.argtypes = (
    SOCKET,
    ctypes.c_char_p, # buf
    ctypes.c_int, # len
    ctypes.c_int, # flags
)

send = winsock.send
send.argtypes = (
    SOCKET,
    ctypes.c_char_p, # buf
    ctypes.c_int, # len
    ctypes.c_int, # flags
)

shutdown = winsock.shutdown
shutdown.argtypes = (
    SOCKET,
    ctypes.c_int, # how
)

errno_fn = winsock.WSAGetLastError
errno_fn.argtypes = ()

WSASetLastError = winsock.WSASetLastError
WSASetLastError.argtypes = (
    ctypes.c_int,
)

# for setblocking
ioctl = winsock.ioctlsocket
ioctl.argtypes = (
    SOCKET,
    ctypes.c_long, # cmd
    ctypes.POINTER(ctypes.c_ulong), # argp
)

poll = winsock.WSAPoll
poll.argtypes = (
    ctypes.POINTER(pollfd), # fds
    ctypes.c_ulong, # number of fds
    ctypes.c_int, # timeout (ms)
)

# for printing errors
FormatMessageW = kernel.FormatMessageW
FormatMessageW.argtypes = (
        ctypes.wintypes.DWORD, # flags
        ctypes.c_void_p, # source (?)
        ctypes.wintypes.DWORD, # message
        ctypes.wintypes.DWORD, # language (0 for default lang)
        ctypes.c_void_p, # buffer; declared as LPWSTR but actually WCHAR**
        ctypes.wintypes.DWORD, # buffer size
        ctypes.c_void_p, # va_args
)
LocalFree = kernel.LocalFree
LocalFree.argtypes = (
    ctypes.c_void_p,
)

# Raise a socket.error
def raise_socket(errno):
    """
    Raise a Windows error as a socket.error.

    Attach the windows error message.
    """
    buf = ctypes.c_wchar_p(None)

    FormatMessageW(
        FORMAT_MESSAGE_FLAGS,
        None,
        errno,
        0,
        ctypes.byref(buf),
        0,
        None,
    )
    try:
        msg = unicode(buf.value)
    finally:
        LocalFree(buf)

    raise socket.error(errno, msg)


# copy the concept in cpython:
#   * wait until the socket is readable or writeable
#   * first, evaluate the function
#   * if interrupted by a signal, retry
#   * if timeout is set, run async and wait for completion or timeout.

def wait_for_fd(s, r_w_connect, timeout):
    """
    Wait until the socket is ready.

    Low on error handling.

    r_w_connect is 0 for read, 1 for write, 2 for connect
    """
    p = pollfd()
    p.fd = s._sock_fd
    if r_w_connect == 0:
        p.events = POLLIN
    elif r_w_connect == 1:
        p.events = POLLOUT
    else:
        p.events = POLLIN | POLLERR

    return poll(ctypes.byref(p), 1, int(timeout))

def call_socket_fn(s, r_w_connect, sockfn):
    """
    Call a function (passed in as a lambda: () -> bool) on a socket,
    with a specified timeout in ms.

    r_w_connect is 0 for read, 1 for write, 2 for connect

    The socket must be non-blocking unless timeout is -1.

    sockfn should be a lambda that returns non-zero on success and zero on fail.

    Heavily based on cpython's sock_call_ex.
    """
    # QueryPerformanceCounter, monotonic and accurate to sub-microsecond.
    if s._timeout is None:
        timeout = -1
    else:
        timeout = s._timeout
    if timeout >= 0:
        end_t = time.clock() + timeout

    while True:
        # wait until we can do the thing; always wait if we're connecting
        if timeout > 0 or r_w_connect == 2:
            if timeout < 0:
                milliseconds = -1
            else:
                milliseconds = max(1, int( (end_t - time.clock()) * 1000.0))
            err = wait_for_fd(s, r_w_connect, milliseconds)
            if err < 0:
                # get the error reason
                err = errno_fn()
                if err == WSAEINTR:
                    # interrupted by signal (and didn't raise) => retry
                    continue
                else:
                    # some other kind of error => raise
                    raise_socket(err)
            elif err == 0:
                # timeout
                raise socket.timeout("timed out")

        # socket is ready; do the thing (loop until we aren't interrupted)
        # the loop sets 'err' if we break out of the loop.
        while True:
            ok = (sockfn() != 0)
            if ok:
                ######################
                # The thing is done!
                # This is the only success codepath.
                ######################
                return
            else:
                # why did it fail?
                err = errno_fn()
                if err == WSAEINTR:
                    # interrupted by signal (and didn't raise) => retry
                    continue
                elif timeout > 0 and err == WSAEWOULDBLOCK:
                    # break this loop, continue outer loop that does a poll()
                    # and waits for the async IO to finish.
                    break
                else:
                    # if we're here, the error is scary
                    raise_socket(err)


class WindowsUnixSocket(object):
    """
    Emulate python AF_UNIX sockets using windows AF_UNIX sockets.

    Python doesn't have support for AF_UNIX on Windows, though Windows itself
    does, so we need to reimplement socketmodule.c to get compatibility.
    """
    __slots__ = (
        "_sock_fd", # socket "file" descriptor
        "_timeout", # timeout in seconds, None for infinite
    )

    def __init__(self, af=socket.AF_UNIX, type=socket.SOCK_STREAM, proto=0, fd = None):
        self._timeout = None
        if af != socket.AF_UNIX or type != socket.SOCK_STREAM:
            raise socket.error(WSA_INVALID_PARAMETER,
                "Invalid family or type: AF_UNIX and SOCK_STREAM is what we implement")
        if fd is None:
            self._sock_fd = socket_fn(socket.AF_UNIX, socket.SOCK_STREAM, 0)
        else:
            self._sock_fd = fd


    def settimeout(self, seconds):
        """
        Set the timeout for blocking operations on the socket.
        None means block forever.
        """
        if seconds is None or seconds < 0:
            self._timeout = None
            is_async = ctypes.wintypes.ULONG(0)
        else:
            self._timeout = seconds
            is_async = ctypes.wintypes.ULONG(1)
        ioctl(self._sock_fd, FIONBIO, ctypes.byref(is_async))

    def setblocking(self, flag):
        if flag:
            self.settimeout(None)
        else:
            self.settimeout(0)

    def bind(self, path):
        """
        Bind to a path.
        """
        path = bytes(path)
        nbytes = len(path) + 1
        if nbytes > MAX_PATH_LENGTH:
            raise socket.error(ERROR_BAD_PATHNAME,
                    "Socket path name too long ({} bytes, max {})".format(nbytes, MAX_PATH_LENGTH)
            )
        addr = sockaddr_un()
        addr.sun_family = socket.AF_UNIX
        # copy the name including the null terminator (cpython has null-terminated strings under the hood)
        ctypes.memmove(addr.sun_path, path, nbytes)
        if bind(self._sock_fd, ctypes.byref(addr), ctypes.sizeof(addr)) != 0:
            raise_socket(errno_fn())

    def listen(self, n):
        """
        Start to listen on the named pipe.
        """
        if listen(self._sock_fd, n) != 0:
            raise_socket(errno_fn())

    def accept(self):
        """
        Block until an incoming connection arrives, or until the timeout elapses.

        Returns a pair with the socket and the peer address.
        Note: the peer is normally anonymous; a blank string is normal.
        """
        addr = sockaddr_un()
        n = ctypes.c_int(ctypes.sizeof(addr))
        nonlocal_fd = [None]
        def do_accept():
            """
            Do the accept, return 0 and write to the_fd[0] on success.

            call_socket_fn will handle interrupts, timeouts, error handling.
            """
            s = accept(self._sock_fd, ctypes.byref(addr), ctypes.byref(n))
            if int(s) >= 0:
                nonlocal_fd[0] = s
                return 1
            return 0
        call_socket_fn(self, 0, do_accept)
        return (WindowsUnixSocket(fd = nonlocal_fd[0]), addr.path)

    def shutdown(self, how):
        """
        Send any data that still needs to be sent.

        This is blocking no matter what, no timeout.
        """
        shutdown(self._sock_fd, how)

    def close(self):
        """
        Close the socket.
        """
        close(self._sock_fd)
        self._sock_fd = -1

    def connect(self, path):
        """
        Connect to a server whose listener pipe is the given path.

        Returns None. After this call, this socket will be connected to the server
        unless an exception was raised.
        """
        path = bytes(path)
        # copy the name including the null terminator (cpython has null-terminated strings under the hood)
        nbytes = len(path) + 1
        if nbytes > MAX_PATH_LENGTH:
            raise socket.error(ERROR_BAD_PATHNAME,
                    "Socket path name too long ({} bytes, max {})".format(nbytes, MAX_PATH_LENGTH)
            )
        addr = sockaddr_un()
        addr.sun_family = socket.AF_UNIX
        ctypes.memmove(addr.sun_path, path, nbytes)

        # Try to connect. Might even succeed already!
        res = connect(self._sock_fd, ctypes.byref(addr), ctypes.sizeof(addr))
        if res == 0:
            return

        # Failed. Could be that it's non-blocking, or that it's interrupted, or
        # that there was actually an error.
        err = errno_fn()
        if err != WSAEINTR and err != WSAEWOULDBLOCK:
            # this is really an error
            raise_socket(err)

        # Wait for connection to finish, using getsockopt.
        def check_connected():
            err = ctypes.c_int()
            n = ctypes.c_int(ctypes.sizeof(err))
            res = getsockopt(self._sock_fd, SOL_SOCKET, SO_ERROR, ctypes.byref(err), ctypes.byref(n))
            if res != 0:
                # getsockopt itself failed.
                return 0
            if err.value == 0 or err.value == EISCONN:
                # If there's no error, success!
                # If the error is that we're connected... also success!
                return 1
            else:
                # We saw an error on the socket; the caller expects that
                # error to be in errno so set the winsock version of errno.
                WSASetLastError(err.value)
                return 0
        call_socket_fn(self, 0, check_connected)

    def fileno(self):
        if int(self._sock_fd) < 0:
            raise EOFError()
        else:
            return self._sock_fd

    def getpeername(self):
        addr = sockaddr_un()
        n = ctypes.c_int(ctypes.sizeof(addr))
        if getpeername(self._sock_fd, ctypes.byref(addr), ctypes.byref(n)) != 0:
            raise_socket(errno_fn())
        return addr.path

    def getsockname(self):
        addr = sockaddr_un()
        n = ctypes.c_int(ctypes.sizeof(addr))
        if getsockname(self._sock_fd, ctypes.byref(addr), ctypes.byref(n)) != 0:
            raise_socket(errno_fn())
        return addr.path

    def recv(self, n):
        buffer = ctypes.create_string_buffer(n)
        nonlocal_n = []
        def do_recv():
            res = recv(self._sock_fd, buffer, n, 0)
            if res >= 0:
                # res is the number of bytes actually received
                nonlocal_n.append(res)
                return 1
            # otherwise, some other kind of error
            return 0
        call_socket_fn(self, 0, do_recv)
        n = nonlocal_n[0]
        # important: not buffer.value which truncates at the first nul
        return bytes(bytearray(buffer[:n]))

    def send(self, msg):
        buffer = bytes(msg)
        nonlocal_n = []
        def do_send():
            res = send(self._sock_fd, buffer, len(buffer), 0)
            if res >= 0:
                # res is the number of bytes actually sent
                nonlocal_n.append(res)
                return 1
            # otherwise, some kind of error
            return 0
        call_socket_fn(self, 1, do_send)
        return nonlocal_n[0]
