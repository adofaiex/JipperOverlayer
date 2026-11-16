$ErrorActionPreference = 'Stop'

# JipperOverlayer 部署脚本：把现有 dll 改名为 .prev（游戏运行中被锁的 DLL 也能改名，
# 原句柄继续有效，下次启动生效）-> 复制 Release 产物到游戏 Mods 目录。
# 只触碰 JipperOverlayer.dll 与 JipperOverlayer.Loader.UMM.dll；运行时数据（Settings/labels/colors/Plays.dat）不动。

$mod = 'D:\Program Files (x86)\Steam\steamapps\common\A Dance of Fire and Ice\Mods\JipperOverlayer'
$src = 'D:\Projects\JipperOverlayer\deploy-output'
$ts  = Get-Date -Format 'yyyyMMdd-HHmmss'

if (-not (Test-Path $mod)) { Write-Error "mod dir not found: $mod"; exit 1 }
if (-not (Test-Path "$src\JipperOverlayer.dll")) { Write-Error "build output missing"; exit 1 }

# 1) 备份：改名（沿用目录里已有的 .prev 时间戳惯例；复制备份在游戏运行中会被文件锁卡住）
Rename-Item "$mod\JipperOverlayer.dll" "JipperOverlayer.dll.$ts.prev"
Rename-Item "$mod\JipperOverlayer.Loader.UMM.dll" "JipperOverlayer.Loader.UMM.dll.$ts.prev"

# 2) 覆盖
Copy-Item "$src\JipperOverlayer.dll" "$mod\JipperOverlayer.dll" -Force
Copy-Item "$src\JipperOverlayer.Loader.UMM.dll" "$mod\JipperOverlayer.Loader.UMM.dll" -Force

# 3) 验证：列出部署结果
Write-Host "=== deployed ($ts) ==="
Get-ChildItem $mod -Filter '*.dll' |
  Where-Object { $_.Name -notmatch '\.(prev|bak|cache)$' } |
  Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize | Out-String | Write-Host
