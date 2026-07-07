# 로컬 HTTPS 인증서 생성 (localhost + Wi-Fi IP)
$ErrorActionPreference = "Stop"

$projectRoot = Split-Path $PSScriptRoot -Parent
$certDir = Join-Path $projectRoot "certs"
$pfxPath = Join-Path $certDir "rotationdating.pfx"
$password = "rotationdating"

New-Item -ItemType Directory -Force -Path $certDir | Out-Null

$ip = (Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254*' -and $_.PrefixOrigin -ne 'WellKnown' } |
    Select-Object -First 1).IPAddress

if (-not $ip) {
    $ip = "127.0.0.1"
    Write-Warning "Wi-Fi IP를 찾지 못해 127.0.0.1만 사용합니다."
}

if (Test-Path $pfxPath) {
    Remove-Item $pfxPath -Force
}

$sanExtension = "2.5.29.17={text}DNS=localhost&DNS=slowblossom.com&IPAddress=$ip"

$cert = New-SelfSignedCertificate `
    -Subject "CN=RotationDating" `
    -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.1", $sanExtension) `
    -FriendlyName "Rotation Dating Local HTTPS" `
    -NotAfter (Get-Date).AddYears(5) `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyExportPolicy Exportable `
    -KeySpec Signature

$securePwd = ConvertTo-SecureString -String $password -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePwd | Out-Null

Write-Host ""
Write-Host "인증서 생성 완료: $pfxPath"
Write-Host "적용 대상: localhost, slowblossom.com, $ip"
Write-Host ""
Write-Host "PC 접속:       http://slowblossom.com"
Write-Host "               https://slowblossom.com:7198"
Write-Host "스마트폰 접속: http://slowblossom.com (hosts/DNS 설정 후)"
Write-Host ""
Write-Host "스마트폰에서 최초 접속 시 인증서 경고가 나오면 '고급' -> '계속'을 선택하세요."
