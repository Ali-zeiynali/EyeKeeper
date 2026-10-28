$ErrorActionPreference = 'Stop'
Remove-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name EyeKeeper -ErrorAction SilentlyContinue
Write-Host 'EyeKeeper startup entry removed. Your preferences are preserved.'
