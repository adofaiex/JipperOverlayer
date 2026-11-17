# JipperOverlayer 项目上下文（代理每次请求都会读到，压缩不会丢失）

## 项目
ADOFAI 游戏内 overlay 模组（C#，双加载器：Unity Mod Manager + MelonLoader），
显示进度/准度/BPM/连击/判定等。仓库 2228293026/JipperOverlayer，分支 master。

## 当前任务（2026-11-13 会话确认，完成后更新此行）
- 把 overlay 数值彻底拆成 tag 模板系统（可自由拼装）：
  `Overlayer/Tags/` 新增引擎（TagAttribute / TagManager / TemplateEngine）与 3 个 GameTags 分片，
  共 119 个内置 tag（特效：ColorRange atMax 顶格金/EasedValue/MovingMan/MarginCombo/XScoreRatio/判定聚合/时间分量/峰值 BPM；导入内置渲染一键生成等价模板；含动画 tag 的模板逐帧求值）
  + 用户自定义组合 tag（CustomTagDef：设置面板可视化编辑「名称 → 模板片段」，即时注册，防环/防重名）；每个文本项一个独立模板字段（LabelConfig），空模板保持原有渲染；
  设置面板新增独立「文本模板」分区（可用 tag 一览，可点击复制）+ 「自定义文本行」分区
  （可增删的任意行，进栈 / 自由坐标两选一，~10Hz 求值）+ tag 游玩状态门控
  （BlockOnNotPlaying/Paused，非游玩/暂停时冻结上一次好值，参照 Overlayer 的 TagType）。Tr 三语已补文案。
- 状态：代码与 CHANGELOG 已写完；主工程 + UMM/Melon 双加载器 Release 零警告编译通过。
  状态（2026-11-14 晚）：效果工具面板已部署（FF2AAD24，bin/deploy-output/游戏目录三点一致）。
  修复「设置窗整块灰板」根因：DrawTemplatesSection 里孤儿 GUILayout.EndHorizontal()（一展开就抛 NRE，UMM 记一次后 ShowModSettings=-1 + ExitGUI 踢出标签页）——已删除并全文件配对审计；Main.OnGUI 加 try/catch 兜底（限频记全堆栈，不再外抛给 UMM）；MovingMan speed 改可选（默认 250）。
  修复「从菜单进关整关没进度条」：ExitOutsidePlayMode（OnUpdate 先于 Show 翻标志）不恢复 Enter 隐藏的进度条 → Show 恢复块被跳过——Exit 现按 ShowProgressBar 点回（16BC56D8）。
  修复「非游玩文本转场后永久消失」：Hide()（场景卸载/擦黑/编辑器 ResetScene）压灭画布但 _outsidePlayActive 标志不复位 → 永不重走 Enter——UpdateOutsidePlayMode 加自愈（检测被外部熄灭即补恢复）（3F7848EF）。
  修复「编辑器里非游玩行从不显示」：门控 !IsGameReady 在编辑器恒为 true（controller/conductor 齐 + isGameWorld=true）→ 改为 IsScnGame || EditorPlayMode（scnEditor.playMode，口径参照 Overlayer.Module.ADOFAI 的 IsPlaying）（BE70DC7C）。OverlayMono 画布裁决（!IsPaused）加 OutsidePlayActive 豁免（编辑器编辑界面 controller.paused=true 会每帧关画布）。
  修复「打开设置文本位置偏移」：SetupLocation 流式高度依赖游戏态条件（检查点/无失败/StartTile）+ Enter 不重排 → 非游玩模式下固定栈顶起排只排勾选行、Enter 主动 SetupLocation、Settings.OnGUI 入口清 GUI.changed 防 UMM 控件污染（CC4461A3）。deploy-to-game.ps1 已升级为改名式部署（游戏运行中可用）。效果工具面板已升级为三分页专属 UI（渐变/动画/缓动：色块+hex+色板、31 缓动选择器、渐变条预览、实时求值、一键插入；FxNum 稳定帧返回 bug 已修）（331DE2A0）。
  游戏运行中留用户实测；未 commit（等用户指示）。注意：本机安全策略开始拦截内联命令直写 Program Files，游戏目录部署一律走 deploy-to-game.ps1。脚本受版本控制的副本在 tools/deploy-to-game.ps1（deploy-output/ 里那份是运行时副本，已被 gitignore）。
  部署技巧（已验证）：游戏运行中 DLL 被锁——把旧 DLL 改名为 .prev 再放入新 DLL，不用关游戏，下次启动生效。
  状态（2026-11-18）：`dcc210b` 已推送（FpsTracker 窗口采样批次 + 2-X 门控修复），本地与 origin 齐平。
  修复「官方关卡（2-X 等）内 overlay 文本全部消失」：官方关卡不走 scnGame/scnEditor 场景（日志实证 inLevel=False IsScnGame=False editorInst=False），
  BE70DC7C 的场景名门控在关卡内误判非游玩 → 回到 IsGameReady 口径，编辑器例外保留：
  `inLevel = EditorInstance != null ? EditorPlayMode : IsGameReady`。
  新增：自定义行每行独立字体（FontIndex，-1 跟随主区域，设置面板折叠列表点选，切换走材质级重刷）；
  「隐藏标题」开关（显示→行为选项，每帧写 GCS.d_dontShowTitles，scrHUDText 原生隐藏标题，Disable 复位，参照 Overlayer.Module.ADOFAI）。
  修复「Tr 三语 HideTitle 错位」：曾把三语文案追加到数组末尾而枚举插在中间 → TemplateExamples~SizeHint 串位（Tr.Get 按下标取值）——文案已移回同位；
  加 Tr.SelfAudit()（Main.Enable 调用：三语长度 = 枚举数 + 哨兵键英文抽查）防再犯。
  注意：deploy-to-game.ps1 的拷贝源是 deploy-output\（旧产物缓存）不是 bin——部署前必须先把 bin 新 DLL 拷进 deploy-output（2026-11-18 差点部署旧 DLL，三点哈希校验拦下）。
  当前未 commit（等实测 + 用户指示）；游戏目录已部署最新 DLL（哈希三点一致）。
- 上一任务（分隔符逐项自定义 + coop XScore 差 2 分）已完成并推送：commit `374d30b`。

## 事实锚点（2026-09-18 核实）
- `CHANGELOG.md` 共 324 行，顶部为 `## Unreleased`。以实际 read 结果为准。

## 行为规则
1. **文件为准**：行号/内容对不上时相信工具读取结果，不要判定为"乱码"后反复重读。
2. 会话被压缩或换模型后：先重读本文件定位任务再继续；不要重新验证已确认的事实（如已 push 的提交）。
3. **用中文回复用户**（用户是中文使用者，即使上下文多为英文）。

## 硬性规则（违反过，2026-09-19 追加）
1. **禁止未经用户明确指示就 commit / push**。尝试性修复尤其不允许——一次失败就意味着历史里多一个垃圾提交。
2. **修复必须先验证再宣布成功**：先完整追踪调用链（谁调用、条件、默认值、配置读写），说得出"为什么这次会生效"才动手；无法运行游戏验证时，明确说"静态分析结论，需要用户实测"，不许说"已修复"。
3. **同一问题第二次失败必须换方法**：重新通读完整代码路径，禁止在同一个函数里再猜一个条件继续试。
4. 构建（Build）通过 ≠ 修复完成。
5. **为验证启动游戏后必须保持游戏运行**（2026-11-14 用户明确指出）：读日志确认加载成功后禁止 Stop-Process，游戏留给用户实测；只有用户要求关闭时才关。
