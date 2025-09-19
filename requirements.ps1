# Set up paths
$ExtractPath = "$PSScriptRoot\ToyBoxx\ffmpeg"

$FFMpegUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2024-08-31-12-50/ffmpeg-n7.0.2-6-g7e69129d2f-win64-gpl-shared-7.0.zip"
$FfmpegDir = "ffmpeg-n7.0.2-6-g7e69129d2f-win64-gpl-shared-7.0"
$SoundTouchUrl = "https://www.surina.net/soundtouch/soundtouch_dll-2.1.1.zip"

function Invoke-ZipDownload($Url, $ZipPath, $ExtractPath) {
    # Create the extraction directory if it doesn't exist
    if (-Not (Test-Path $ExtractPath)) {
        New-Item -ItemType Directory -Path $ExtractPath | Out-Null
    }

    Write-Host "Downloading from $Url ..."
    Invoke-WebRequest -Uri $Url -OutFile $ZipPath

    Write-Host "Extracting to $ExtractPath ..."
    Expand-Archive -Path $ZipPath -DestinationPath $ExtractPath -Force

    Write-Host "Cleaning up..."
    Remove-Item $ZipPath
}

# Download and set up ffmpeg if not already present
if (-Not (Test-Path "$ExtractPath\bin\ffmpeg.exe")) {
    Invoke-ZipDownload $FFMpegUrl "$ExtractPath\ffmpeg.zip" $ExtractPath

    # Move the contents of the extracted ffmpeg folder to the main folder
    Move-Item -Path "$ExtractPath\$FfmpegDir\*" -Destination $ExtractPath -Force

    # Remove the now-empty extracted folder
    Remove-Item "$ExtractPath\$FfmpegDir" -Recurse -Force
}

# Download and set up SoundTouch DLL if not already present
if (-Not (Test-Path "$ExtractPath\bin\SoundTouch.dll")) {
    Invoke-ZipDownload $SoundTouchUrl "$ExtractPath\soundtouch.zip" "$ExtractPath\bin"

    # Rename original DLL and replace with the x64 version
    Rename-Item -Path "$ExtractPath\bin\SoundTouch.dll" -NewName "SoundTouch.dll.orig"
    Copy-Item -Path "$ExtractPath\bin\SoundTouch_x64.dll" -Destination "$ExtractPath\bin\SoundTouch.dll"
}
