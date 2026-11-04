# ScrubInput 参考卡

## 来源与固定范围

- 页面：Arc 免费 Registry 的 `scrub-input`（MIT），2026-11-03 冻结。
- 快照：`tests/Baselines/References/2026-11-03-scrub-input/`。
- 批次：2026-11-03 第八批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 尺寸 | Medium 36/半径 12/14px；Small 30/半径 9/12px | 源码确认 | 一致 |
| 布局 | 手柄最小 30，内边距 0 4 0 10；单位最小 18×22 | 源码确认 | 一致 |
| 画布 | 260 高，半径 34，圆点 20 间距，尺寸胶囊 80.6×24.8 @ y220.2 | 运行观察：`scrub-input.txt` | 一致；插图原创 |
| 面板 | y274 高 198，半径 26，内边距 14，行距 8 | 运行观察 | 一致 |
| Reset frame | y486，36 高，128.3 宽 | 运行观察 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/ScrubInput.md)。回归：`tests/ArcWpf.Tests/ScrubInputTests.cs`，Gallery 页 `ScrubInputPage`。
