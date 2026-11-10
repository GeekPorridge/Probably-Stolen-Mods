$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$project=Split-Path $PSScriptRoot
$catalog=Join-Path $project 'Design/v2-art-exports.json'
$records=Get-Content -LiteralPath $catalog -Raw | ConvertFrom-Json
foreach($spec in @(
    @{Key='egg';Width=1;Height=2;Atlas='V2-props.png'},
    @{Key='mayonnaise';Width=2;Height=2;Atlas='V2-props.png'}
)){
    $record=@($records | Where-Object key -eq $spec.Key)
    if($record.Count -ne 1){throw 'Ambiguous sprite record.'}
    $record=$record[0];$record.width=$spec.Width;$record.height=$spec.Height
    $source=[Drawing.Bitmap]::new((Join-Path $project ('ArtOriginals/'+$spec.Atlas)))
    $target=[Drawing.Bitmap]::new(128*$spec.Width,128*$spec.Height,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics=[Drawing.Graphics]::FromImage($target)
    try{
        $graphics.Clear([Drawing.Color]::Transparent)
        $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
        $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::Half
        $box=$record.source_box
        $scale=[Math]::Min(($target.Width-12)/$box[2],($target.Height-12)/$box[3])
        $width=[int][Math]::Round($box[2]*$scale);$height=[int][Math]::Round($box[3]*$scale)
        $dest=[Drawing.Rectangle]::new([int](($target.Width-$width)/2),[int](($target.Height-$height)/2),$width,$height)
        $crop=[Drawing.Rectangle]::new($box[0],$box[1],$box[2],$box[3])
        $graphics.DrawImage($source,$dest,$crop,[Drawing.GraphicsUnit]::Pixel)
        $target.Save((Join-Path $PSScriptRoot ('Items/'+$spec.Key+'.png')),[Drawing.Imaging.ImageFormat]::Png)
        Write-Output ($spec.Key+': '+$target.Width+'x'+$target.Height)
    }finally{$graphics.Dispose();$target.Dispose();$source.Dispose()}
}
[IO.File]::WriteAllText($catalog,($records | ConvertTo-Json -Depth 5),[Text.UTF8Encoding]::new($false))
