logdlls
=======
A utility to track DLLs loaded by a Windows process and log them to a file.

Usage
-----
    logdlls dlls.txt app.exe arg1 arg2 [...]

How it works
------------
logdlls acts as a debugger; it starts the process (and any child processes) using `CreateProcess`
with the `DEBUG_PROCESS` flag, then calls `WaitForDebugEvent`. For events of type
`LOAD_DLL_DEBUG_EVENT`, logdlls accepts a file handle to the DLL, passes it to a background thread,
and allows the process to continue.

The background thread obtains the name of the DLL from its file handle: it calls
`CreateFileMapping`, `MapViewOfFile`, then `GetMappedFileName`. Note that strings returned from
`GetMappedFileName` use Windows kernel syntax; logdlls uses `GetLogicalDriveStrings` to find all the
drive letters in the system, then `QueryDosDevice` to find which the letter of the drive on which
the DLL is located.
