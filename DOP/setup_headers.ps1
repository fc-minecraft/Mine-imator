$devDir = "C:\Dev"

# Function to download and extract
function Install-Dep($name, $url, $destPath, $extractDirName) {
    Write-Host "Installing $name..." -ForegroundColor Cyan
    $fileName = [System.IO.Path]::GetFileName($url)
    $downloadPath = Join-Path $devDir $fileName
    
    # Create destination parent directory
    $parentDir = (Split-Path $destPath -Parent)
    if (-not (Test-Path $parentDir)) {
        New-Item -Path $parentDir -ItemType Directory -Force | Out-Null
    }

    if (-not (Test-Path $destPath)) {
        Write-Host "Downloading $url..."
        Invoke-WebRequest -Uri $url -OutFile $downloadPath
        
        Write-Host "Extracting $fileName..."
        # Use tar for tar.gz/bz2/xz files
        # -C changes directory to extract to
        tar -xf $downloadPath -C $parentDir
        
        Remove-Item $downloadPath
        Write-Host "$name installed." -ForegroundColor Green
    }
    else {
        Write-Host "$name already present." -ForegroundColor Yellow
    }
}

# 1. OpenAL Soft
# Expects: C:\Dev\OpenAL\openal-soft-1.22.0\include
Install-Dep "OpenAL" "https://openal-soft.org/openal-releases/openal-soft-1.22.0.tar.bz2" "$devDir\OpenAL\openal-soft-1.22.0" "openal-soft-1.22.0"

# 2. Libzip
# Expects: C:\Dev\Libzip\libzip-1.9.2\lib
Install-Dep "Libzip" "https://libzip.org/download/libzip-1.9.2.tar.gz" "$devDir\Libzip\libzip-1.9.2" "libzip-1.9.2"

# 3. FreeType
# Expects: C:\Dev\FreeType\freetype-2.9.1\include
Install-Dep "FreeType" "https://download.savannah.gnu.org/releases/freetype/freetype-2.9.1.tar.gz" "$devDir\FreeType\freetype-2.9.1" "freetype-2.9.1"

# 4. FFmpeg
# Expects: C:\Dev\FFmpeg\ffmpeg-5.0
Install-Dep "FFmpeg" "https://www.ffmpeg.org/releases/ffmpeg-5.0.tar.xz" "$devDir\FFmpeg\ffmpeg-5.0" "ffmpeg-5.0"
