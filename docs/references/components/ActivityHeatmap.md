# ActivityHeatmap 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/activity-heatmap；Registry：https://uiarc.dev/r/activity-heatmap.json（MIT，版权 Elia Kuratli），2026-11-04 采集。
- 快照：`tests/Baselines/References/2026-11-04-activity-heatmap/`，含 `manifest.json`、`license.txt`、Registry 源码与 motion 令牌。`registry.txt` SHA-256：dbba257f5b337cb9ec7081e0cc6240b93ccb1e1438c09552f1c450df5435f499。
- 演示数据读自公开预览页，放在 `samples/ArcWpf.Gallery/Pages/ChartDemoData.cs`。
- 批次：2026-11-04 第十批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 网格 | 周列日行，格子 9 到 15，间距 3 | 源码确认 | 一致；`CellsClamp...`、`WideCards...` |
| 等级 | 0 为空，1-4 按阈值；默认按最大值四等分 | 源码确认 | 一致；`ModelLays...`、`DefaultBounds...` |
| 气泡 | `4 contributions` / `Tue, Apr 29, 2025` | 运行观察 | 一致，见 heat-hover 对比图 |
| 演示阈值 | 1–3、4–6、7–10、11+ | 运行观察 | Gallery 设 `[3, 6, 10]` |

## WPF差异和回归入口

差异见[组件规格](../../components/ActivityHeatmap.md)。回归：`tests/ArcWpf.Tests/ActivityHeatmapTests.cs`，Gallery 页 `ActivityHeatmapPage`。
