# FunnelChart 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/funnel-chart（Arc Pro，无公开版本号），2026-11-04 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。
- 参考文件不入库，放在 `work/refs/batch10/funnel-chart/`。SHA-256：`markdown.txt` 736f13e8bbf3c094347779fe7714fc86dbeb1e7a51049572bc85f6f2300edc37；`page.html` f9e4d52ec9425fd337fe6cb115cc1b0cfa5177a2e08d48013438c62d52ef2835。
- 批次：2026-11-04 第十批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 色带 | 每步平直，步界两侧各 .24 缓动 | 运行观察 | `TheBandIsFlat...` |
| 分离视图 | 间隙 2，顶 7，底 193 | 运行观察 | 一致 |
| 分段视图 | mobile 39.7K、2.3%；desktop 31.6K、50% | 页面文本确认 | 一致 |
| Tooltip | `Entry step, sessions`，分段行，`27% kept from viewed product` | 页面文本确认 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/FunnelChart.md)。回归：`tests/ArcWpf.Tests/FunnelChartTests.cs`，Gallery 页 `FunnelChartPage`。
