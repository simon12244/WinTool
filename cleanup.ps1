# 清理临时产物（保留构建与验证脚本，移入 dev\ 便于保留开发能力）
$ErrorActionPreference = "SilentlyContinue"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

# 1) 删除一次性诊断文件与中间产物
Remove-Item .\res-split.ps1 -Force
Remove-Item .\src\Probe.cs, .\src\Probe2.cs, .\src\version.res -Force
Get-ChildItem .\src -Filter "t_*.res" | Remove-Item -Force
Get-ChildItem .\build -Filter "*.png" | Remove-Item -Force
Get-ChildItem .\build -Filter "*.exe" | Where-Object { $_.Name -ne "WinTool.exe" } | Remove-Item -Force
Remove-Item .\build\pages -Recurse -Force
Remove-Item .\build\logs -Recurse -Force
Remove-Item .\build\scripts -Recurse -Force
Remove-Item .\build\error.log, .\build\probe.txt, .\build\probe2.txt, .\build\wintool.ini -Force

# 2) 开发脚本归档到 dev\
if (-not (Test-Path .\dev)) { New-Item -ItemType Directory .\dev | Out-Null }
foreach ($f in @("make-assets.ps1", "shot.ps1", "shots-pages.ps1")) {
    if (Test-Path (Join-Path $root $f)) { Move-Item (Join-Path $root $f) (Join-Path $root "dev\$f") -Force }
}

Write-Host "cleanup done"
Get-ChildItem -Recurse -File | ForEach-Object { $_.FullName.Replace($root, ".") + "   " + $_.Length + " B" }
