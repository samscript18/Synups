param([Parameter(Mandatory=$true)][string]$Executable,[Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference='Stop'
$exe=(Resolve-Path $Executable).Path
$out=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force $out | Out-Null
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class OriginalWindow {
 [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hwnd,IntPtr hdc,uint flags);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
}
'@
$rule='Synups-original-evidence-'+[Guid]::NewGuid().ToString('N')
$process=$null
$runKeyPath='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runKeyExisted=Test-Path $runKeyPath
$record=[ordered]@{ upstream_revision='61ae14391151ca9f209223e62c98fae3df5482b8'; workflow_revision=$env:GITHUB_SHA; started_utc=[DateTime]::UtcNow.ToString('o'); os=[Environment]::OSVersion.VersionString; framework='net48'; original_product_source_modified=$false; network_scope='Disposable GitHub Windows runner; outbound connections from original WirePeep.exe explicitly blocked by temporary per-process firewall rule. Host/user networks elsewhere untouched.'; method='Actual original WPF executable, user32 PrintWindow, clean WM_CLOSE, actual Menees .stgx files written by app'; screenshot_verified_visually=$false }
try {
 # Original AppOptions.Apply assumes this standard user key exists. Fresh CI images may omit it.
 # Create an empty key only; never install a startup command. Original source stays untouched.
 $record.run_registry_key_existed=$runKeyExisted
 if(-not $runKeyExisted){ New-Item -Path $runKeyPath -Force | Out-Null }
 $record.startup_entry_installed=$false
 New-NetFirewallRule -DisplayName $rule -Direction Outbound -Program $exe -Action Block -Profile Any | Out-Null
 $record.firewall_rule_created=$true
 $record.executable_sha256=(Get-FileHash $exe -Algorithm SHA256).Hash.ToLowerInvariant()
 $process=Start-Process -FilePath $exe -WorkingDirectory ([IO.Path]::GetDirectoryName($exe)) -PassThru -RedirectStandardError "$out/stderr.txt" -RedirectStandardOutput "$out/stdout.txt"
 $deadline=[DateTime]::UtcNow.AddSeconds(40)
 do { Start-Sleep -Milliseconds 250; $process.Refresh(); if($process.HasExited){throw "Original application exited before window creation: $($process.ExitCode)"} } while(($process.MainWindowHandle -eq [IntPtr]::Zero) -and [DateTime]::UtcNow -lt $deadline)
 if($process.MainWindowHandle -eq [IntPtr]::Zero){throw 'No original WPF native window appeared.'}
 Start-Sleep -Seconds 2
 $process.Refresh();$hwnd=$process.MainWindowHandle
 $record.window_title=$process.MainWindowTitle
 $record.process_id=$process.Id;$record.window_handle=$hwnd.ToInt64();$record.window_visible=[OriginalWindow]::IsWindowVisible($hwnd)
 $record.loaded_runtime=@($process.Modules | Where-Object { $_.ModuleName -in @('clr.dll','Menees.Common.dll','Menees.Windows.Presentation.dll') } | ForEach-Object { @{name=$_.ModuleName;version=$_.FileVersionInfo.FileVersion;sha256=(Get-FileHash $_.FileName -Algorithm SHA256).Hash.ToLowerInvariant()} })
 $rect=New-Object OriginalWindow+RECT
 if(-not [OriginalWindow]::GetWindowRect($hwnd,[ref]$rect)){throw 'Cannot read original window bounds.'}
 $width=$rect.Right-$rect.Left;$height=$rect.Bottom-$rect.Top
 if($width -lt 100 -or $height -lt 100){throw 'Invalid native window dimensions.'}
 $bitmap=New-Object Drawing.Bitmap($width,$height)
 try {
  $graphics=[Drawing.Graphics]::FromImage($bitmap)
  try { $hdc=$graphics.GetHdc();try{$printed=[OriginalWindow]::PrintWindow($hwnd,$hdc,2)}finally{$graphics.ReleaseHdc($hdc)} }finally{$graphics.Dispose()}
  if(-not $printed){throw 'PrintWindow failed; no screenshot evidence.'}
  $bitmap.Save("$out/wirepeep-original-windows.png",[Drawing.Imaging.ImageFormat]::Png)
  $record.screenshot_sha256=(Get-FileHash "$out/wirepeep-original-windows.png" -Algorithm SHA256).Hash.ToLowerInvariant()
  $record.window_dimensions=@{width=$width;height=$height}
  if($width -lt 600 -or $height -lt 300){throw 'Captured a small startup/error dialog, not the original main application. Screenshot is failed diagnostic evidence only.'}
 } finally {$bitmap.Dispose()}
 $record.close_main_window_sent=$process.CloseMainWindow()
 if(-not $process.WaitForExit(15000)){throw 'Original app did not close cleanly; fixture not certified.'}
 $record.process_exit_code=$process.ExitCode
 if($process.ExitCode -ne 0){throw "Original app exit $($process.ExitCode)"}
 $roots=@([IO.Path]::GetDirectoryName($exe),(Join-Path $env:LOCALAPPDATA 'Menees'),(Join-Path $env:TEMP 'Menees'))
 $fixtures=@(foreach($directory in $roots){if(Test-Path $directory){Get-ChildItem $directory -Filter '*.stgx' -Recurse -File | Where-Object {$_.LastWriteTimeUtc -ge [DateTime]::Parse($record.started_utc).AddSeconds(-2)}}})
 if($fixtures.Count -eq 0){throw 'Original app produced no genuine settings fixture at the documented store locations.'}
 New-Item -ItemType Directory -Force "$out/settings" | Out-Null
 $record.settings=@($fixtures | ForEach-Object { $destination=Join-Path "$out/settings" $_.Name;Copy-Item -LiteralPath $_.FullName -Destination $destination;@{name=$_.Name;bytes=$_.Length;sha256=(Get-FileHash $destination -Algorithm SHA256).Hash.ToLowerInvariant()} })
 $record.status='passed-original-launch-capture-clean-exit-fixture-pending-visual-review'
} catch { $record.status='failed';$record.error=$_.Exception.Message;throw }
finally {
 if($null -ne $process -and -not $process.HasExited){Stop-Process -Id $process.Id -Force;$record.forced_cleanup=$true}
 Remove-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue
 $record.firewall_rule_removed=$null -eq (Get-NetFirewallRule -DisplayName $rule -ErrorAction SilentlyContinue)
 if(-not $runKeyExisted -and (Test-Path $runKeyPath)){ $key=Get-Item $runKeyPath;if($key.ValueCount -eq 0 -and $key.SubKeyCount -eq 0){Remove-Item $runKeyPath} }
 $record.finished_utc=[DateTime]::UtcNow.ToString('o')
 $record | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 "$out/original-evidence.json"
}
