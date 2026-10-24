#Requires -Version 5.1
[CmdletBinding()]
param([switch]$ShowWindow,[switch]$SmokeTest)
$ErrorActionPreference='Stop'
Add-Type -Path @(Get-ChildItem (Join-Path $PSScriptRoot '../Core/*.cs'),(Join-Path $PSScriptRoot '../Devices/*.cs') | ForEach-Object FullName)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type 'using System; using System.Runtime.InteropServices; public class PbmIcon { [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h); }'
[Windows.Forms.Application]::EnableVisualStyles()
$instanceName=if($SmokeTest){'Local\PeripheralBatteryMonitor.UiTest.'+[Guid]::NewGuid().ToString('N')}else{'Local\AJ139BatteryIndicator'}
$instance=New-Object Threading.Mutex($false,$instanceName)
try{$owned=$instance.WaitOne(0)}catch [Threading.AbandonedMutexException]{$owned=$true}
if(-not $owned){
    try{$show=[Threading.EventWaitHandle]::OpenExisting('Local\PeripheralBatteryMonitor.Show');$show.Set() | Out-Null;$show.Dispose()}catch{}
    $instance.Dispose();exit
}
$eventPrefix=if($SmokeTest){$instanceName}else{'Local\PeripheralBatteryMonitor'}
$exitEvent=New-Object Threading.EventWaitHandle($false,[Threading.EventResetMode]::AutoReset,($eventPrefix+'.Exit'))
$showEvent=New-Object Threading.EventWaitHandle($false,[Threading.EventResetMode]::AutoReset,($eventPrefix+'.Show'))
$packageRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$version=(Get-Content -Raw (Join-Path $packageRoot 'version.json') | ConvertFrom-Json).version
$stateRoot=Join-Path $env:LOCALAPPDATA 'PeripheralBatteryMonitor'
$settingsPath=Join-Path $stateRoot 'settings.json'
$settings=@{AutoStart=$false;LowBatteryAlerts=$true;LowBatteryThreshold=20;CheckForUpdates=$true;ShowUnavailable=$false;TrayStyle='Battery'}
if(Test-Path -LiteralPath $settingsPath){try{
    $saved=Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
    foreach($key in @('AutoStart','LowBatteryAlerts','CheckForUpdates','ShowUnavailable')){if($saved.$key -is [bool]){$settings[$key]=$saved.$key}}
    if($saved.LowBatteryThreshold -is [int] -and $saved.LowBatteryThreshold -ge 5 -and $saved.LowBatteryThreshold -le 50){$settings.LowBatteryThreshold=$saved.LowBatteryThreshold}
    if($saved.TrayStyle -in @('Battery','Percent')){$settings.TrayStyle=$saved.TrayStyle}
}catch{}}
if($SmokeTest){$settings.CheckForUpdates=$false;$settings.LowBatteryAlerts=$false}
$mouse=New-Object Aj139Battery.Monitor
$keyboard=New-Object Aj139Battery.MagicKeyboardMonitor
$discovery=New-Object Aj139Battery.WindowsDeviceMonitor
$context=New-Object Windows.Forms.ApplicationContext
$tray=New-Object Windows.Forms.NotifyIcon
$menu=New-Object Windows.Forms.ContextMenuStrip
$openItem=$menu.Items.Add('Open Peripheral Battery Monitor')
$refreshItem=$menu.Items.Add('Refresh batteries')
$settingsItem=$menu.Items.Add('Settings')
$updatesItem=$menu.Items.Add('Check for updates...')
$menu.Items.Add('-') | Out-Null
$exitItem=$menu.Items.Add('Exit')
$tray.ContextMenuStrip=$menu
$script:paintKey=''
$script:lastUpdateCache=[DateTime]::MinValue
$script:notifiedRelease=''
$script:discoveryVersion=-1
$alerted=@{}
$bg=[Drawing.ColorTranslator]::FromHtml('#10151F')
$cardColor=[Drawing.ColorTranslator]::FromHtml('#1B2432')
$muted=[Drawing.ColorTranslator]::FromHtml('#95A3B7')
$green=[Drawing.ColorTranslator]::FromHtml('#55DEAD')
$blue=[Drawing.ColorTranslator]::FromHtml('#67BBFF')

function New-Label($text,$x,$y,$width,$height,$size=10,$bold=$false){
    $label=New-Object Windows.Forms.Label
    $label.Text=$text;$label.SetBounds($x,$y,$width,$height)
    $style=if($bold){[Drawing.FontStyle]::Bold}else{[Drawing.FontStyle]::Regular}
    $label.Font=New-Object Drawing.Font('Segoe UI',$size,$style)
    $label.ForeColor=[Drawing.Color]::White;$label.BackColor=[Drawing.Color]::Transparent
    return $label
}
function New-Button($text,$x,$y,$width=130){
    $button=New-Object Windows.Forms.Button
    $button.Text=$text;$button.SetBounds($x,$y,$width,34)
    $button.FlatStyle='Flat';$button.FlatAppearance.BorderColor=[Drawing.ColorTranslator]::FromHtml('#3D4D63')
    $button.BackColor=$cardColor;$button.ForeColor=[Drawing.Color]::White
    $button.Font=New-Object Drawing.Font('Segoe UI',9)
    return $button
}
function Start-UpdateCheck([bool]$quiet){
    $arguments='-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+(Join-Path $PSScriptRoot 'Update.ps1')+'"'
    if($quiet){$arguments+=' -Quiet -CheckOnly'}
    Start-Process powershell.exe -WindowStyle Hidden -ArgumentList $arguments
}
function Save-Settings {
    New-Item -ItemType Directory -Path $stateRoot -Force | Out-Null
    $temporary=Join-Path $stateRoot 'settings.pending'
    $settings | ConvertTo-Json | Set-Content -LiteralPath $temporary -Encoding UTF8
    Move-Item -LiteralPath $temporary -Destination $settingsPath -Force
    $run='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    if($settings.AutoStart){
        $installed=Join-Path $env:LOCALAPPDATA 'Programs\PeripheralBatteryMonitor\PeripheralBatteryMonitor.exe'
        if(Test-Path -LiteralPath $installed){$command='"'+$installed+'" --background'}
        else{$command='powershell.exe -NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+$PSCommandPath+'"'}
        New-Item -Path $run -Force | Out-Null
        New-ItemProperty -Path $run -Name PeripheralBatteryMonitor -Value $command -PropertyType String -Force | Out-Null
    }else{Remove-ItemProperty -Path $run -Name PeripheralBatteryMonitor -ErrorAction SilentlyContinue}
    $script:paintKey=''
}

$window=New-Object Windows.Forms.Form
$window.Text='Peripheral Battery Monitor';$window.ClientSize=New-Object Drawing.Size(730,710)
$window.BackColor=$bg;$window.ForeColor=[Drawing.Color]::White
$window.StartPosition='CenterScreen';$window.FormBorderStyle='FixedSingle';$window.MaximizeBox=$false
$window.AutoScroll=$true
$availableHeight=[Windows.Forms.Screen]::PrimaryScreen.WorkingArea.Height-70
if($window.Height -gt $availableHeight){$window.Height=[Math]::Max(480,$availableHeight)}
$window.Controls.Add((New-Label 'Peripheral Battery Monitor' 28 24 640 44 23 $true))
$subtitle=New-Label 'Your devices. One place for battery status.' 30 74 650 26 10
$subtitle.ForeColor=$muted;$window.Controls.Add($subtitle)

function New-DeviceCard($name,$transport,$x,$accent,$provider){
    $panel=New-Object Windows.Forms.Panel;$panel.SetBounds($x,120,325,240);$panel.BackColor=$cardColor
    $heading=New-Label $name 20 18 285 30 14 $true;$panel.Controls.Add($heading)
    $connection=New-Label $transport 20 51 285 23 10;$connection.ForeColor=$accent;$panel.Controls.Add($connection)
    $percentLabel=New-Label '--' 17 82 200 66 36 $true;$panel.Controls.Add($percentLabel)
    $track=New-Object Windows.Forms.Panel;$track.SetBounds(22,158,281,7);$track.BackColor=[Drawing.ColorTranslator]::FromHtml('#334155')
    $fill=New-Object Windows.Forms.Panel;$fill.SetBounds(0,0,0,7);$fill.BackColor=$accent;$track.Controls.Add($fill);$panel.Controls.Add($track)
    $status=New-Label 'Connecting...' 20 180 285 22 9;$status.ForeColor=$muted;$panel.Controls.Add($status)
    $seen=New-Label 'No battery report yet' 20 205 285 22 8;$seen.ForeColor=$muted;$panel.Controls.Add($seen)
    $window.Controls.Add($panel)
    return @{Percent=$percentLabel;Fill=$fill;Status=$status;Seen=$seen;Accent=$accent;Provider=$provider;Name=$name}
}
$mouseCard=New-DeviceCard 'AJ139 V2 MC' '2.4 GHz receiver' 28 $green $mouse
$keyboardCard=New-DeviceCard 'Magic Keyboard' 'Bluetooth' 377 $blue $keyboard
$refreshButton=New-Button 'Refresh batteries' 28 382 150
$settingsButton=New-Button 'Settings' 190 382 105
$updateButton=New-Button 'Check for updates' 307 382 155
$repoButton=New-Button 'GitHub' 474 382 100
$window.Controls.AddRange(@($refreshButton,$settingsButton,$updateButton,$repoButton))
$otherHeading=New-Label 'Other devices' 28 441 360 30 15 $true
$window.Controls.Add($otherHeading)
$discoverySummary=New-Label 'Reading Windows device information...' 28 476 675 25 9
$discoverySummary.ForeColor=$muted;$window.Controls.Add($discoverySummary)
$deviceGrid=New-Object Windows.Forms.DataGridView
$deviceGrid.SetBounds(28,511,675,145);$deviceGrid.ReadOnly=$true;$deviceGrid.AllowUserToAddRows=$false;$deviceGrid.AllowUserToDeleteRows=$false
$deviceGrid.RowHeadersVisible=$false;$deviceGrid.SelectionMode='FullRowSelect';$deviceGrid.AutoSizeColumnsMode='Fill'
$deviceGrid.BackgroundColor=$cardColor;$deviceGrid.BorderStyle='None';$deviceGrid.GridColor=[Drawing.ColorTranslator]::FromHtml('#334155')
$deviceGrid.EnableHeadersVisualStyles=$false;$deviceGrid.ColumnHeadersDefaultCellStyle.BackColor=$cardColor;$deviceGrid.ColumnHeadersDefaultCellStyle.ForeColor=[Drawing.Color]::White
$deviceGrid.DefaultCellStyle.BackColor=$cardColor;$deviceGrid.DefaultCellStyle.ForeColor=[Drawing.Color]::White;$deviceGrid.DefaultCellStyle.SelectionBackColor=[Drawing.ColorTranslator]::FromHtml('#314561')
foreach($column in @(@('Name','Device'),@('Transport','Connection'),@('Battery','Battery'),@('Status','Status'))){$deviceGrid.Columns.Add($column[0],$column[1]) | Out-Null}
$deviceGrid.Columns['Name'].FillWeight=110;$deviceGrid.Columns['Transport'].FillWeight=70;$deviceGrid.Columns['Battery'].FillWeight=45;$deviceGrid.Columns['Status'].FillWeight=180
$window.Controls.Add($deviceGrid)
$footer=New-Label "Version $version  |  Specific reports + automatic Windows device discovery" 30 674 675 24 9
$footer.ForeColor=$muted;$window.Controls.Add($footer)
$window.Add_FormClosing({param($sender,$eventArgs) if($eventArgs.CloseReason -eq 'UserClosing'){$eventArgs.Cancel=$true;$window.Hide()}})

function Show-Settings {
    $dialog=New-Object Windows.Forms.Form
    $dialog.Text='Settings';$dialog.ClientSize=New-Object Drawing.Size(460,390)
    $dialog.BackColor=$bg;$dialog.ForeColor=[Drawing.Color]::White;$dialog.StartPosition='CenterParent'
    $dialog.FormBorderStyle='FixedDialog';$dialog.MaximizeBox=$false;$dialog.MinimizeBox=$false
    $dialog.Controls.Add((New-Label 'Make it yours' 24 20 410 34 19 $true))
    $auto=New-Object Windows.Forms.CheckBox;$auto.Text='Start when I sign in to Windows';$auto.SetBounds(28,74,400,26);$auto.Checked=$settings.AutoStart
    $alerts=New-Object Windows.Forms.CheckBox;$alerts.Text='Notify me when a battery is low';$alerts.SetBounds(28,110,400,26);$alerts.Checked=$settings.LowBatteryAlerts
    $thresholdLabel=New-Label 'Low battery threshold (%)' 48 149 235 26 10
    $threshold=New-Object Windows.Forms.NumericUpDown;$threshold.SetBounds(306,146,95,28);$threshold.Minimum=5;$threshold.Maximum=50;$threshold.Value=$settings.LowBatteryThreshold
    $checks=New-Object Windows.Forms.CheckBox;$checks.Text='Check for new releases at startup (once a day)';$checks.SetBounds(28,189,420,26);$checks.Checked=$settings.CheckForUpdates
    $unavailable=New-Object Windows.Forms.CheckBox;$unavailable.Text='Show detected devices without battery data';$unavailable.SetBounds(28,225,420,26);$unavailable.Checked=$settings.ShowUnavailable
    $styleLabel=New-Label 'Tray appearance' 28 272 190 28 10
    $style=New-Object Windows.Forms.ComboBox;$style.SetBounds(226,269,175,28);$style.DropDownStyle='DropDownList';$style.Items.AddRange(@('Battery rings','Lowest percentage'));$style.SelectedIndex=if($settings.TrayStyle -eq 'Percent'){1}else{0}
    foreach($check in @($auto,$alerts,$checks,$unavailable)){$check.ForeColor=[Drawing.Color]::White;$check.Font=New-Object Drawing.Font('Segoe UI',10)}
    $save=New-Button 'Save settings' 268 334 150
    $dialog.Controls.AddRange(@($auto,$alerts,$thresholdLabel,$threshold,$checks,$unavailable,$styleLabel,$style,$save))
    $save.Add_Click({
        try{
            $settings.AutoStart=$auto.Checked;$settings.LowBatteryAlerts=$alerts.Checked
            $settings.LowBatteryThreshold=[int]$threshold.Value;$settings.CheckForUpdates=$checks.Checked
            $settings.ShowUnavailable=$unavailable.Checked;$script:discoveryVersion=-1
            $settings.TrayStyle=if($style.SelectedIndex -eq 1){'Percent'}else{'Battery'}
            Save-Settings;$dialog.DialogResult='OK';$dialog.Close()
        }catch{[Windows.Forms.MessageBox]::Show($_.Exception.Message,'Could not save settings') | Out-Null}
    })
    $dialog.ShowDialog($window) | Out-Null;$dialog.Dispose()
}
function Show-Window {$window.Show();if($window.WindowState -eq 'Minimized'){$window.WindowState='Normal'};$window.Activate()}
function Refresh-All {$mouse.Refresh();$keyboard.Refresh();$discovery.Refresh()}
$openItem.Add_Click({Show-Window});$settingsItem.Add_Click({Show-Window;Show-Settings})
$refreshItem.Add_Click({Refresh-All});$updatesItem.Add_Click({Start-UpdateCheck $false})
$exitItem.Add_Click({$context.ExitThread()})
$refreshButton.Add_Click({Refresh-All});$settingsButton.Add_Click({Show-Settings})
$updateButton.Add_Click({Start-UpdateCheck $false})
$repoButton.Add_Click({Start-Process 'https://github.com/vhnatovskyi/peripheral-battery-monitor'})
$tray.Add_DoubleClick({Show-Window})

function Draw-Tray($mouseState,$keyboardState,$otherDevices){
    $confirmed=@($otherDevices | Where-Object {$_.Connected -eq $true -and $null -ne $_.Percent})
    $values=@($mouseState.Percent,$keyboardState.Percent)+@($confirmed | ForEach-Object Percent) | Where-Object {$null -ne $_}
    $bitmap=New-Object Drawing.Bitmap(32,32)
    $graphics=[Drawing.Graphics]::FromImage($bitmap);$graphics.SmoothingMode='AntiAlias'
    $back=New-Object Drawing.Pen([Drawing.ColorTranslator]::FromHtml('#425168'),3)
    $graphics.DrawEllipse($back,3,3,26,26)
    $pens=@()
    $parts=New-Object Collections.ArrayList
    $parts.Add(@($mouseState,$green)) | Out-Null;$parts.Add(@($keyboardState,$blue)) | Out-Null
    foreach($other in $confirmed){$parts.Add(@($other,[Drawing.Color]::MediumPurple)) | Out-Null}
    $segment=360.0/$parts.Count;$index=0
    foreach($part in $parts){
        if($null -ne $part[0].Percent){
            $color=if($part[0].Percent -le $settings.LowBatteryThreshold){[Drawing.Color]::OrangeRed}else{$part[1]}
            $pen=New-Object Drawing.Pen($color,3);$pens+=$pen
            $gap=[Math]::Min(12,$segment/3)
            $graphics.DrawArc($pen,3,3,26,26,[single](100+$index*$segment),[single](($segment-$gap)*[double]$part[0].Percent/100))
        }
        $index++
    }
    if($settings.TrayStyle -eq 'Percent'){
        $text=if($values.Count -gt 0){[string](($values | Measure-Object -Minimum).Minimum)}else{'?'}
        $font=New-Object Drawing.Font('Segoe UI',10,[Drawing.FontStyle]::Bold,[Drawing.GraphicsUnit]::Pixel)
        $format=New-Object Drawing.StringFormat;$format.Alignment='Center';$format.LineAlignment='Center'
        $graphics.DrawString($text,$font,[Drawing.Brushes]::White,[Drawing.RectangleF]::new(1,1,30,30),$format)
        $font.Dispose();$format.Dispose()
    }else{
        $outline=New-Object Drawing.Pen([Drawing.Color]::White,1.7)
        $graphics.DrawRectangle($outline,10,11,12,11);$graphics.DrawLine($outline,14,9,18,9)
        if($values.Count -gt 0){$level=[Math]::Max(1,[int](8*(($values | Measure-Object -Minimum).Minimum)/100));$graphics.FillRectangle([Drawing.Brushes]::White,12,(20-$level),8,$level)}
        $outline.Dispose()
    }
    $handle=$bitmap.GetHicon();$temporary=[Drawing.Icon]::FromHandle($handle);$icon=$temporary.Clone();$temporary.Dispose();[PbmIcon]::DestroyIcon($handle) | Out-Null
    $previous=$tray.Icon;$tray.Icon=$icon;$window.Icon=$icon
    if($previous){$previous.Dispose()}
    $mouseText=if($null -ne $mouseState.Percent){"$($mouseState.Percent)%"}else{'unavailable'}
    $keyboardText=if($null -ne $keyboardState.Percent){"$($keyboardState.Percent)%"}else{'unavailable'}
    $tip="Mouse: $mouseText | Keyboard: $keyboardText"
    if($confirmed.Count -gt 0){$tip+=" | +$($confirmed.Count)"}
    $tray.Text=$tip.Substring(0,[Math]::Min(63,$tip.Length));$tray.Visible=$true
    foreach($pen in $pens){$pen.Dispose()};$back.Dispose();$graphics.Dispose();$bitmap.Dispose()
}
function Update-Views {
    if($exitEvent.WaitOne(0)){$context.ExitThread();return}
    if($showEvent.WaitOne(0)){Show-Window}
    $mouseState=$mouse.Snapshot();$keyboardState=$keyboard.Snapshot()
    $otherDevices=@($discovery.Snapshot())
    $key="$($mouseState.Version):$($keyboardState.Version):$($discovery.Version):$($settings.TrayStyle):$($settings.LowBatteryThreshold)"
    if($key -ne $script:paintKey){Draw-Tray $mouseState $keyboardState $otherDevices;$script:paintKey=$key}
    if($script:discoveryVersion -ne $discovery.Version){
        $script:discoveryVersion=$discovery.Version;$deviceGrid.Rows.Clear()
        foreach($device in $otherDevices){
            if(-not $settings.ShowUnavailable -and $null -eq $device.Percent){continue}
            $batteryText=if($null -ne $device.Percent){"$($device.Percent)%"}else{'Unavailable'}
            $deviceGrid.Rows.Add($device.Name,$device.Transport,$batteryText,$device.Status) | Out-Null
        }
        $withBattery=@($otherDevices | Where-Object {$null -ne $_.Percent}).Count
        $discoverySummary.Text="$($otherDevices.Count) detected / $withBattery with battery data. Windows cache checked every 15 seconds."
    }
    foreach($device in $otherDevices){
        if($device.Connected -ne $true -or $null -eq $device.Percent){continue}
        $alertKey='generic:'+$device.Id
        if($device.Percent -gt ($settings.LowBatteryThreshold+5)){$alerted[$alertKey]=$false}
        if($settings.LowBatteryAlerts -and $device.Percent -le $settings.LowBatteryThreshold -and -not $alerted[$alertKey]){
            $tray.ShowBalloonTip(5000,'Low battery',"$($device.Name): $($device.Percent)%",[Windows.Forms.ToolTipIcon]::Warning);$alerted[$alertKey]=$true
        }
    }
    foreach($pair in @(@($mouseCard,$mouseState),@($keyboardCard,$keyboardState))){
        $card=$pair[0];$state=$pair[1]
        $card.Percent.Text=if($null -ne $state.Percent){"$($state.Percent)%"}else{'--'}
        $card.Fill.Width=if($null -ne $state.Percent){[int](281*$state.Percent/100)}else{0}
        $card.Fill.BackColor=if($null -ne $state.Percent -and $state.Percent -le $settings.LowBatteryThreshold){[Drawing.Color]::OrangeRed}else{$card.Accent}
        $card.Status.Text=if($state.Online){'Connected'}else{$state.Status}
        $card.Seen.Text=if($state.UpdatedUtc -gt [DateTime]::MinValue){'Last report: '+$state.UpdatedUtc.ToLocalTime().ToString('HH:mm:ss')}else{'No battery report yet'}
        if($null -ne $state.Percent){
            if($state.Percent -gt ($settings.LowBatteryThreshold+5)){$alerted[$card.Name]=$false}
            if($settings.LowBatteryAlerts -and $state.Percent -le $settings.LowBatteryThreshold -and -not $alerted[$card.Name]){
                $tray.ShowBalloonTip(5000,'Low battery',"$($card.Name): $($state.Percent)%",[Windows.Forms.ToolTipIcon]::Warning);$alerted[$card.Name]=$true
            }
        }
    }
    if(([DateTime]::UtcNow-$script:lastUpdateCache).TotalSeconds -ge 30){
        $script:lastUpdateCache=[DateTime]::UtcNow
        $cache=Join-Path $stateRoot 'release.json'
        if($settings.CheckForUpdates -and (Test-Path -LiteralPath $cache)){try{
            $release=Get-Content -Raw -LiteralPath $cache | ConvertFrom-Json
            if($release.available -and [version]$release.version -gt [version]$version -and $script:notifiedRelease -ne $release.version){
                $script:notifiedRelease=$release.version;$updatesItem.Text="Install version $($release.version)..."
                $tray.ShowBalloonTip(5000,'Update available',"Version $($release.version) is available. Open the menu to install it.",[Windows.Forms.ToolTipIcon]::Info)
            }
        }catch{}}
    }
}
$timer=New-Object Windows.Forms.Timer;$timer.Interval=100
$script:smokeTicks=0;$script:smokeError=$null
$timer.Add_Tick({
    if(-not $SmokeTest){Update-Views;return}
    try{
        Update-Views;$script:smokeTicks++
        if($script:smokeTicks -ge 12){
            $samples=@(1..24 | ForEach-Object {[pscustomobject]@{Percent=50;Connected=$true}})
            Draw-Tray ($mouse.Snapshot()) ($keyboard.Snapshot()) $samples
            if(-not $tray.Visible -or $deviceGrid.Columns.Count -ne 4){throw 'UI initialization failed'}
            $context.ExitThread()
        }
    }catch{$script:smokeError=$_;$context.ExitThread()}
})
try{
    $mouse.Start();$keyboard.Start();$discovery.Start();Update-Views;$timer.Start()
    if($ShowWindow){Show-Window}
    if($settings.CheckForUpdates){Start-UpdateCheck $true}
    [Windows.Forms.Application]::Run($context)
    if($SmokeTest -and $script:smokeError){throw $script:smokeError}
}finally{
    $timer.Stop();$timer.Dispose();$tray.Visible=$false
    $mouse.Dispose();$keyboard.Dispose();$discovery.Dispose();$window.Dispose()
    if($tray.Icon){$tray.Icon.Dispose()};$tray.Dispose();$menu.Dispose();$context.Dispose()
    $exitEvent.Dispose();$showEvent.Dispose();$instance.ReleaseMutex();$instance.Dispose()
}
