$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -Path (Join-Path $PSScriptRoot '../src/UI/TrayBatteryPainter.cs') -ReferencedAssemblies System.Drawing
function Assert($condition,$message){if(-not $condition){throw $message}}
$colors=[Drawing.Color[]]@([Drawing.ColorTranslator]::FromHtml('#55DEAD'),[Drawing.ColorTranslator]::FromHtml('#67BBFF'),[Drawing.Color]::MediumPurple,[Drawing.Color]::Orange)
foreach($combined in @($false,$true)){
    $levels=if($combined){[int[]]@(97,100,82,65)}else{[int[]]@(97)}
    $image=[PeripheralBatteryMonitor.UI.TrayBatteryPainter]::Render($levels,$colors,$combined,$false)
    try{
        $green=0;$blue=0;$caption=0
        for($y=0;$y -lt 32;$y++){for($x=0;$x -lt 32;$x++){
            $pixel=$image.GetPixel($x,$y)
            if($pixel.A -gt 100){
                if($pixel.G -gt $pixel.R+30 -and $pixel.G -gt $pixel.B){$green++}
                if($pixel.B -gt $pixel.R+30 -and $pixel.B -gt $pixel.G){$blue++}
            }
            $captionArea=if($combined){$x -ge 6 -and $x -lt 27 -and $y -ge 6 -and $y -lt 27}else{$y -ge 18}
            if($captionArea -and $pixel.A -gt 100 -and $pixel.R -gt 220 -and $pixel.G -gt 220 -and $pixel.B -gt 220){$caption++}
        }}
        Assert ($green -gt 0) 'Device accent disappeared from indicator'
        Assert ($caption -gt 0) 'Percentage label missing from readable caption area'
        if($combined){Assert ($blue -gt 0) 'Combined circle lost a device section color'}
    }finally{$image.Dispose()}
}
foreach($level in @(0,100,-1)){
    $image=[PeripheralBatteryMonitor.UI.TrayBatteryPainter]::Render([int[]]@($level),$colors,$false,$true)
    $image.Dispose()
}
$native=[PeripheralBatteryMonitor.UI.TrayBatteryPainter]::Render([int[]]@(100),$colors,$false,$false,16)
try{
    Assert ($native.Width -eq 16) 'Native tray size was not used'
    $captionPixels=0
    for($y=6;$y -lt 16;$y++){for($x=0;$x -lt 16;$x++){$pixel=$native.GetPixel($x,$y);if($pixel.A -gt 200 -and $pixel.R -eq 255 -and $pixel.G -eq 255 -and $pixel.B -eq 255){$captionPixels++}}}
    Assert ($captionPixels -gt 15) 'Native-size charge digits are too small or missing'
}finally{$native.Dispose()}
'Tray colors, circle sections and percentage placement regression tests passed'
$light=[PeripheralBatteryMonitor.UI.TrayBatteryPainter]::Render([int[]]@(79),$colors,$false,$false,16,$true)
try{
    Assert ($light.GetPixel(0,15).A -eq 0) 'Separate icon gained an opaque background box'
    $darkCaption=0
    for($y=9;$y -lt 16;$y++){for($x=0;$x -lt 16;$x++){$p=$light.GetPixel($x,$y);if($p.A -eq 255 -and $p.R -lt 60 -and $p.G -lt 60 -and $p.B -lt 60){$darkCaption++}}}
    Assert ($darkCaption -gt 15) 'Light-taskbar caption does not have dark contrast'
}finally{$light.Dispose()}
foreach($size in @(16,20,24,32)){
    foreach($combined in @($false,$true)){
        foreach($value in @(0,82,93,100,-1)){
            $image=[PeripheralBatteryMonitor.UI.TrayBatteryPainter]::Render([int[]]@($value),$colors,$combined,$false,$size)
            try{
                $white=0;$left=$size;$right=0
                for($y=0;$y -lt $size;$y++){for($x=0;$x -lt $size;$x++){
                    $pixel=$image.GetPixel($x,$y)
                    if($pixel.A -eq 255 -and $pixel.R -eq 255 -and $pixel.G -eq 255 -and $pixel.B -eq 255){$white++;$left=[Math]::Min($left,$x);$right=[Math]::Max($right,$x)}
                }}
                Assert ($white -ge 10) 'Native tray caption lost its solid pixels'
                Assert ($left -ge 0 -and $right -lt $size) 'Native charge caption was clipped'
                if($value -eq 100){Assert (($right-$left+1) -ge 10) '100 lost one or more digits'}
            }finally{$image.Dispose()}
        }
    }
}
