#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'Aj139Monitor.cs')
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
Add-Type 'using System; using System.Runtime.InteropServices; public class AjIcon { [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr h); }'
$ajMutex = New-Object Threading.Mutex($false, 'Local\AJ139BatteryIndicator')
if (-not $ajMutex.WaitOne(0)) { $ajMutex.Dispose(); exit }
$ajTray = New-Object Windows.Forms.NotifyIcon
$ajTray.Text = 'AJ139 V2 MC'
$ajMenu = New-Object Windows.Forms.ContextMenuStrip
$ajRefresh = $ajMenu.Items.Add('Refresh battery')
$ajExit = $ajMenu.Items.Add('Exit')
$ajTray.ContextMenuStrip = $ajMenu
$ajContext = New-Object Windows.Forms.ApplicationContext
$ajMonitor = New-Object Aj139Battery.Monitor
$script:ajVersion = -1
function Update-AjBattery {
    $ajState = $ajMonitor.Snapshot()
    if ($script:ajVersion -eq $ajState.Version) { return }
    $script:ajVersion = $ajState.Version
    $ajLabel = '?'
    $ajTip = "AJ139: $($ajState.Status)"
    $ajColor = [Drawing.Color]::Silver
    if ($ajState.Online -and $null -ne $ajState.Percent) {
        $ajValue = $ajState.Percent
        $ajLabel = "$ajValue"
        $ajTip = "AJ139 V2 MC: $ajValue% (2.4 GHz)"
        $ajColor = if ($ajValue -le 20) { [Drawing.Color]::OrangeRed } else { [Drawing.Color]::LimeGreen }
    }
    $ajBitmap = New-Object Drawing.Bitmap(32,32)
    $ajGraphics = [Drawing.Graphics]::FromImage($ajBitmap)
    $ajGraphics.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $ajFont = New-Object Drawing.Font('Segoe UI', 15, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
    $ajBrush = New-Object Drawing.SolidBrush($ajColor)
    $ajFormat = New-Object Drawing.StringFormat
    $ajFormat.Alignment = [Drawing.StringAlignment]::Center
    $ajFormat.LineAlignment = [Drawing.StringAlignment]::Center
    $ajGraphics.DrawString($ajLabel, $ajFont, $ajBrush, [Drawing.RectangleF]::new(0,0,32,32), $ajFormat)
    $ajHandle = $ajBitmap.GetHicon()
    $ajTemporary = [Drawing.Icon]::FromHandle($ajHandle)
    $ajNewIcon = $ajTemporary.Clone()
    $ajTemporary.Dispose()
    [AjIcon]::DestroyIcon($ajHandle) | Out-Null
    $ajOldIcon = $ajTray.Icon
    $ajTray.Icon = $ajNewIcon
    if ($ajOldIcon) { $ajOldIcon.Dispose() }
    $ajTray.Text = $ajTip
    $ajTray.Visible = $true
    $ajFormat.Dispose(); $ajBrush.Dispose(); $ajFont.Dispose(); $ajGraphics.Dispose(); $ajBitmap.Dispose()
}
$ajTimer = New-Object Windows.Forms.Timer
# Only render state received by the background listener. No battery polling.
$ajTimer.Interval = 100
$ajTimer.Add_Tick({ Update-AjBattery })
$ajRefresh.Add_Click({ $ajMonitor.Refresh() })
$ajExit.Add_Click({ $ajContext.ExitThread() })
$ajTray.Add_DoubleClick({ $ajMonitor.Refresh() })
try {
    $ajMonitor.Start()
    Update-AjBattery
    $ajTimer.Start()
    [Windows.Forms.Application]::Run($ajContext)
} finally {
    $ajTimer.Stop(); $ajTimer.Dispose(); $ajTray.Visible = $false; $ajMonitor.Dispose()
    if ($ajTray.Icon) { $ajTray.Icon.Dispose() }
    $ajTray.Dispose(); $ajMenu.Dispose(); $ajContext.Dispose()
    $ajMutex.ReleaseMutex(); $ajMutex.Dispose()
}
