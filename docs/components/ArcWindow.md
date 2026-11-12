# ArcWindow

状态：**实现完成，待验收**。事实见[参考卡](../references/components/ArcWindow.md)，验证见[记录](../verification/2026-11-12-arc-window.md)。

Arc 没有窗口组件，这是 ArcWpf 的设计：用 Arc 的表面、文字和配色画标题栏与窗口按钮，系统边框和窗口行为全部交给 WPF 的 `WindowChrome` 与 Windows。

## 范围

| 交付项 | 本次范围 | 实现状态 | 验证证据 | 剩余差异/下一步 |
|---|---|---|---|---|
| 标题栏：图标、标题、内容插槽、操作插槽 | 必做 | 已实现 | 专项测试；Wine 真实点击插槽内控件 | 真实 Windows 未运行 |
| 窗口按钮：最小化、最大化/还原、关闭，随 `ResizeMode` 变化 | 必做 | 已实现 | 专项测试；Wine 真实点击 | — |
| 拖动、双击最大化、边缘调整大小、系统菜单 | 必做（系统提供） | 已实现 | Wine 真实拖动、双击、拖边缘 | 系统菜单（Alt+Space、右键）未运行 |
| 最大化时内容不越出屏幕 | 必做 | 已实现 | 专项测试；Wine 下尺寸 1288×1108、边距 4 | Wine 截图无法显示最大化后的画面（普通 Window 同样），视觉未验证 |
| Windows 11 贴靠布局 | 必做 | 已实现 | 专项测试（消息级）；Wine 真实悬停命中 `HTMAXBUTTON` 并点击最大化 | 贴靠布局浮层本身只有 Windows 11 有，未运行 |
| DWM 边框：深色模式、圆角、边框色 | 必做 | 已实现 | 无（Wine 没有 DWM） | 真实 Windows 未运行 |
| 失去激活时标题和图标变淡 | 必做 | 已实现 | Wine 截图 | — |
| 悬停/按下颜色过渡 | 必做 | 已实现 | 专项测试（Off 终态）；Wine 截图 | — |
| 全屏模式、标题栏双行、Mica/Acrylic 背景、自定义窗口按钮集合 | 暂缓 | 未实现 | — | 需要时再加 |

## WPF 公开契约

`ArcWindow : Window`。默认样式在 `ArcWindow/Themes/ArcWindowTheme.xaml`，已并入 `Generic.xaml`。

| 属性 | 类型与默认值 | 说明 |
|---|---|---|
| `TitleBarHeight` | `double`，40 | 标题栏高度，同时是 `WindowChrome.CaptionHeight`；必须有限且不小于 0 |
| `TitleBarContent` | `object`，空 | 标题后面的区域（分段切换、面包屑、搜索框）。可以点击；其中没有命中的空白处仍然拖动窗口 |
| `TitleBarActions` | `object`，空 | 窗口按钮左侧的少量操作 |
| `ShowTitle` | `bool`，true | 是否显示标题文字（自动化名称仍是 `Title`） |

- 继承 `Window` 的 `Title`、`Icon`（有图标时显示 16×16）、`ResizeMode`、`WindowState`。
- `ResizeMode`：`CanResize`/`CanResizeWithGrip` 三个按钮齐全；`CanMinimize` 最大化按钮禁用（与系统边框一致）且不能拖边缘；`NoResize` 只有关闭。
- 窗口按钮执行 `SystemCommands` 的四个命令，绑定在窗口上；不可聚焦、不在 Tab 顺序里，自动化名称为 Minimize、Maximize/Restore、Close。
- 模板部件：`PART_Root`（最大化时设边距）、`PART_TitleBar`、`PART_CaptionButtons`（`Panel`，按钮由代码放入）。缺少时对应功能跳过。
- 不接管 `Content`、`DataContext`、`Background` 绑定；`Background` 默认是 `Background` 令牌，可覆盖。

## 实现要点

- `WindowChrome`：`GlassFrameThickness=-1` 让 DWM 保留阴影和 Windows 11 圆角；`UseAeroCaptionButtons=false`；可调整大小时 `ResizeBorderThickness` 为系统值，否则为 0。
- 最大化：WindowChrome 窗口最大化时会越出屏幕一个调整边框的宽度，`PART_Root` 加 `SystemParameters.WindowResizeBorderThickness` 的边距收回。
- 贴靠布局：`HwndSource` 钩子在 `WM_NCHITTEST` 落在最大化按钮上时返回 `HTMAXBUTTON`，于是系统接管该区域，WPF 不再收到鼠标事件；悬停由 `WM_NCHITTEST`/`WM_NCMOUSEMOVE` 驱动，按下和松开由 `WM_NCLBUTTONDOWN`/`UP` 处理并标记已处理，避免系统画出旧式按钮。松开时只有之前在按钮上按下过才执行。
- DWM：`DWMWA_USE_IMMERSIVE_DARK_MODE` 按 `Background` 亮度决定，`DWMWA_WINDOW_CORNER_PREFERENCE` 为圆角，`DWMWA_BORDER_COLOR` 用 `Border` 令牌；主题切换时重设。系统不支持的属性被忽略。
- 窗口按钮复用 `ArcChromeButton`（颜色过渡、主题跟随），宽 46、高等于标题栏。
- 动效：颜色过渡 160ms，Off 时立即到位（沿用 `ArcChromeButton`）。窗口本身的打开、最大化动画交给系统。

## 验收场景

1. 打开 Gallery → AI → ArcWindow，点“打开 Agent 窗口”：标题栏有标题、分段切换、主题开关和三个窗口按钮，无缝接正文。
2. 悬停三个按钮：前两个浅灰底，关闭为红底白图标；切换深色主题后再看一次。
3. 拖动标题栏空白处移动窗口；双击最大化、再双击还原；拖右边缘调整宽度。
4. 点分段切换的其它项：能切换，不拖动窗口。
5. Windows 11：悬停最大化按钮出现贴靠布局；Alt+Space 和右键标题栏出现系统菜单。
6. 最大化后四边内容完整，没有被屏幕裁掉；任务栏不被遮挡。
7. 切到另一个窗口：标题和按钮图标变淡。
8. “固定大小窗口”：不能拖边缘，最大化按钮禁用。
