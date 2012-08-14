#nowarn "9"

open System
open System.Diagnostics
open System.Runtime.InteropServices
open System.Threading
open Microsoft.Win32.SafeHandles

[<StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode); Struct>]
type STARTUPINFO =
    [<DefaultValue>] val mutable public cb : int
    [<DefaultValue>] val mutable public lpReserved : string
    [<DefaultValue>] val mutable public lpDesktop : string
    [<DefaultValue>] val mutable public lpTitle : string
    [<DefaultValue>] val mutable public dwX : int
    [<DefaultValue>] val mutable public dwY : int
    [<DefaultValue>] val mutable public dwXSize : int
    [<DefaultValue>] val mutable public dwYSize : int
    [<DefaultValue>] val mutable public dwXCountChars : int
    [<DefaultValue>] val mutable public dwYCountChars : int
    [<DefaultValue>] val mutable public dwFillAttribute : int
    [<DefaultValue>] val mutable public dwFlags : int
    [<DefaultValue>] val mutable public wShowWindow : int16
    [<DefaultValue>] val mutable public cbReserved2 : int16
    [<DefaultValue>] val mutable public lpReserved2 : nativeint
    [<DefaultValue>] val mutable public hStdInput : nativeint
    [<DefaultValue>] val mutable public hStdOutput : nativeint
    [<DefaultValue>] val mutable public hStdError : nativeint

[<StructLayout(LayoutKind.Sequential); Struct>]
type PROCESS_INFORMATION  =
   [<DefaultValue>] val mutable public hProcess : nativeint
   [<DefaultValue>] val mutable public hThread : nativeint
   [<DefaultValue>] val mutable public dwProcessId : uint32
   [<DefaultValue>] val mutable public dwThreadId : uint32

type DebugEventType =
    | ExitProcessDebugEvent = 5
    | LoadDllDebugEvent = 6

[<StructLayout(LayoutKind.Sequential); Struct>]
type DEBUG_EVENT =
    [<DefaultValue>] val mutable public dwDebugEventCode : DebugEventType 
    [<DefaultValue>] val mutable public dwProcessId : uint32
    [<DefaultValue>] val mutable public dwThreadId : uint32

[<StructLayout(LayoutKind.Sequential); Struct>]
type LOAD_DLL_DEBUG_INFO =
   [<DefaultValue>] val mutable public DebugEvent : DEBUG_EVENT
   [<DefaultValue>] val mutable public hFile : nativeint
   [<DefaultValue>] val mutable public lpBaseOfDll : nativeint
   [<DefaultValue>] val mutable public dwDebugInfoFileOffset : uint32
   [<DefaultValue>] val mutable public nDebugInfoSize : uint32
   [<DefaultValue>] val mutable public lpImageName : nativeint
   [<DefaultValue>] val mutable public fUnicode : uint16

module NativeMethods =

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool CreateProcess(string lpApplicationName,
       string lpCommandLine, nativeint lpProcessAttributes, 
       nativeint lpThreadAttributes, bool bInheritHandles, 
       uint32 dwCreationFlags, nativeint lpEnvironment, string lpCurrentDirectory,
       [<In>] STARTUPINFO& lpStartupInfo, 
       [<Out>] PROCESS_INFORMATION& lpProcessInformation)
    
    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool WaitForDebugEvent(nativeint lpDebugEvent, int dwMilliseconds)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool ContinueDebugEvent(uint32 dwProcessId, uint32 dwThreadId, uint32 dwContinueStatus)

    [<DllImport("kernel32.dll", SetLastError = true)>]
    extern bool GetExitCodeProcess(nativeint hProcess, [<Out>] int& lpExitCode)

type AnyWaitHandle(handle : nativeint, owns : bool) =
    inherit WaitHandle()
    do base.SafeWaitHandle <- new SafeWaitHandle(handle, owns)

module Program =

    [<EntryPoint>]
    let main args =
        let mutable si = STARTUPINFO(cb = Marshal.SizeOf(typeof<STARTUPINFO>), dwFlags = 1, wShowWindow = 5s)
        let mutable pi = PROCESS_INFORMATION()
        if not (NativeMethods.CreateProcess(args.[0], (if args.Length > 1 then args.[1] else null), IntPtr.Zero, IntPtr.Zero, false, 1u, IntPtr.Zero, null, &si, &pi)) then
            Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error())

        use hp = new AnyWaitHandle(pi.hProcess, true)
        use ht = new AnyWaitHandle(pi.hThread, true)

        let buffer = Marshal.AllocHGlobal(256)
        let processId = pi.dwProcessId

        let rec run () =
            if not (NativeMethods.WaitForDebugEvent(buffer, Timeout.Infinite)) then
                Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error())

            let e : DEBUG_EVENT = unbox (Marshal.PtrToStructure(buffer, typeof<DEBUG_EVENT>))
            printfn "%O" e.dwDebugEventCode

            match e.dwDebugEventCode with
            | DebugEventType.ExitProcessDebugEvent when e.dwProcessId = processId ->
                ()

            | _ ->
                ignore (NativeMethods.ContinueDebugEvent(e.dwProcessId, e.dwThreadId, 0x80010001u))
                run ()

        try
            run ()
        finally
            Marshal.FreeHGlobal(buffer)

        let mutable exitCode = 0
        if not (NativeMethods.GetExitCodeProcess(pi.hProcess, &exitCode)) then
            Marshal.ThrowExceptionForHR(Marshal.GetHRForLastWin32Error())

        exitCode
