#Requires -Version 5.1
[CmdletBinding()]
param([switch]$ShowWindow,[switch]$SmokeTest)
$ErrorActionPreference='Stop'
if(-not $SmokeTest){
    $entryRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
}
$desktop=Join-Path $PSScriptRoot '../../app/PeripheralBatteryMonitor.Desktop.exe'
if((-not $SmokeTest) -and (Test-Path -LiteralPath $desktop)){
    Import-Module (Join-Path $entryRoot 'installer/Runtime.psm1') -Force
    $dotnetRoot=Initialize-PbmRuntime
    $env:DOTNET_ROOT_X64=$dotnetRoot
    $env:DOTNET_ROOT=$dotnetRoot
    $agent=Join-Path ([IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($desktop))) 'PeripheralBatteryMonitor.Agent.exe'
    if(Test-Path -LiteralPath $agent){$desktop=$agent}
    $start=@{FilePath=[IO.Path]::GetFullPath($desktop);WorkingDirectory=[IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($desktop));WindowStyle='Hidden'}
    if(-not $ShowWindow){$start.ArgumentList='--background'}elseif($desktop -eq $agent){$start.ArgumentList='--show-ui'}
    Start-Process @start
    exit
}
Add-Type -Path @(Get-ChildItem (Join-Path $PSScriptRoot '../Core/*.cs'),(Join-Path $PSScriptRoot '../Devices/*.cs') | ForEach-Object FullName)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type -Path @(Get-ChildItem (Join-Path $PSScriptRoot '../UI/*.cs') | ForEach-Object FullName) -ReferencedAssemblies System.Windows.Forms,System.Drawing
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
Import-Module (Join-Path $PSScriptRoot 'Localization.psm1') -Force
$settings=@{Language='uk';TrayMode='Combined';AutoStart=$false;LowBatteryAlerts=$true;LowBatteryThreshold=20;CheckForUpdates=$true;ShowUnavailable=$false;TrayStyle='Battery'}
if(Test-Path -LiteralPath $settingsPath){try{
    $saved=Get-Content -Raw -LiteralPath $settingsPath | ConvertFrom-Json
    foreach($key in @('AutoStart','LowBatteryAlerts','CheckForUpdates','ShowUnavailable')){if($saved.$key -is [bool]){$settings[$key]=$saved.$key}}
    if($saved.Language -in @('uk','en')){$settings.Language=$saved.Language}
    if($saved.TrayMode -in @('Combined','Separate')){$settings.TrayMode=$saved.TrayMode}
    if($saved.LowBatteryThreshold -is [int] -and $saved.LowBatteryThreshold -ge 5 -and $saved.LowBatteryThreshold -le 50){$settings.LowBatteryThreshold=$saved.LowBatteryThreshold}
    if($saved.TrayStyle -in @('Battery','Percent')){$settings.TrayStyle=$saved.TrayStyle}
}catch{}}
if($SmokeTest){$settings.CheckForUpdates=$false;$settings.LowBatteryAlerts=$false}
function L([string]$text){Get-PbmText $text $settings.Language}
$deviceTrays=@{}
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
    $arguments='-NoProfile -STA -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+(Join-Path $PSScriptRoot 'Update.ps1')+'" -Language '+$settings.Language
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
try{$window.Icon=[Drawing.Icon]::ExtractAssociatedIcon((Join-Path $packageRoot 'PeripheralBatteryMonitor.exe'))}catch{}
$window.Text='Peripheral Battery Monitor';$window.ClientSize=New-Object Drawing.Size(552,326)
$window.BackColor=$bg;$window.ForeColor=[Drawing.Color]::White
$window.StartPosition='CenterScreen';$window.FormBorderStyle='FixedSingle';$window.MaximizeBox=$false
$window.AutoScroll=$false
$availableHeight=[Windows.Forms.Screen]::PrimaryScreen.WorkingArea.Height-70
if($window.Height -gt $availableHeight){$window.Height=[Math]::Max(480,$availableHeight)}
$window.Controls.Add((New-Label 'Devices' 24 47 230 30 13 $true))

$toolbar=New-Object Windows.Forms.ToolStrip
$toolbar.Dock='Top';$toolbar.GripStyle='Hidden';$toolbar.AutoSize=$true
$toolbar.RenderMode='System';$toolbar.BackColor=[Drawing.SystemColors]::Control
$toolbar.ForeColor=[Drawing.SystemColors]::ControlText;$toolbar.Font=[Drawing.SystemFonts]::MenuFont
function New-ToolButton($text,$width){
    $button=New-Object Windows.Forms.ToolStripButton
    $button.Text=$text;$button.AutoSize=$true;$button.Padding=New-Object Windows.Forms.Padding(8,4,8,4)
    $button.DisplayStyle='Text';$toolbar.Items.Add($button) | Out-Null;return $button
}
$refreshButton=New-ToolButton 'Refresh' 104
$settingsButton=New-ToolButton 'Settings' 142
$updateButton=New-ToolButton 'Updates' 104
$aboutButton=New-ToolButton 'About' 128
$window.Controls.Add($toolbar)
$discoverySummary=New-Label '' 254 49 274 26 9
$discoverySummary.TextAlign='MiddleRight'
$discoverySummary.ForeColor=$muted;$window.Controls.Add($discoverySummary)
$cardsPanel=New-Object Windows.Forms.Panel
$cardsPanel.SetBounds(24,87,504,142);$cardsPanel.AutoScroll=$false
$window.Controls.Add($cardsPanel)
$emptyState=New-Label 'No devices connected' 18 34 468 50 15 $true
$emptyState.TextAlign='MiddleCenter';$emptyState.ForeColor=$muted;$cardsPanel.Controls.Add($emptyState)
$script:deviceCards=@{};$script:cardOrder=@();$script:cardPage=0;$script:layoutKey=''
$previousPage=New-Button '<' 390 300 32;$nextPage=New-Button '>' 496 300 32
$pageLabel=New-Label '' 428 306 62 22 9;$pageLabel.TextAlign='MiddleCenter'
$window.Controls.AddRange(@($previousPage,$pageLabel,$nextPage))
function Update-CardLayout {
    $area=[Windows.Forms.Screen]::FromControl($window).WorkingArea
    $rowsAvailable=[Math]::Max(1,[int][Math]::Floor(($area.Height-234)/142))
    $capacity=$rowsAvailable*2
    $pages=[Math]::Max(1,[int][Math]::Ceiling($script:cardOrder.Count/[double]$capacity))
    $script:cardPage=[Math]::Min($script:cardPage,$pages-1)
    $layoutKey=($script:cardOrder -join '|')+':'+$script:cardPage+':'+$capacity
    if($script:layoutKey -eq $layoutKey){return};$script:layoutKey=$layoutKey
    $visibleCount=[Math]::Min($capacity,[Math]::Max(0,$script:cardOrder.Count-$script:cardPage*$capacity))
    $rows=[Math]::Max(1,[int][Math]::Ceiling($visibleCount/2.0))
    $navigationHeight=if($pages -gt 1){36}else{0}
    $window.ClientSize=New-Object Drawing.Size(552,(99+$rows*142+$navigationHeight))
    $cardsPanel.Height=$rows*142
    foreach($control in @($previousPage,$nextPage)){$control.Top=$window.ClientSize.Height-36;$control.Visible=($pages -gt 1)}
    $pageLabel.Top=$window.ClientSize.Height-30;$pageLabel.Visible=($pages -gt 1);$pageLabel.Text=('{0} / {1}' -f ($script:cardPage+1),$pages)
    $previousPage.Enabled=$script:cardPage -gt 0;$nextPage.Enabled=$script:cardPage -lt ($pages-1)
    for($i=0;$i -lt $script:cardOrder.Count;$i++){
        $view=$script:deviceCards[$script:cardOrder[$i]].View
        $position=$i-$script:cardPage*$capacity
        $onPage=($position -ge 0 -and $position -lt $capacity)
        $view.Visible=$onPage
        if($onPage){$view.Location=New-Object Drawing.Point(($position%2)*258),([int][Math]::Floor($position/2.0)*142)}
    }
    $emptyState.Visible=$script:cardOrder.Count -eq 0
    if($window.Visible -and $window.Bottom -gt $area.Bottom){$window.Top=[Math]::Max($area.Top,$area.Bottom-$window.Height)}
}
$previousPage.Add_Click({$script:cardPage--;Update-CardLayout})
$nextPage.Add_Click({$script:cardPage++;Update-CardLayout})
function New-DeviceCard($name,$transport,$accent){
    $view=New-Object PeripheralBatteryMonitor.UI.DeviceCard
    $view.DisconnectButton.Add_Click({param($sender) Disconnect-Device $sender.Tag})
    $cardsPanel.Controls.Add($view)
    return @{View=$view}
}
function Disconnect-Device($device){
    if($script:disconnectTask -and -not $script:disconnectTask.IsCompleted){return}
    if($device.Transport -notmatch 'Bluetooth'){return}
    $address=$device.Address
    if(-not $address){$address=$discovery.ResolveBluetoothAddress($device.Id,$device.Name)}
    if(-not $address){Start-Process 'ms-settings:bluetooth';return}
    $message=(L 'Disconnect {0} from Windows? Pairing will be kept. The device may reconnect automatically.') -f $device.Name
    if([Windows.Forms.MessageBox]::Show($window,$message,(L 'Disconnect Bluetooth device'),'YesNo','Question') -ne 'Yes'){return}
    $script:disconnectTask=[Aj139Battery.BluetoothConnection]::DisconnectAsync($address)
}
function Sync-DeviceCards($devices){
    $active=@{};$cardsPanel.SuspendLayout()
    try{
        foreach($device in $devices){
            $active[$device.Id]=$true
            if(-not $script:deviceCards.ContainsKey($device.Id)){
                $script:deviceCards[$device.Id]=New-DeviceCard $device.Name $device.Transport $device.Accent
                $script:deviceCards[$device.Id].AddressVersion=-1
            }
            $card=$script:deviceCards[$device.Id]
            if($card.AddressVersion -ne $discovery.Version){
                $card.Address=if(-not $SmokeTest -and $device.Transport -match 'Bluetooth'){$discovery.ResolveBluetoothAddress($device.Id,$device.Name)}else{$null}
                $card.AddressVersion=$discovery.Version
            }
            $card.View.DisconnectButton.Tag=[pscustomobject]@{Id=$device.Id;Name=$device.Name;Transport=$device.Transport;Address=$card.Address}
            $card.View.SetDisconnectStatus(($device.Transport -match 'Bluetooth' -and -not [string]::IsNullOrWhiteSpace($card.Address)),(L 'Disconnect'))
            $seen=if($device.Updated -gt [DateTime]::MinValue){((L 'Updated {0}') -f $device.Updated.ToLocalTime().ToString('HH:mm:ss'))}else{L 'Battery report unavailable'}
            $color=if($null -ne $device.Percent -and $device.Percent -le $settings.LowBatteryThreshold){[Drawing.Color]::OrangeRed}else{$device.Accent}
            $card.View.SetData($device.Name,(L $device.Transport),$device.Percent,(L $device.Status),$seen,$color)
            $card.View.SetLabels((L 'Connected'),(L 'Low battery'))
        }
        foreach($id in @($script:deviceCards.Keys)){
            if(-not $active.ContainsKey($id)){$card=$script:deviceCards[$id];$cardsPanel.Controls.Remove($card.View);$card.View.Dispose();$script:deviceCards.Remove($id)}
        }
        $script:cardOrder=@($devices | ForEach-Object Id)
        Update-CardLayout
    }finally{$cardsPanel.ResumeLayout()}
}
function Show-About {
    $dialog=New-Object Windows.Forms.Form
    $dialog.Text='About Peripheral Battery Monitor';$dialog.ClientSize=New-Object Drawing.Size(430,378)
    $dialog.BackColor=$bg;$dialog.ForeColor=[Drawing.Color]::White;$dialog.StartPosition='CenterParent'
    $dialog.FormBorderStyle='FixedDialog';$dialog.MaximizeBox=$false;$dialog.MinimizeBox=$false
    $dialog.Controls.Add((New-Label 'Peripheral Battery Monitor' 24 24 384 34 17 $true))
    $details=New-Label (((L 'Version {0}') -f $version)+[Environment]::NewLine+[Environment]::NewLine+(L 'Developed by Vladyslav Hnatovskyi')+[Environment]::NewLine+(L 'Battery status for your wireless devices.')) 26 75 378 108 10
    $details.ForeColor=$muted;$dialog.Controls.Add($details)
    $profile=New-Object Windows.Forms.LinkLabel;$profile.Text='Developer on GitHub';$profile.SetBounds(26,191,200,26);$profile.LinkColor=$blue;$profile.ActiveLinkColor=$green
    $profile.Add_LinkClicked({Start-Process 'https://github.com/vhnatovskyi'})
    $repository=New-Object Windows.Forms.LinkLabel;$repository.Text='Source code and releases';$repository.SetBounds(26,223,300,26);$repository.LinkColor=$blue;$repository.ActiveLinkColor=$green
    $repository.Add_LinkClicked({Start-Process 'https://github.com/vhnatovskyi/peripheral-battery-monitor'})
    $linkedin=New-Object Windows.Forms.LinkLabel;$linkedin.Text='LinkedIn';$linkedin.SetBounds(26,255,300,26);$linkedin.LinkColor=$blue;$linkedin.ActiveLinkColor=$green
    $linkedin.Add_LinkClicked({Start-Process 'https://www.linkedin.com/in/vladyslav-hnatovskyi/'})
    $email=New-Object Windows.Forms.LinkLabel;$email.Text='vhnatovskyi@hotmail.com';$email.SetBounds(26,287,300,26);$email.LinkColor=$blue;$email.ActiveLinkColor=$green
    $email.Add_LinkClicked({Start-Process 'mailto:vhnatovskyi@hotmail.com'})
    $close=New-Button 'Close' 307 326 96;$close.Add_Click({$dialog.Close()});$dialog.CancelButton=$close
    $dialog.Controls.AddRange(@($profile,$repository,$linkedin,$email,$close))
    Set-PbmControlLanguage $dialog $settings.Language;$dialog.ShowDialog($window) | Out-Null;$dialog.Dispose()
}
$window.Add_FormClosing({param($sender,$eventArgs) if($eventArgs.CloseReason -eq 'UserClosing'){$eventArgs.Cancel=$true;$window.Hide()}})

function New-SettingsCombo($items,$x,$y,$width){
    $combo=New-Object PeripheralBatteryMonitor.UI.SettingsComboBox
    $combo.SetBounds($x,$y,$width,28);$combo.DropDownStyle='DropDownList';$combo.FlatStyle='Flat'
    $combo.BackColor=[Drawing.ColorTranslator]::FromHtml('#29384B');$combo.ForeColor=[Drawing.Color]::White
    $combo.Font=New-Object Drawing.Font('Segoe UI',9)
    $combo.Items.AddRange([object[]]$items);return $combo
}
function Show-Settings {
    param([switch]$ForTest)
    $dialog=New-Object Windows.Forms.Form
    $dialog.Text=L 'Settings';$dialog.ClientSize=New-Object Drawing.Size(552,442)
    $dialog.BackColor=$bg;$dialog.ForeColor=[Drawing.Color]::White;$dialog.StartPosition='CenterParent'
    $dialog.Font=New-Object Drawing.Font('Segoe UI',9)
    $dialog.FormBorderStyle='FixedDialog';$dialog.MaximizeBox=$false;$dialog.MinimizeBox=$false
    $dialog.Controls.Add((New-Label (L 'Settings') 24 18 504 36 20 $true))
    $intro=New-Label (L 'Appearance, language and notifications.') 26 57 504 22 9;$intro.ForeColor=$muted;$dialog.Controls.Add($intro)
    $general=New-Object Windows.Forms.Panel;$general.SetBounds(24,93,246,194);$general.BackColor=$cardColor
    $appearance=New-Object Windows.Forms.Panel;$appearance.SetBounds(282,93,246,194);$appearance.BackColor=$cardColor
    $notifications=New-Object Windows.Forms.Panel;$notifications.SetBounds(24,301,504,78);$notifications.BackColor=$cardColor
    $general.Controls.Add((New-Label (L 'General') 16 12 212 25 11 $true))
    $general.Controls.Add((New-Label (L 'Language') 16 43 212 20 9))
    $language=New-SettingsCombo @('Українська','English') 16 66 212
    $language.SelectedIndex=if($settings.Language -eq 'en'){1}else{0}
    $auto=New-Object Windows.Forms.CheckBox;$auto.Text=L 'Start with Windows';$auto.SetBounds(16,111,212,25);$auto.Checked=$settings.AutoStart
    $checks=New-Object Windows.Forms.CheckBox;$checks.Text=L 'Check for releases daily';$checks.SetBounds(16,145,212,32);$checks.Checked=$settings.CheckForUpdates
    $general.Controls.AddRange(@($language,$auto,$checks))
    $appearance.Controls.Add((New-Label (L 'Tray icons') 16 12 212 25 11 $true))
    $appearance.Controls.Add((New-Label (L 'Tray mode') 16 43 212 20 9))
    $mode=New-SettingsCombo @((L 'Combined (up to 4)'),(L 'Separate icons')) 16 66 212
    $mode.SelectedIndex=if($settings.TrayMode -eq 'Separate'){1}else{0}
    $appearance.Controls.Add((New-Label (L 'Tray appearance') 16 111 212 20 9))
    $style=New-SettingsCombo @((L 'Battery and percentage'),(L 'Percentage emphasis')) 16 134 212
    $style.SelectedIndex=if($settings.TrayStyle -eq 'Percent'){1}else{0}
    $appearance.Controls.AddRange(@($mode,$style))
    $alerts=New-Object Windows.Forms.CheckBox;$alerts.Text=L 'Low battery notifications';$alerts.SetBounds(16,13,310,26);$alerts.Checked=$settings.LowBatteryAlerts
    $hint=New-Label (L 'Warn when charge reaches the threshold.') 16 43 310 21 8;$hint.ForeColor=$muted
    $thresholdLabel=New-Label (L 'Threshold (%)') 354 10 134 20 9;$thresholdLabel.ForeColor=$muted
    $threshold=New-Object Windows.Forms.NumericUpDown;$threshold.SetBounds(354,35,126,28)
    $threshold.Minimum=5;$threshold.Maximum=50;$threshold.Value=$settings.LowBatteryThreshold
    $threshold.BackColor=[Drawing.ColorTranslator]::FromHtml('#29384B');$threshold.ForeColor=[Drawing.Color]::White;$threshold.BorderStyle='FixedSingle'
    $threshold.Enabled=$alerts.Checked;$alerts.Add_CheckedChanged({$threshold.Enabled=$alerts.Checked})
    $notifications.Controls.AddRange(@($alerts,$hint,$thresholdLabel,$threshold))
    foreach($check in @($auto,$checks,$alerts)){$check.ForeColor=[Drawing.Color]::White;$check.Font=New-Object Drawing.Font('Segoe UI',9)}
    $save=New-Button (L 'Save settings') 366 396 162
    $cancel=New-Button (L 'Cancel') 244 396 110;$cancel.Add_Click({$dialog.Close()});$dialog.CancelButton=$cancel
    $dialog.Controls.AddRange(@($general,$appearance,$notifications,$save,$cancel));$dialog.AcceptButton=$save
    $save.Add_Click({
        try{
            $settings.AutoStart=$auto.Checked;$settings.LowBatteryAlerts=$alerts.Checked
            $settings.LowBatteryThreshold=[int]$threshold.Value;$settings.CheckForUpdates=$checks.Checked
            $settings.TrayStyle=if($style.SelectedIndex -eq 1){'Percent'}else{'Battery'}
            $settings.TrayMode=if($mode.SelectedIndex -eq 1){'Separate'}else{'Combined'}
            $settings.Language=if($language.SelectedIndex -eq 1){'en'}else{'uk'}
            Save-Settings
            Set-PbmControlLanguage $window $settings.Language;Set-PbmControlLanguage $menu $settings.Language
            $dialog.DialogResult='OK';$dialog.Close()
        }catch{[Windows.Forms.MessageBox]::Show($_.Exception.Message,(L 'Could not save settings')) | Out-Null}
    })
    if($ForTest){return $dialog}
    $dialog.ShowDialog($window) | Out-Null;$dialog.Dispose()
}
function Show-Window {$window.Show();if($window.WindowState -eq 'Minimized'){$window.WindowState='Normal'};$window.Activate()}
function Refresh-All {$mouse.Refresh();$keyboard.Refresh();$discovery.Refresh()}
$openItem.Add_Click({Show-Window});$settingsItem.Add_Click({Show-Window;Show-Settings})
$refreshItem.Add_Click({Refresh-All});$updatesItem.Add_Click({Start-UpdateCheck $false})
$exitItem.Add_Click({$context.ExitThread()})
$refreshButton.Add_Click({Refresh-All});$settingsButton.Add_Click({Show-Settings})
$updateButton.Add_Click({Start-UpdateCheck $false})
$aboutButton.Add_Click({Show-About})
$tray.Add_DoubleClick({Show-Window})

function New-BatteryIcon($devices){
    $levels=[int[]]@(foreach($device in @($devices | Select-Object -First 4)){if($null -ne $device.Percent){[int]$device.Percent}else{-1}})
    $colors=[Drawing.Color[]]@($devices | Select-Object -First 4 | ForEach-Object Accent)
    $size=[Windows.Forms.SystemInformation]::SmallIconSize.Width
    $bitmap=[PeripheralBatteryMonitor.UI.TrayBatteryPainter]::Render($levels,$colors,($settings.TrayMode -eq 'Combined'),($settings.TrayStyle -eq 'Percent'),$size)
    try{
        $handle=$bitmap.GetHicon();$temporary=[Drawing.Icon]::FromHandle($handle)
        try{return $temporary.Clone()}finally{$temporary.Dispose();[PbmIcon]::DestroyIcon($handle) | Out-Null}
    }finally{$bitmap.Dispose()}
}
function Get-TrayTip($devices){
    $lines=@(foreach($device in @($devices | Select-Object -First 4)){
        $connection=if($device.Transport -match 'Bluetooth'){'BT'}elseif($device.Transport -match '2.4'){'2.4G'}else{'USB'}
        $state=if($null -ne $device.Percent){"$($device.Percent)%"}else{'--'}
        $suffix=": $state / $connection"
        $maxName=31-$suffix.Length
        $name=if($device.Name.Length -gt $maxName){$device.Name.Substring(0,$maxName-1)+[char]0x2026}else{$device.Name}
        $name+$suffix
    })
    if(-not $lines.Count){return L 'No devices connected'}
    return $lines -join "`n"
}
function Set-TrayImage($target,$devices){
    $icon=New-BatteryIcon $devices;$previous=$target.Icon;$target.Icon=$icon
    $tip=Get-TrayTip $devices;$target.Text=$tip.Substring(0,[Math]::Min(63,$tip.Length));$target.Visible=$true
    [PeripheralBatteryMonitor.UI.TrayTooltip]::Set($target,$tip) | Out-Null
    if($previous){$previous.Dispose()}
}
function Draw-Tray($devices){
    $active=@{}
    if($settings.TrayMode -eq 'Separate' -and $devices.Count -gt 0){
        $tray.Visible=$false
        foreach($device in $devices){
            $active[$device.Id]=$true
            if(-not $deviceTrays.ContainsKey($device.Id)){
                $item=New-Object Windows.Forms.NotifyIcon;$item.ContextMenuStrip=$menu;$item.Add_DoubleClick({Show-Window});$deviceTrays[$device.Id]=$item
            }
            Set-TrayImage $deviceTrays[$device.Id] @($device)
        }
    }else{Set-TrayImage $tray @($devices | Select-Object -First 4)}
    foreach($id in @($deviceTrays.Keys)){if(-not $active.ContainsKey($id)){
        $item=$deviceTrays[$id];$item.Visible=$false;if($item.Icon){$item.Icon.Dispose()};$item.Dispose();$deviceTrays.Remove($id)
    }}
}
function Update-Views {
    if($exitEvent.WaitOne(0)){$context.ExitThread();return}
    if($showEvent.WaitOne(0)){Show-Window}
    if($script:disconnectTask -and $script:disconnectTask.IsCompleted){
        try{$code=$script:disconnectTask.GetAwaiter().GetResult()}catch{$code=-1}
        $script:disconnectTask=$null;$discovery.Refresh()
        if($code -ne 0){
            $message=(L 'Windows could not disconnect this device (error {0}). Open Bluetooth settings?') -f $code
            if([Windows.Forms.MessageBox]::Show($window,$message,(L 'Disconnect'),'YesNo','Information') -eq 'Yes'){Start-Process 'ms-settings:bluetooth'}
        }
    }
    $mouseState=$mouse.Snapshot();$keyboardState=$keyboard.Snapshot()
    $otherDevices=@($discovery.Snapshot())
    $visible=New-Object Collections.ArrayList
    if($mouseState.Online){$visible.Add([pscustomobject]@{Id='aj139';Name='AJ139 V2 MC';Transport='2.4 GHz receiver';Percent=$mouseState.Percent;Status=$mouseState.Status;Updated=$mouseState.UpdatedUtc;Accent=$green}) | Out-Null}
    if($keyboardState.Online){$visible.Add([pscustomobject]@{Id='magic-keyboard';Name='Magic Keyboard';Transport='Bluetooth';Percent=$keyboardState.Percent;Status=$keyboardState.Status;Updated=$keyboardState.UpdatedUtc;Accent=$blue}) | Out-Null}
    foreach($device in $otherDevices){
        if($device.Connected -ne $true){continue}
        $visible.Add([pscustomobject]@{Id=$device.Id;Name=$device.Name;Transport=$device.Transport;Percent=$device.Percent;Status=$device.Status;Updated=$device.CheckedUtc;Accent=[Drawing.Color]::MediumPurple}) | Out-Null
    }
    $key=(@($visible | ForEach-Object {"$($_.Id):$($_.Percent):$($_.Accent)"}) -join '|')+":$($settings.TrayStyle):$($settings.TrayMode):$($settings.Language):$($settings.LowBatteryThreshold)"
    if($key -ne $script:paintKey){Draw-Tray $visible;$script:paintKey=$key}
    $notificationIcon=if($tray.Visible){$tray}else{@($deviceTrays.Values)[0]}
    Sync-DeviceCards $visible
    $discoverySummary.Text=(L '{0} connected') -f $visible.Count
    foreach($device in $visible){
        if($null -eq $device.Percent){continue}
        if($device.Percent -gt ($settings.LowBatteryThreshold+5)){$alerted[$device.Id]=$false}
        if($settings.LowBatteryAlerts -and $device.Percent -le $settings.LowBatteryThreshold -and -not $alerted[$device.Id]){
            $notificationIcon.ShowBalloonTip(5000,(L 'Low battery'),"$($device.Name): $($device.Percent)%",[Windows.Forms.ToolTipIcon]::Warning);$alerted[$device.Id]=$true
        }
    }
    if(([DateTime]::UtcNow-$script:lastUpdateCache).TotalSeconds -ge 30){
        $script:lastUpdateCache=[DateTime]::UtcNow
        $cache=Join-Path $stateRoot 'release.json'
        if($settings.CheckForUpdates -and (Test-Path -LiteralPath $cache)){try{
            $release=Get-Content -Raw -LiteralPath $cache | ConvertFrom-Json
            if($release.available -and [version]$release.version -gt [version]$version -and $script:notifiedRelease -ne $release.version){
                $script:notifiedRelease=$release.version;$updatesItem.Text=(L 'Install version {0}...') -f $release.version
                $notificationIcon.ShowBalloonTip(5000,(L 'Update available'),((L 'Version {0} is available. Open Updates to install it.') -f $release.version),[Windows.Forms.ToolTipIcon]::Info)
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
            $timer.Stop()
            $samples=@(1..24 | ForEach-Object {[pscustomobject]@{Percent=50;Connected=$true}})
            $traySamples=@(1..4 | ForEach-Object {[pscustomobject]@{Id="sample-$_";Name="Sample device $_";Transport='Bluetooth';Percent=25*$_;Accent=$blue}})
            Draw-Tray $traySamples
            $tip=Get-TrayTip $traySamples
            if(($tip -split "`n").Count -ne 4 -or $tip.Length -gt 127){throw 'Four-device tooltip failed'}
            $settings.TrayMode='Separate';Draw-Tray $traySamples
            if($deviceTrays.Count -ne 4 -or $tray.Visible){throw 'Separate tray mode failed'}
            $settings.TrayMode='Combined';Draw-Tray $traySamples
            if($deviceTrays.Count -ne 0 -or -not $tray.Visible){throw 'Combined tray mode cleanup failed'}
            $fixture=@([pscustomobject]@{Id='test-one';Name='Test headset';Transport='Bluetooth';Percent=83;Status='Connected';Updated=[DateTime]::UtcNow;Accent=$blue},[pscustomobject]@{Id='test-two';Name='Test mouse';Transport='Receiver';Percent=97;Status='Connected';Updated=[DateTime]::UtcNow;Accent=$green})
            Sync-DeviceCards $fixture
            if($deviceCards.Count -ne 2){throw 'Dynamic card creation failed'}
            if($toolbar.Dock -ne 'Top' -or $toolbar.RenderMode -ne 'System'){throw 'System toolbar initialization failed'}
            if($deviceCards['test-one'].View.CanDisconnect -or $deviceCards['test-two'].View.CanDisconnect){throw 'Disconnect action shown without a real Bluetooth address'}
            $deviceCards['test-one'].View.SetDisconnectStatus($true,(L 'Disconnect'))
            if(-not $deviceCards['test-one'].View.CanDisconnect -or $deviceCards['test-one'].View.DisconnectButton.ImageAlign -ne 'MiddleCenter'){throw 'Supported disconnect icon initialization failed'}
            if($deviceCards['test-one'].View.DisconnectButton.AccessibleRole -ne 'PushButton'){throw 'Disconnect button is inaccessible'}
            $settingsDialog=Show-Settings -ForTest
            try{
                if($env:PBM_SETTINGS_PREVIEW){$settingsDialog.Show();[Windows.Forms.Application]::DoEvents()}
                $settingsBitmap=New-Object Drawing.Bitmap($settingsDialog.Width,$settingsDialog.Height)
                try{
                    $settingsDialog.DrawToBitmap($settingsBitmap,[Drawing.Rectangle]::new(0,0,$settingsDialog.Width,$settingsDialog.Height))
                    if($env:PBM_SETTINGS_PREVIEW){$settingsBitmap.Save($env:PBM_SETTINGS_PREVIEW,[Drawing.Imaging.ImageFormat]::Png)}
                }finally{$settingsBitmap.Dispose()}
                foreach($panel in @($settingsDialog.Controls | Where-Object {$_ -is [Windows.Forms.Panel]})){
                    foreach($control in $panel.Controls){if($control.Right -gt $panel.Width -or $control.Bottom -gt $panel.Height){throw 'Settings control extends beyond section'}}
                }
            }finally{$settingsDialog.Dispose()}
            if($window.AutoScroll -or $cardsPanel.AutoScroll){throw 'Dashboard must not scroll'}
            if($deviceCards['test-two'].View.Left -ne 258){throw 'Hidden-window layout failed'}
            foreach($card in $deviceCards.Values){
                $bitmap=New-Object Drawing.Bitmap(286,154)
                try{$card.View.DrawToBitmap($bitmap,[Drawing.Rectangle]::new(0,0,286,154))}finally{$bitmap.Dispose()}
            }
            Sync-DeviceCards @($fixture[1])
            if($deviceCards.Count -ne 1 -or $deviceCards.ContainsKey('test-one')){throw 'Disconnected card removal failed'}
            Sync-DeviceCards @()
            if($deviceCards.Count -ne 0 -or -not $tray.Visible){throw 'UI initialization failed'}
            $context.ExitThread()
        }
    }catch{$script:smokeError=$_;$context.ExitThread()}
})
try{
    Set-PbmControlLanguage $window $settings.Language;Set-PbmControlLanguage $menu $settings.Language
    if(-not $SmokeTest){
        $mouse.Start();$keyboard.Start();$discovery.Start()
        $bluetoothWorker=[PowerShell]::Create()
        $bluetoothWorker.AddCommand((Join-Path $PSScriptRoot '../Devices/BluetoothWorker.ps1')).AddArgument($discovery) | Out-Null
        $bluetoothAsync=$bluetoothWorker.BeginInvoke()
    }
    Update-Views;$timer.Start()
    if($ShowWindow){Show-Window}
    if($settings.CheckForUpdates){Start-UpdateCheck $true}
    [Windows.Forms.Application]::Run($context)
    if($SmokeTest -and $script:smokeError){throw $script:smokeError}
}finally{
    $timer.Stop();$timer.Dispose();$tray.Visible=$false
    foreach($item in $deviceTrays.Values){$item.Visible=$false;if($item.Icon){$item.Icon.Dispose()};$item.Dispose()}
    $mouse.Dispose();$keyboard.Dispose();$discovery.Dispose()
    if($bluetoothWorker){$bluetoothWorker.Stop();$bluetoothWorker.Dispose()}
    $window.Dispose()
    if($tray.Icon){$tray.Icon.Dispose()};$tray.Dispose();$menu.Dispose();$context.Dispose()
    $exitEvent.Dispose();$showEvent.Dispose();$instance.ReleaseMutex();$instance.Dispose()
}
