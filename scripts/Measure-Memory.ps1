[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][int]$ProcessId,
    [ValidateRange(5,300)][int]$Seconds=30,
    [string]$OutputPath
)
$ErrorActionPreference='Stop'
$process=[Diagnostics.Process]::GetProcessById($ProcessId)
try {
    if($process.ProcessName -ne 'PeripheralBatteryMonitor.Desktop'){throw 'Select the Peripheral Battery Monitor desktop process'}
    $samples=@()
    for($i=0;$i -lt $Seconds;$i++){
        $process.Refresh()
        if($process.HasExited){throw 'Application exited during measurement'}
        $native=Get-CimInstance Win32_PerfFormattedData_PerfProc_Process -Filter "IDProcess=$ProcessId"
        if($null -eq $native){throw 'Process performance counters unavailable'}
        $samples+=[pscustomobject]@{PrivateWorkingSet=$native.WorkingSetPrivate;PrivateBytes=$process.PrivateMemorySize64;CpuSeconds=$process.CPU}
        Start-Sleep -Seconds 1
    }
    $report=[pscustomobject]@{
        Version=$process.MainModule.FileVersionInfo.ProductVersion
        Samples=$samples.Count
        AveragePrivateWorkingSetMiB=[Math]::Round(($samples | Measure-Object PrivateWorkingSet -Average).Average/1MB,2)
        AveragePrivateBytesMiB=[Math]::Round(($samples | Measure-Object PrivateBytes -Average).Average/1MB,2)
        CpuSeconds=$samples[-1].CpuSeconds-$samples[0].CpuSeconds
    }
    if($OutputPath){[IO.File]::WriteAllText([IO.Path]::GetFullPath($OutputPath),($report | ConvertTo-Json))}
    $report
} finally { $process.Dispose() }
