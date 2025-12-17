# Check for Administrator privileges
if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Warning "This script requires Administrator privileges to create C:\Dev and install packages."
    Write-Warning "Please right-click PowerShell and select 'Run as Administrator', then run this script again."
    break
}

# 1. Create directory C:\Dev
$devDir = "C:\Dev"
if (-not (Test-Path -Path $devDir)) {
    Write-Host "Creating $devDir..." -ForegroundColor Cyan
    New-Item -ItemType Directory -Path $devDir | Out-Null
} else {
    Write-Host "$devDir already exists." -ForegroundColor Green
}

# 2. Set environment variable DEV_DIR
[System.Environment]::SetEnvironmentVariable('DEV_DIR', $devDir, [System.EnvironmentVariableTarget]::User)
[System.Environment]::SetEnvironmentVariable('DEV_DIR', $devDir, [System.EnvironmentVariableTarget]::Machine)
$env:DEV_DIR = $devDir
Write-Host "Environment variable DEV_DIR set to $devDir" -ForegroundColor Green

# 3. Utilities check and install using winget
function Install-Tool ($name, $id) {
    if (Get-Command $name -ErrorAction SilentlyContinue) {
        Write-Host "$name is already installed." -ForegroundColor Green
    } else {
        Write-Host "Installing $name..." -ForegroundColor Cyan
        winget install --id $id -e --source winget --accept-package-agreements --accept-source-agreements
    }
}

Write-Host "Checking dependency tools..." -ForegroundColor Cyan
Install-Tool "cmake" "Kitware.CMake"
Install-Tool "perl" "StrawberryPerl.StrawberryPerl"
Install-Tool "python" "Python.Python.3.12"
Install-Tool "nasm" "NASM.NASM"

# 4. Create subfolders required by BUILD.md
$subfolders = @("OpenSSL", "Jom", "Qt", "Libzip", "FreeType", "FFmpeg", "OpenAL")
foreach ($folder in $subfolders) {
    $path = Join-Path $devDir $folder
    if (-not (Test-Path -Path $path)) {
        New-Item -ItemType Directory -Path $path | Out-Null
    }
}

Write-Host "`nSetup complete!" -ForegroundColor Green
Write-Host "IMPORTANT: You must restart your terminal or Visual Studio Code for PATH and Env Var changes to take effect." -ForegroundColor Yellow
Write-Host "Next Step: You will need to open 'x64 Native Tools Command Prompt for VS 2022' to perform the C++ build steps." -ForegroundColor Yellow
