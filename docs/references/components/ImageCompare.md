# ImageCompare 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/image-compare（MIT，免费 Registry）。
- 批次：2026-11-03，项目采集标识，非官方版本。快照在 `tests/Baselines/References/2026-11-03-image-compare/`（Registry 源码、页面文本、许可、manifest 哈希）。
- 用户图片：无。当前页面是否仍同版：未知，Arc 未公开版本号。
- 离屏截图未入库；WPF 对照结果见 [验证记录](../../verification/2026-11-03-batch5-replication.md)。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 文案、内容组合 | Before / After；Living room；Side by side / Stacked；Center | DOM | 文案一致；图片换为原创插画 |
| 初始值与状态 | Position 50；横向 | 源码确认 | 一致 |
| 控件本体尺寸/圆角 | 521.6×347.75；手柄 40 圆，拖拽时变胶囊 | DOM+源码 | 一致 |
| 外部间距 | 说明栏在图下 12，高 44 | DOM 几何 | 一致 |

## 状态与动效索引

弹簧与时长以快照内源码和 `motion.txt` 为准，状态矩阵见[规格](../../components/ImageCompare.md)。中间帧未逐帧比对。

## WPF差异和回归入口

手柄读数是 After 占比（`100 − Position`），与 Arc 的 aria-valuenow 一致。触控走提升的鼠标事件，未在真实设备运行。

回归：`tests/ArcWpf.Tests/ImageCompareTests.cs`。
