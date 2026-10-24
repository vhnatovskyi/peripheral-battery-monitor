$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source=[Drawing.Bitmap]::FromFile((Join-Path $root 'assets/branding/app-icon.png'))
$images=@()
try{
    $left=$source.Width;$top=$source.Height;$right=0;$bottom=0
    for($y=0;$y -lt $source.Height;$y++){for($x=0;$x -lt $source.Width;$x++){
        if($source.GetPixel($x,$y).A -ge 32){$left=[Math]::Min($left,$x);$top=[Math]::Min($top,$y);$right=[Math]::Max($right,$x);$bottom=[Math]::Max($bottom,$y)}
    }}
    $side=[Math]::Max($right-$left+1,$bottom-$top+1)
    $crop=[Drawing.Rectangle]::new([int](($left+$right+1-$side)/2),[int](($top+$bottom+1-$side)/2),$side,$side)
    foreach($size in @(16,20,24,32,40,48,64,128,256)){
        $bitmap=New-Object Drawing.Bitmap($size,$size)
        $graphics=[Drawing.Graphics]::FromImage($bitmap)
        $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($source,[Drawing.Rectangle]::new(0,0,$size,$size),$crop,[Drawing.GraphicsUnit]::Pixel)
        $graphics.Dispose()
        $stream=New-Object IO.MemoryStream
        $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
        $images+=@{Size=$size;Bytes=$stream.ToArray()}
        $stream.Dispose();$bitmap.Dispose()
    }
    $file=[IO.File]::Create((Join-Path $root 'assets/branding/app-icon.ico'))
    $writer=New-Object IO.BinaryWriter($file)
    try{
        $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$images.Count)
        $offset=6+16*$images.Count
        foreach($entry in $images){
            $dimension=if($entry.Size -eq 256){0}else{$entry.Size}
            $writer.Write([byte]$dimension);$writer.Write([byte]$dimension)
            $writer.Write([byte]0);$writer.Write([byte]0)
            $writer.Write([uint16]1);$writer.Write([uint16]32)
            $writer.Write([uint32]$entry.Bytes.Length);$writer.Write([uint32]$offset)
            $offset+=$entry.Bytes.Length
        }
        foreach($entry in $images){$writer.Write([byte[]]$entry.Bytes)}
    }finally{$writer.Dispose()}
}finally{$source.Dispose()}
