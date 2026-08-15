Add-Type -AssemblyName System.Drawing

$icoPath = "D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM\EDM\Models\edm logo.ico"
$sizes = @(16, 32, 48, 128)
$destDirs = @(
    "D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM\extension\chrome",
    "D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM\extension\chrome\icons",
    "D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM\extension\firefox",
    "D:\Project 2\10 AUG - 2.07AM\5 AUG\EDM\extension\firefox\icons"
)

foreach ($dir in $destDirs) {
    if (-not (Test-Path -Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
    }
}

$ico = New-Object System.Drawing.Icon($icoPath)

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.DrawIcon($ico, [System.Drawing.Rectangle]::new(0, 0, $size, $size))
    $g.Dispose()

    foreach ($dir in $destDirs) {
        $outFile = Join-Path -Path $dir -ChildPath "icon$($size).png"
        $bmp.Save($outFile, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Output "Generated: $outFile"
    }
    $bmp.Dispose()
}

$ico.Dispose()
Write-Output "All extension icons generated successfully!"
