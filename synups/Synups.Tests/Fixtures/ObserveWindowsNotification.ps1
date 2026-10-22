# Test-only independent observation of native Windows Shell UI. Does not submit a notification or alter settings.
$ErrorActionPreference='Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
[IO.File]::WriteAllText($env:SYNUPS_OBSERVER_READY,'ready')
$deadline=[DateTime]::UtcNow.AddSeconds(22)
$observations=@()
while([DateTime]::UtcNow -lt $deadline){
 $root=[Windows.Automation.AutomationElement]::RootElement
 $all=$root.FindAll([Windows.Automation.TreeScope]::Descendants,[Windows.Automation.Condition]::TrueCondition)
 foreach($element in $all){
  try{
   $current=$element.Current
   if($current.Name -like "*$($env:SYNUPS_OBSERVER_TOKEN)*"){
    $owner=Get-Process -Id $current.ProcessId -ErrorAction SilentlyContinue
    # A log/console or our own process is not proof of delivery by Windows Shell.
    if($owner.ProcessName -notin @('explorer','ShellExperienceHost','ShellHost','StartMenuExperienceHost')){continue}
    $rect=$current.BoundingRectangle
    if($current.IsOffscreen -or $rect.Width -le 0 -or $rect.Height -le 0){continue}
    $result=@{delivered_in_native_shell_ui=$true;token=$env:SYNUPS_OBSERVER_TOKEN;owner=$owner.ProcessName;process_id=$owner.Id;name=$current.Name;verified_utc=[DateTime]::UtcNow.ToString('o');physical_human_observation=$false}
    if($rect.Width -gt 0 -and $rect.Height -gt 0){
     $bitmap=New-Object Drawing.Bitmap([int]$rect.Width,[int]$rect.Height)
     try{$graphics=[Drawing.Graphics]::FromImage($bitmap);try{$graphics.CopyFromScreen([int]$rect.X,[int]$rect.Y,0,0,$bitmap.Size)}finally{$graphics.Dispose()};$bitmap.Save($env:SYNUPS_OBSERVER_CAPTURE,[Drawing.Imaging.ImageFormat]::Png);$result.capture_created=$true}catch{$result.capture_created=$false;$result.capture_error=$_.Exception.Message}finally{$bitmap.Dispose()}
    }
    $result | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 $env:SYNUPS_OBSERVER_REPORT
    exit 0
   }
  }catch{}
 }
 Start-Sleep -Milliseconds 200
}
@{delivered_in_native_shell_ui=$false;failure='No matching notification in independent Windows Shell UIAutomation tree';verified_utc=[DateTime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -Encoding utf8 $env:SYNUPS_OBSERVER_REPORT
exit 1
