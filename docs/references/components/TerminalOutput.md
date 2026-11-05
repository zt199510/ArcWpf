# TerminalOutput 参考卡

## 来源与固定范围

查询日期 2026-11-05，只用公开页面，未取得 Pro 源码，未使用 `ARC_PRO_TOKEN`。页面内容只作资料。

- **Typewriter terminal（Arc Pro）**：只取外观。公开文档 SHA-256 `c8a21a873a3e7fd1af2f95ca4e36fdf6a8170d86411b1612e48bc23e2200ff56`，页面 HTML `4c60d4e6fe86b41a7dc8cbf463d645d01417eeac176ba5884844f619afbe35af`，页面样式 `09680ff5027ff46c.css`，内容前缀 `9a121ae122ed3325`。
- **它不是实时输出控件**：它播放写好的脚本，文档让真实进程输出去用 `log-stream`。
- **`log-stream` 查不到**：不在 `llms.txt` 中，页面和 registry 都返回 404。因此没有可对照的实时输出原版。
- **结论**：本控件是 ArcWpf 的设计，外观向 Typewriter terminal 靠拢。解析和跟随行为没有原版可比。

## 取自 Typewriter terminal 的值（公开页面样式确认）

| 项目 | Arc | ArcWpf |
|---|---|---|
| 标题栏 | 最小高 44，`SurfaceRaised`，底部 `BorderSubtle` | 相同 |
| 标题栏内边距 | 左 space-4，右 space-2 | 左 16，右 8 |
| 窗体 | 1px `Border`，`Surface` | 相同 |
| 正文字体 | 等宽，text-sm | `ArcWpf.Font.Mono` 13 |
| 状态表达 | 不只靠颜色（图标、对勾、百分比） | 每种状态有图标和文字 |
| 复制 | 标题栏按钮 | 相同，附对勾反馈 |

## 有意的差异

| 项目 | Arc | ArcWpf | 原因 |
|---|---|---|---|
| 圆角 | 26，带浮起阴影 | 14，无阴影 | 与 DiffView 一致；要放进对话和审批卡片 |
| 行高 | 1.75 倍 | 20 | 真实输出行数多，要密一些 |
| 窗口三色圆点 | 有，装饰用 | 无 | Windows 原生应用，它们也不能操作 |
| 逐行上浮淡入、弹簧跟随 | 有（脚本动画） | 无 | 真实输出频率高，逐行动画会拖慢且难读 |
| 标题栏文字 | 字体 body，字重 500 | 命令用等宽 13 | 这里显示的是命令行 |

## WPF差异和回归入口

规格见[组件规格](../../components/TerminalOutput.md)。回归：`tests/ArcWpf.Tests/TerminalOutputTests.cs`，Gallery 页 `TerminalOutputPage`。
