$devDir = "C:\Dev"
$jomUrl = "https://download.qt.io/official_releases/jom/jom_1_1_3.zip"

# Check for Administrator privileges
if (!([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Warning "This script requires Administrator privileges to install OpenSSL."
    Write-Warning "Please run as Administrator."
    break
}

# 1. Jom
$jomDir = Join-Path $devDir "Jom"
if (-not (Test-Path "$jomDir\jom.exe")) {
    Write-Host "Downloading Jom..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $jomUrl -OutFile "$devDir\jom.zip"
    Expand-Archive -Path "$devDir\jom.zip" -DestinationPath $jomDir -Force
    Remove-Item "$devDir\jom.zip"
    Write-Host "Jom installed." -ForegroundColor Green
}
else {
    Write-Host "Jom already present." -ForegroundColor Green
}

# 2. OpenSSL
$openSslUrl = "https://slproweb.com/download/Win64OpenSSL-3_0_5.msi"
$openSslInstaller = "$devDir\openssl.msi"
$openSslDest = Join-Path $devDir "OpenSSL"

if (-not (Test-Path "$openSslDest\include")) {
    Write-Host "Downloading OpenSSL MSI..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $openSslUrl -OutFile $openSslInstaller
    
    Write-Host "Installing OpenSSL to $openSslDest (Passive mode)..." -ForegroundColor Cyan
    # /quiet /mn - passive, no restart. DIR property sets install location.
    $proc = Start-Process msiexec.exe -ArgumentList "/i `"$openSslInstaller`" /quiet /norestart DIR=`"$openSslDest`"" -PassThru -Wait
    
    if ($proc.ExitCode -eq 0) {
        # Double check if include exists (sometimes generic path is used regardless of DIR)
        if (Test-Path "$openSslDest\include") {
            Write-Host "OpenSSL installed successfully." -ForegroundColor Green
            Remove-Item $openSslInstaller
        }
        else {
            Write-Warning "OpenSSL installer finished but C:\Dev\OpenSSL\include is missing."
            Write-Warning "It might have installed to 'C:\Program Files\OpenSSL-Win64'. Please check and move files manually if needed."
        }
    }
    else {
        Write-Error "OpenSSL installation failed (Exit Code: $($proc.ExitCode)). Try installing manually: $openSslInstaller"
    }
}
else {
    Write-Host "OpenSSL seems to be present." -ForegroundColor Green
}

# 3. Clone Qt
$qtDir = "$devDir\Qt\5.15.9"
if (-not (Test-Path $qtDir)) {
    New-Item -ItemType Directory -Path $qtDir -Force | Out-Null
}

Set-Location $qtDir
# Ensure git is available
if (Get-Command git -ErrorAction SilentlyContinue) {
    if (-not (Test-Path ".git")) {
        Write-Host "Cloning Qt5 git repo (this might take 5-10 minutes)..." -ForegroundColor Cyan
        git clone git://code.qt.io/qt/qt5.git .
        git checkout 5.15
        
        Write-Host "Initializing repository (downloading submodules)..." -ForegroundColor Cyan
        # Check if perl is available
        if (Get-Command perl -ErrorAction SilentlyContinue) {
            perl init-repository
            Write-Host "Qt5 source ready." -ForegroundColor Green
        }
        else {
            Write-Error "Perl not found! Cannot run init-repository. Please ensure Perl is in PATH."
        }
    }
    else {
        Write-Host "Qt5 repo already exists in $qtDir" -ForegroundColor Green
    }
}
else {
    Write-Error "Git not found! Cannot clone Qt."
}

Write-Host "Dependencies setup complete." -ForegroundColor Green
