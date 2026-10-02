param(
    [Parameter(Mandatory = $true)]
    [string]$Path
)

$ErrorActionPreference = "SilentlyContinue"

if (-not (Test-Path -LiteralPath $Path)) {
    exit 0
}

$code = @"
using System;
using System.Runtime.InteropServices;

public static class RestartManager
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string strServiceShortName;
        public int ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    public static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll")]
    public static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    public static extern int RmRegisterResources(
        uint pSessionHandle,
        uint nFiles,
        string[] rgsFilenames,
        uint nApplications,
        RM_UNIQUE_PROCESS[] rgApplications,
        uint nServices,
        string[] rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    public static extern int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
        ref uint lpdwRebootReasons);
}
"@

Add-Type $code

$resources = New-Object System.Collections.Generic.List[string]
$resources.Add((Resolve-Path -LiteralPath $Path).Path)

Get-ChildItem -LiteralPath $Path -Recurse -Force |
    Select-Object -First 400 |
    ForEach-Object { $resources.Add($_.FullName) }

$session = 0
$sessionKey = [Guid]::NewGuid().ToString()
if ([RestartManager]::RmStartSession([ref]$session, 0, $sessionKey) -ne 0) {
    exit 0
}

try {
    [void][RestartManager]::RmRegisterResources($session, [uint32]$resources.Count, [string[]]$resources.ToArray(), 0, $null, 0, $null)

    $needed = 0
    $count = 0
    $reason = 0
    [void][RestartManager]::RmGetList($session, [ref]$needed, [ref]$count, $null, [ref]$reason)
    if ($needed -le 0) {
        exit 0
    }

    $count = $needed
    $apps = New-Object RestartManager+RM_PROCESS_INFO[] $count
    [void][RestartManager]::RmGetList($session, [ref]$needed, [ref]$count, $apps, [ref]$reason)

    $self = $PID
    $apps |
        Select-Object -First $count |
        ForEach-Object {
            $processId = $_.Process.dwProcessId
            if ($processId -and $processId -ne $self) {
                Stop-Process -Id $processId -Force
            }
        }
}
finally {
    [void][RestartManager]::RmEndSession($session)
}
