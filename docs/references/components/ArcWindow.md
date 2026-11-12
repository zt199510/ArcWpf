# ArcWindow 参考卡

## 来源与固定范围

- **Arc 没有对应组件**：Arc（uiarc.dev）是网页组件库，没有顶层窗口；页面里的“窗口”装饰（如 Typewriter terminal 的三色圆点）是演示用的，不能操作。本控件是 ArcWpf 为桌面 Agent 做的设计（2026-11-05 计划确定的控件顺序：DiffView、TerminalOutput、TreeView 与 Tabs 增强、ArcWindow）。
- **TKCode.Arc**（`5680bf55414fff07398a9fecadc0b72c900fdaba`）：搜索 `WindowChrome`、`WM_NCHITTEST`、`DwmSetWindowAttribute`，没有窗口外框实现，无可参考的实现经验。
- **DeepSeek Harness**：本次没有读取它的窗口实现，未用作依据。
- **WPF 原生机制**：`System.Windows.Shell.WindowChrome`（保留系统边框、拖动、贴靠、调整大小和系统菜单）、`SystemCommands`、`HwndSource` 消息钩子、DWM 窗口属性。Windows 11 贴靠布局要求最大化按钮在 `WM_NCHITTEST` 中返回 `HTMAXBUTTON`，这是 Windows 的公开行为。

## 取自 Arc 设计语言的值

| 项目 | ArcWpf | 依据 |
|---|---|---|
| 窗口与标题栏底色 | `Background`，标题栏不另画底色，与内容无缝 | Arc 页面底色令牌 |
| 标题文字 | `Font.Body`、`FontSize.Small`（14）、Medium；失去激活时 `TextMuted` | Arc text-sm |
| 窗口按钮图标 | lucide Minus / Square / Copy（还原）/ X，16，线宽 1.5 | Arc 的图标库是 lucide |
| 按钮悬停/按下 | `SurfaceMuted` / `WindowCaptionPressed`，160ms 颜色过渡 | Arc 悬停底色与 fast 过渡 |
| 关闭按钮悬停 | `WindowCloseHover`（OKLCH .54/.21/25，明暗相同）+ 白色图标 | Arc Danger 的亮色值；暗色主题的 Danger 太亮，白字看不清 |

## 有意的差异

| 项目 | 常见做法 | ArcWpf | 原因 |
|---|---|---|---|
| 窗口按钮尺寸 | Windows 46×32 | 46×标题栏高（默认 40） | 标题栏要放分段切换等控件，40 更合适 |
| 关闭按钮红色 | Windows #C42B1C | Arc 的 Danger 色相 | 与控件库其它危险色一致 |
| 窗口三色圆点 | Arc 演示 | 无 | Windows 原生应用 |
