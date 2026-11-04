# Gauge 参考卡

## 来源与固定范围

- 页面：Arc 免费 Registry 的 `gauge`（MIT），2026-11-03 冻结。
- 快照：`tests/Baselines/References/2026-11-03-gauge/`。
- 批次：2026-11-03 第八批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 尺寸 | 环宽上限 176，高 .9 倍，viewBox 100×90，r42，描边 9 | 源码确认：`gauge.tsx`、`module.css` | 一致 |
| 读数 | 36/36 w500，tracking -1.08px；单位 18，状态 12/16.8 | 运行观察：`gauge.txt` | 一致；替身字体致字宽差 |
| 说明 | 14/19.6 w500，间距 3，详情 12/16.8 | 运行观察 | 一致 |
| 主示例 | 240 宽，Workspace health，72，步长 12，按钮 36×36 间距 8 | 运行观察 | 一致 |
| 阈值 | warning 起点 (28,40]，success 起点 (64,76] | 运行观察，取 30/70 | 待核对 |

## WPF差异和回归入口

差异见[组件规格](../../components/Gauge.md)。回归：`tests/ArcWpf.Tests/GaugeTests.cs`，Gallery 页 `GaugePage`。
