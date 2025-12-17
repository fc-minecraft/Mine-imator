$devDir = "C:\Dev"

# 0. Fix Perl Path for this session
$perlPath = "C:\Strawberry\perl\bin"
if (Test-Path $perlPath) {
    $env:PATH = "$perlPath;$env:PATH"
    if (Get-Command perl -ErrorAction SilentlyContinue) {
        Write-Host "Perl detected: $(perl --version | Select-Object -First 1)" -ForegroundColor Green
    }
}
else {
    Write-Warning "Could not find Strawberry Perl at $perlPath. Please ensure it is installed."
}

# 1. OpenSSL (Try v3.3.2)
$openSslUrl = "https://slproweb.com/download/Win64OpenSSL-3_3_2.msi"
$openSslInstaller = "$devDir\openssl.msi"
$openSslDest = Join-Path $devDir "OpenSSL"

if (-not (Test-Path "$openSslDest\include")) {
    Write-Host "Downloading OpenSSL MSI ($openSslUrl)..." -ForegroundColor Cyan
    try {
        Invoke-WebRequest -Uri $openSslUrl -OutFile $openSslInstaller
        
        Write-Host "Installing OpenSSL to $openSslDest..." -ForegroundColor Cyan
        $proc = Start-Process msiexec.exe -ArgumentList "/i `"$openSslInstaller`" /quiet /norestart DIR=`"$openSslDest`"" -PassThru -Wait
        
        if ($proc.ExitCode -eq 0) {
            Write-Host "OpenSSL installed successfully." -ForegroundColor Green
            Remove-Item $openSslInstaller
        }
        else {
            Write-Error "OpenSSL installation failed (Exit Code: $($proc.ExitCode))."
        }
    }
    catch {
        Write-Warning "Failed to download OpenSSL automatically. Please manually download Win64 OpenSSL v3.x MSI from https://slproweb.com/products/Win32OpenSSL.html"
        Write-Warning "Install it to C:\Dev\OpenSSL (ensure 'bin', 'include', 'lib' folders are there)."
    }
}
else {
    Write-Host "OpenSSL seems to be present." -ForegroundColor Green
}

# 2. Check Jom
if (-not (Test-Path "$devDir\Jom\jom.exe")) {
    Write-Warning "Jom is missing. Please re-run the previous script or download Jom manually to C:\Dev\Jom."
}

# 3. Qt Init Repository
$qtDir = "$devDir\Qt\5.15.9"
Set-Location $qtDir

if (Test-Path ".git") {
    Write-Host "Initializing Qt repository..." -ForegroundColor Cyan
    if (Get-Command perl -ErrorAction SilentlyContinue) {
        # Retry init-repository
        try {
            perl init-repository
            Write-Host "Qt5 source initialized and ready for build." -ForegroundColor Green
        }
        catch {
            Write-Error "Failed to run 'perl init-repository'. Please check the error output."
        }
    }
    else {
        Write-Error "Perl is still not found in PATH."
    }
}
else {
    Write-Error "Qt directory not found or not a git repo. Please re-run the previous script to clone it."
}

Write-Host "Dependencies setup complete." -ForegroundColor Green
