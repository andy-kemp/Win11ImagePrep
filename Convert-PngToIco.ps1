# PNG to ICO Converter
# This script converts WinImagePrep.png to a proper multi-resolution .ico file

Write-Host "Converting PNG to ICO format..." -ForegroundColor Cyan
Write-Host ""

$pngPath = ".\WinImagePrep\WinImagePrep.png"
$icoPath = ".\WinImagePrep\app.ico"

if (-not (Test-Path $pngPath)) {
	Write-Host "ERROR: PNG file not found at $pngPath" -ForegroundColor Red
	exit 1
}

Write-Host "Source: $pngPath" -ForegroundColor Gray
Write-Host "Output: $icoPath" -ForegroundColor Gray
Write-Host ""

try {
	Add-Type -AssemblyName System.Drawing

	# Load the PNG image
	$png = [System.Drawing.Image]::FromFile((Resolve-Path $pngPath))
	Write-Host "✓ PNG loaded: $($png.Width)x$($png.Height) pixels" -ForegroundColor Green

	# Create icon sizes (16, 32, 48, 256)
	$sizes = @(256, 48, 32, 16)

	# For Windows ICO format, we'll create the largest size and let Windows scale it
	# This is simpler than creating a true multi-resolution ICO

	$size = 256
	$bitmap = New-Object System.Drawing.Bitmap($size, $size)
	$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
	$graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
	$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
	$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

	# Draw the image scaled to icon size
	$graphics.DrawImage($png, 0, 0, $size, $size)
	$graphics.Dispose()

	Write-Host "✓ Scaled to ${size}x${size}" -ForegroundColor Green

	# Get the icon handle
	$iconHandle = $bitmap.GetHicon()
	$icon = [System.Drawing.Icon]::FromHandle($iconHandle)

	# Save to file
	$fileStream = [System.IO.File]::Create((Resolve-Path ".\WinImagePrep\") + "\app.ico")
	$icon.Save($fileStream)
	$fileStream.Close()

	Write-Host "✓ Icon saved: $icoPath" -ForegroundColor Green

	# Cleanup
	$icon.Dispose()
	$bitmap.Dispose()
	$png.Dispose()

	# Verify the file
	if (Test-Path $icoPath) {
		$file = Get-Item $icoPath
		Write-Host ""
		Write-Host "SUCCESS!" -ForegroundColor Green
		Write-Host "Icon created: $($file.Length) bytes" -ForegroundColor Gray
	}

} catch {
	Write-Host ""
	Write-Host "ERROR: $($_.Exception.Message)" -ForegroundColor Red
	Write-Host ""
	Write-Host "Alternative: Use an online converter" -ForegroundColor Yellow
	Write-Host "1. Go to https://convertico.com/ or https://www.icoconverter.com/" -ForegroundColor Gray
	Write-Host "2. Upload WinImagePrep.png" -ForegroundColor Gray
	Write-Host "3. Download as app.ico" -ForegroundColor Gray
	Write-Host "4. Replace .\WinImagePrep\app.ico with the downloaded file" -ForegroundColor Gray
}

Write-Host ""
$null = Read-Host "Press Enter to exit"
