$ErrorActionPreference='Stop'
try {$signal=[Threading.EventWaitHandle]::OpenExisting('Local\GTBookEssentialsShow');[void]$signal.Set();$signal.Dispose();exit 0}catch{}
try {
    $scheduler=New-Object -ComObject Schedule.Service;$scheduler.Connect();$folder=$scheduler.GetFolder('\');$task=$folder.GetTask('GTBookStable')
    $expected=Join-Path $PSScriptRoot 'GTBook.exe'
    if($task.Enabled -and [string]::Equals($task.Definition.Actions.Item(1).Path,$expected,[StringComparison]::OrdinalIgnoreCase)){$started=$task.Run($null);Start-Sleep -Milliseconds 400;try{$signal=[Threading.EventWaitHandle]::OpenExisting('Local\GTBookEssentialsShow');[void]$signal.Set();$signal.Dispose()}catch{};exit 0}
}catch{}finally{foreach($item in @($started,$task,$folder,$scheduler)){if($item -and [Runtime.InteropServices.Marshal]::IsComObject($item)){[void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($item)}}}
Start-Process -FilePath (Join-Path $PSScriptRoot 'GTBook.exe') -WindowStyle Hidden
