# Map slowblossom.com to local PC IP (requires Administrator)
$ErrorActionPreference = "Stop"
$domain = "slowblossom.com"
$hostsPath = "$env:Windir\System32\drivers\etc\hosts"
$marker = "# slowblossom local"

$ip = (Get-NetIPAddress -AddressFamily IPv4 |
    Where-Object { $_.IPAddress -notlike '127.*' -and $_.IPAddress -notlike '169.254*' -and $_.PrefixOrigin -ne 'WellKnown' } |
    Select-Object -First 1).IPAddress

if (-not $ip) {
    $ip = "127.0.0.1"
    Write-Warning "Wi-Fi IP not found. Using 127.0.0.1 only."
}

$lines = New-Object System.Collections.Generic.List[string]
foreach ($line in [System.IO.File]::ReadAllLines($hostsPath)) {
    if ($line -match [regex]::Escape($domain) -or $line -eq $marker) {
        continue
    }
    $lines.Add($line)
}

$lines.Add("")
$lines.Add($marker)
$lines.Add("127.0.0.1`t$domain")
$lines.Add("$ip`t$domain")

[System.IO.File]::WriteAllLines($hostsPath, $lines.ToArray())

Write-Host ""
Write-Host "hosts file updated."
Write-Host "  127.0.0.1  -> $domain"
Write-Host "  $ip  -> $domain"
Write-Host ""
Write-Host "Open on PC: http://$domain (port 80) or http://${domain}:5198"
Write-Host ""
Write-Host "Start server as Administrator:"
Write-Host "  cd RotationDating.Web"
Write-Host "  dotnet run --launch-profile https"
