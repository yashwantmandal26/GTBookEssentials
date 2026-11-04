function Get-GTBookDeviceSupport([bool]$Supported) {
    if(-not $Supported){return [pscustomobject]@{Ready=$false;Status='UnsupportedProfile'}}
    try {
        $class=Get-CimClass -Namespace root/wmi -ClassName ACPIMethod -OperationTimeoutSec 3 -ErrorAction Stop
        $guid=($class.CimClassQualifiers | Where-Object Name -eq 'guid').Value
        $methods=@($class.CimClassMethods | ForEach-Object Name)
        if($guid -ne '{3161c7c3-489f-4074-82ae-538101cce1c2}' -or @('MemIO','DoMethod','SMI' | Where-Object {$_ -notin $methods}).Count){
            return [pscustomobject]@{Ready=$false;Status='InterfaceMismatch'}
        }
        $instances=@(Get-CimInstance -Namespace root/wmi -ClassName ACPIMethod -OperationTimeoutSec 3 -ErrorAction Stop)
        return [pscustomobject]@{Ready=($instances.Count -gt 0);Status=$(if($instances.Count -gt 0){'Ready'}else{'ProviderUnavailable'})}
    } catch {
        $status='ProviderUnavailable'
        if($_.Exception -is [Microsoft.Management.Infrastructure.CimException]){
            if([int]$_.Exception.NativeErrorCode -eq 2){$status='AccessDenied'}
        }
        return [pscustomobject]@{Ready=$false;Status=$status}
    }
}
function Invoke-GTBookDeviceScan {
    # Use Windows' own device enumeration. Do not install exported drivers, rewrite
    # firmware, recompile arbitrary MOF files or reset the WMI repository.
    $service=Get-Service -Name Winmgmt -ErrorAction Stop
    if($service.Status -ne 'Running'){Start-Service -Name Winmgmt -ErrorAction Stop}
    $process=Start-Process -FilePath (Join-Path $env:WINDIR 'System32\pnputil.exe') -ArgumentList '/scan-devices' -WindowStyle Hidden -PassThru
    try{if(-not $process.WaitForExit(15000)){try{$process.Kill()}catch{};throw 'Device scan timed out.'};if($process.ExitCode -ne 0){throw 'Windows device scan did not complete successfully.'}}finally{$process.Dispose()}
}
function Resolve-GTBookDeviceSupport([bool]$Supported,[scriptblock]$Probe={param($profile) Get-GTBookDeviceSupport $profile},[scriptblock]$Scan={Invoke-GTBookDeviceScan}) {
    $result=& $Probe $Supported
    if($Supported -and -not $result.Ready -and $result.Status -eq 'ProviderUnavailable'){
        try{& $Scan;$result=& $Probe $Supported}catch{}
    }
    return $result
}
