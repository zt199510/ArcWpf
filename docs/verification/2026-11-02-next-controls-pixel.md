# AnnouncementBar 等八个控件像素对照记录

日期：2026-11-02。按用户要求对照 `https://uiarc.dev/components/*` 重做 AnnouncementBar、AnimatedCounter、Countdown、Timeline、AvatarUpload、FileDropzone、MentionInput、EmojiPicker。**这不是像素完全一致**，差异见下文。

## 来源

- 视觉基准：Playwright + Chromium，视口 1280x1000、1x、亮色，每页独立浏览器上下文，截取线上控件并读取 DOM 几何和计算样式（证据等级：线上实测）。
- 实现依据：`tests/Baselines/References/2026-11-02-*` 的冻结 Registry 源码（AnnouncementBar、AnimatedCounter、Countdown、Timeline、FileDropzone、MentionInput）。AvatarUpload 与 EmojiPicker 属 Arc Pro，只依据公开文档和公开页面显示的几何独立实现，未使用实现 JS、Pro 源码或付费素材。
- 截图和对照图在 `artifacts/2026-11-02-next-controls-pixel/`（不入库）。

## 结果

| 层级 | 结论 |
|---|---|
| 构建 | `ArcWpf.slnx` 0 错误 |
| 相关测试 | `NextArcControls*` 29/29 通过（Wine）；全量测试未运行，按用户要求只跑任务相关测试 |
| 离屏渲染（Wine） | 亮色八控件、暗色八控件均已出图并人工对比 |
| 真实 Windows 窗口 | 未运行，待验证 |

## 尺寸

与线上 DOM 一致：AnnouncementBar 演示 521.6×148，单行条 45，折行条 66；AnimatedCounter 卡片 250×114.6；Countdown 卡片 521.6×450.2、紧凑胶囊高 32；Timeline 卡片 521.6×376.8；FileDropzone 537.6×398、投放区高 196、行高 58；MentionInput 编辑器 520×114、输入框高 48、建议项高 50.4；AvatarUpload 卡片 521.6×303.6；EmojiPicker 消息卡 480×278、面板 352×410。

例外：胶囊宽度随字体变化（WPF 145，线上 152）；建议浮层高 316.4，线上 314，线上取值是高度弹簧的瞬时值，未单独捕获终值。

## 逐像素差异

同一尺寸下比较 RGB，允许 ±3 DIP 平移取最优，排除 4 DIP 边缘。

| 控件 | 对照 | 有差异 | 差值 >32 | 主要来源 |
|---|---|---|---|---|
| AnnouncementBar | announcement-m2 ↔ 线上画布 Annual plans | 26.4% | 3.4% | 反色条带文字占比高，字体差放大差异 |
| AnimatedCounter | 卡片 ↔ 画布 | 14.8% | 8.8% | 数字字面差异；Geist 比回退字体大约 2 DIP |
| Countdown 跳转后 | countdown-final | 12.8% | 6.4% | 大号数字字面差异 |
| Countdown 完成 | countdown-done | 8.6% | 4.0% |  |
| Timeline 默认 | timeline ↔ 画布 | 10.0% | 6.7% | 头像为占位渐变 |
| Timeline 展开 | timeline-open | 22.7% | 7.0% | 线上基准带悬停底色，WPF 未悬停 |
| FileDropzone | file-dropzone ↔ 画布 | 8.2% | 4.3% | 缩略图为占位渐变 |
| MentionInput | composer ↔ 画布 | 10.0% | 3.2% |  |
| AvatarUpload | card ↔ 画布 | 14.5% | 8.2% | 头像和样片为占位渐变 |
| EmojiPicker 消息卡 | thread ↔ 画布 | 11.3% | 6.1% |  |
| EmojiPicker 面板（无彩色字体） | emoji-panel | 21.7% | 17.6% | Wine 无 Segoe UI Emoji，表情未绘制 |
| EmojiPicker 面板（Twemoji 彩色） | emoji-color-panel | 22.4% | 14.3% | 字形来自 Twemoji，基准来自 Noto，图案本来就不同 |

## 已知差异

- 字体：线上是 Inter/Geist；用户明确不内置 Inter，WPF 使用 `ArcWpf.Font.Body`，且未设字间距。Wine 下实际是回退字体，真实 Windows 字面会不同。
- **EmojiPicker 的彩色表情只用 Twemoji 离屏验证过，Segoe UI Emoji 未验证。** 控件现在自行解析字体的 COLR v0、CPAL、GSUB 连字和 `glyf` 轮廓并逐层绘制。验证用的是 Mozilla Twemoji 0.7.0（只放在临时目录，不随仓库分发），浅色、深色、肤色面板都已离屏检查，1870 项全部能生成形状。真实 Windows 上是否同样正确**未运行**，原因是本环境没有 Windows 和 Segoe UI Emoji。已知风险：
  - 只处理 GSUB 的连字查找（类型 4 及其扩展形式），且不区分 feature 标签；字体若靠上下文替换（类型 6）组合肤色或 ZWJ 序列，这些序列会拆成多个字形绘制。
  - 只读 COLR v0 记录：只存在于 COLR v1 的字形按单层轮廓绘制。没有 `glyf` 的字体（CFF）或没有彩色表的字体回退到单色文本。
  - 字体缺少某个序列时会拆开绘制，宽度超出单元格；Twemoji 0.7.0 上只有 🐦‍⬛ 一项如此。
  - 搜索框仍由 WPF 文本栈绘制，输入的表情是单色。Gallery 消息框已接入 [TextColoring](2026-11-02-text-coloring.md)。
- 轮廓不走 `GlyphTypeface.GetGlyphOutline`：在 Wine 下它对 Twemoji 字形 1735 触发原生访问冲突（0xC0000005，新进程单独调用也复现），托管代码无法捕获。这是否只发生在 Wine 上未验证，所以改为解析 `glyf`，Twemoji 全部 10006 个非空字形可解析，耗时约 150 ms。
- 头像、样片和缩略图：线上为站点自有图片，Gallery 用占位渐变代替。
- MentionInput：令牌只有底色，文字色仍是正文色（线上 .24 对 .15），因为 `TextBox` 不支持按区段着色；浮层高度不随结果数做弹簧过渡。
- Countdown、AnnouncementBar 的单字符滚轮没有模糊；AnimatedCounter 沿用 `OdometerNumber`，羽化遮罩与线上比例相同。
- FileDropzone 的虚线边框由 WPF 虚线绘制，未按周长拟合；图纸、投放光晕和呼吸的拖入态未在真实拖放下验证。
- AnnouncementBar 折行消息（Fieldwork）线上截图未捕获到，只按 DOM 尺寸核对。

## 修复的缺陷

- MentionInput：弹出建议时 `SelectionChanged` 立即插入第一项，用户无法继续输入筛选；改为点击或 Enter 才选择。令牌底色起点错位一个字符；占位符和令牌从装饰层移入模板，画在文字之下。
- Timeline：应用模板时会清除调用方设置的 `IsExpanded`；`Events` 被替换后不再监听变更；按本地时区而不是 `TimeZoneId` 分组。超过 12 小时的今日条目改显示时钟时间。
- AnimatedCounter：模板应用前设置 `Value` 时滚轮按默认字号建立；`Label` 变化不更新自动化名称。
- AvatarUpload：裁剪输出的是整个 248 舞台，现在对应 184 裁剪圆；进度环弧长按实际周长计算。
- FileDropzone：`FormatFileSize` 在 10 MB 以上保留小数，线上取整。
- EmojiPicker：约 1,800 个按钮一次性创建，改为按行虚拟化；Shift 连选保持打开。
- EmojiPicker：目录按码点排序，与线上顺序不符（Smileys 从 ☠️ 开始）；改为线上顺序，蟹虾类归入 Animals，补上英格兰、苏格兰、威尔士三面旗，共 1870 项。预览占位按线上 `grayscale(1)` 绘制为灰度。

## API 变化

公开 API 只增不删，默认值有两处改变。

- 新增：`AnnouncementItem.CountdownLabel`、`AnnouncementBar.Label/CountdownEnded/MutedBrush/IconHoverBrush/IconHoverForeground`，`Countdown.Clock`，`TimelineEvent.Avatar/Icon`，`FileDropzoneItem.PreviewSource/Thumbnail/IconData/StatusText`，`MentionPerson.Avatar`，`MentionInput.InsertTrigger`，`AvatarUpload.RemoveImage/Removed/HasError`，`EmojiPicker.TriggerContent`。
- 类型变化：`TimelineEvent.Detail` 由 `string` 改为 `object`。
- 新增：`EmojiPicker.EmojiFontFamily`（默认 Segoe UI Emoji）。
- 默认值：`EmojiPicker.Columns` 6 → 9，`MaxRecent` 8 → 27；`DefaultRecent` 换为线上 Popular 18 项。
- Gallery：`Arc Next Batch` 合并页删除，换成八个独立页面。旧记录里的 `--page=arc-next-batch` 不再可用。

## 待验收

真实窗口的比例、DPI、悬停、拖放、Popup 定位与碰撞、IME、键盘导航、动效中间帧，以及 Windows 下的表情字形。
