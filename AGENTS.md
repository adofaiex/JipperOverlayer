# JipperOverlayer 项目上下文（代理每次请求都会读到，压缩不会丢失）

## 项目
ADOFAI 游戏内 overlay 模组（C#，双加载器：Unity Mod Manager + MelonLoader），
显示进度/准度/BPM/连击/判定等。仓库 2228293026/JipperOverlayer，分支 master。

## 当前任务（2026-09-18 会话确认，完成后更新此行）
- 同步本地 `Libs/` 到 adofai-libs 最新游戏版本的代码改动；
- 修正过时注释；
- 测试 checkpoint restart 场景。
- 已完成：commit `3975859` 已推送到 origin/master（5 个文件，中文 commit message）。

## 事实锚点（2026-09-18 核实）
- `CHANGELOG.md` 共 324 行，顶部为 `## Unreleased`。以实际 read 结果为准。

## 行为规则
1. **文件为准**：行号/内容对不上时相信工具读取结果，不要判定为"乱码"后反复重读。
2. 会话被压缩或换模型后：先重读本文件定位任务再继续；不要重新验证已确认的事实（如已 push 的提交）。
3. **用中文回复用户**（用户是中文使用者，即使上下文多为英文）。
