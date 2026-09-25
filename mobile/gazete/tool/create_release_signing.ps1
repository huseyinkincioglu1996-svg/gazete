[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$androidDirectory = Join-Path $projectRoot 'android'
$signingDirectory = Join-Path $androidDirectory 'signing'
$keystorePath = Join-Path $signingDirectory 'gazete-release.jks'
$propertiesPath = Join-Path $androidDirectory 'key.properties'

if ((Test-Path -LiteralPath $keystorePath) -or (Test-Path -LiteralPath $propertiesPath)) {
    throw 'İmzalama dosyaları zaten var. Güncelleme anahtarını korumak için işlem durduruldu.'
}

$keytool = (Get-Command keytool.exe -ErrorAction Stop).Source
$randomBytes = New-Object byte[] 36
[System.Security.Cryptography.RandomNumberGenerator]::Fill($randomBytes)
$password = [Convert]::ToBase64String($randomBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=')

New-Item -ItemType Directory -Path $signingDirectory -Force | Out-Null

& $keytool `
    -genkeypair `
    -v `
    -keystore $keystorePath `
    -storetype JKS `
    -alias 'gazete-release' `
    -keyalg RSA `
    -keysize 4096 `
    -validity 10000 `
    -storepass $password `
    -keypass $password `
    -dname 'CN=Gazete, OU=Mobil, O=TurnaExpress, L=Istanbul, ST=Istanbul, C=TR'

if ($LASTEXITCODE -ne 0) {
    throw "keytool işlemi başarısız oldu (çıkış kodu: $LASTEXITCODE)."
}

$properties = @(
    'storeFile=../signing/gazete-release.jks'
    "storePassword=$password"
    'keyAlias=gazete-release'
    "keyPassword=$password"
)

[System.IO.File]::WriteAllLines(
    $propertiesPath,
    $properties,
    [System.Text.UTF8Encoding]::new($false)
)

Write-Host 'Kalıcı Gazete release anahtarı oluşturuldu.'
Write-Host "Yedeklenecek anahtar: $keystorePath"
Write-Host "Yedeklenecek parola dosyası: $propertiesPath"
