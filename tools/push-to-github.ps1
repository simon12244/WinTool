# 推送 WinTool 到 GitHub。令牌从环境变量读取，不写进命令行、不落盘、不进历史记录。
# 用法：$env:GH_PAT="github_pat_xxx"; $env:GH_USER="你的用户名"; .\dev\push-to-github.ps1
$ErrorActionPreference = "Stop"

$git = "C:\Program Files\Git\cmd\git.exe"
if (-not (Test-Path $git)) { throw "找不到 git.exe，请先安装 Git" }

$user = $env:GH_USER
$pat  = $env:GH_PAT
if (-not $user) { throw "请先设置环境变量 GH_USER（你的 GitHub 用户名）" }
if (-not $pat)  { throw "请先设置环境变量 GH_PAT（你的 PAT 令牌）" }

$repo = if ($env:GH_REPO) { $env:GH_REPO } else { "WinTool" }
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Set-Location $root

Write-Host "=== 1. 检查本地仓库状态 ==="
& $git status --short
& $git log --oneline -1

Write-Host ""
Write-Host "=== 2. 设置远端 origin ==="
$remote = "https://github.com/$user/$repo.git"
$existing = & $git remote 2>$null
if ($existing -contains "origin") { & $git remote remove origin }
& $git remote add origin $remote
Write-Host ("origin -> " + $remote)

Write-Host ""
Write-Host "=== 3. 推送 main 分支 ==="
# 令牌通过临时 HTTP 头传入，避免出现在 URL 或 .git/config 里
$env:GIT_CONFIG_COUNT = "1"
$env:GIT_CONFIG_KEY_0 = "http.extraHeader"
$env:GIT_CONFIG_VALUE_0 = "Authorization: Basic " + [Convert]::ToBase64String(
    [Text.Encoding]::ASCII.GetBytes($user + ":" + $pat))

try {
    & $git push -u origin main
    if ($LASTEXITCODE -ne 0) { throw ("push 失败，退出码 " + $LASTEXITCODE) }
} finally {
    Remove-Item Env:GIT_CONFIG_COUNT, Env:GIT_CONFIG_KEY_0, Env:GIT_CONFIG_VALUE_0 -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "=== 4. 完成 ==="
& $git remote -v
Write-Host ("仓库地址: https://github.com/" + $user + "/" + $repo)
