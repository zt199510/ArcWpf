# FloatingButtonGroup 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/floating-button-group；免费 Registry（MIT），快照 `tests/Baselines/References/2026-11-03-floating-button-group/`。
- 批次：2026-11-03，项目采集标识，非官方版本。证据等级：结构与参数为源码确认，尺寸为 DOM 几何。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 主行 | Editor actions，336.9×46，位于 92.3,0 | DOM 几何 | 高度一致；宽度受替身字体影响 |
| 竖排 | 46×215，位于 149.3,78，纯图标 | DOM 几何 | 一致 |
| 浮动小号 | 145×38，位于 227.3,166.5 | DOM 几何 | 一致 |
| 内容 | Undo/Redo/Comment/Share/Present 等，Redo 与 Delete、Zoom to fit 禁用 | 运行观察 | 一致 |

## WPF差异和回归入口

- 禁用项用 `IsInert`（aria-disabled 语义）：保留悬停和工具提示，不用 `IsEnabled`（WPF适配）。
- 高亮位于按钮着色之下；光标 WPF 无 grab 类型。
- 复用现有 Tooltip、弹簧与主题工具；未直接组合 ButtonGroup、Popover，因为 Arc 这里是单块共享高亮的独立结构。
- 回归：`tests/ArcWpf.Tests/FloatingButtonGroupTests.cs`。
