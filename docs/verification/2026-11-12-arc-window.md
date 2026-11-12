# 2026-11-12 ArcWindow 验证

状态：**实现完成，待验收**。规格见 [ArcWindow](../components/ArcWindow.md)。Arc 没有窗口组件，外观只做目视自查。

## 构建与专项测试

- `dotnet build ArcWpf.slnx -p:EnableWindowsTargeting=true`：0 错误。
- `ArcWindowTests` 6 项全部通过：窗口按钮命令与最大化边距、`ResizeMode` 决定按钮和调整边框、标题栏高度与插槽可点击、贴靠布局的非客户区消息（命中、按下、松开、无按下的松开）、关闭按钮明暗主题的红底白字、Gallery 的 Agent 窗口。
- 测试中发现并修正：窗口按钮先设 `Command` 后设 `CommandTarget`，第一次 CanExecute 查询没有目标，切换 `ResizeMode` 后重建的最大化按钮一直禁用。
- 提交前最终构建（含 ArcWindow，`ArcWpf.Controls.dll` 前 16 位 `8ebf5d4f376bd372`）：TreeView 23、JSONViewer 10、Tabs 23、ArcWindow 6，**62 项全部通过**。
- 全量测试：**未运行**（按用户要求只测相关控件）。

## Wine 窗口内真实输入

Linux + Wine + Xvfb，没有窗口管理器和 DWM，不是真实 Windows。日志见下表，截图在 `artifacts/arcwindow-2026-11-12/`（不入库）。

| 场景 | 结果 |
|---|---|
| 悬停最小化 | `IsMouseOver` 为真，底色 `#F8F8F8`（SurfaceMuted） |
| 悬停最大化 | WPF 的 `IsMouseOver` 为假、按钮仍显示悬停：真实 `WM_NCHITTEST` 经钩子返回了 `HTMAXBUTTON` |
| 悬停关闭 | 底色 `#CD1526`，图标白色；深色主题相同 |
| 点击标题栏里的分段切换 | 选中项从 0 变为 1 |
| 拖动标题栏 | 窗口移动 (80, 40) |
| 拖右边缘 | 宽度 +60 |
| 真实点击最大化 | 进入 Maximized，名称变为 Restore，根边距 4 |
| 双击标题栏 | 还原；再双击最大化 |
| 固定大小窗口点最大化 | 按钮禁用，无反应 |
| 切到另一窗口 | 标题与图标变为 TextMuted |
| 点击关闭 | 窗口关闭 |

- 测试程序的教训：在标题栏上按下会进入系统的模态移动循环，同步等待的测试代码发不出松开，进程卡住。拖动和双击要用定时器在循环里继续发送输入。

## 未能验证

- **最大化后的画面**：WPF 报告 1288×1108（屏幕 1280×1100，每边越出 4，根边距 4 收回），但 Wine 截图显示的是最大化前的旧画面。普通 `Window` 和关闭玻璃的 ArcWindow 都一样，是 Wine 截图的问题，最大化视觉需要在真实 Windows 上看。
- DWM 深色边框、圆角、边框色：Wine 没有 DWM。
- Windows 11 贴靠布局浮层、系统菜单、高 DPI、多显示器最大化、任务栏自动隐藏时的最大化。

## 未运行

真实 Windows 10/11 窗口、触摸、屏幕阅读器。
