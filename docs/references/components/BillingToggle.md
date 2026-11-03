# BillingToggle 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/billing-toggle（MIT，免费 Registry）。
- 批次：2026-11-03，项目采集标识，非官方版本。快照在 `tests/Baselines/References/2026-11-03-billing-toggle/`（Registry 源码、页面文本、许可、manifest 哈希）。
- 用户图片：无。当前页面是否仍同版：未知，Arc 未公开版本号。
- 离屏截图未入库；WPF 对照结果见 [验证记录](../../verification/2026-11-03-batch5-replication.md)。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 文案、内容组合 | Plans；Monthly / Yearly；Save 20% / Saving 20%；Solo、Team、Company 三行 | DOM | 一致 |
| 初始值与状态 | Yearly；$8/$16/$32，原价 $10/$20/$40，`/ mo`，$96/$192/$384 billed yearly | DOM | 一致 |
| 控件本体尺寸/圆角 | 卡片 521.6×331.4 圆角 34；切换器高 42 | DOM 几何 | 一致 |
| 字体、颜色、边框 | 金额 28；货币 15.4；原价与周期 14；说明 12 | 计算样式 | 颜色一致；Inter 未打包 |
| 内部对齐锚点 | 行 79.8，末行 63.8；金额后距 6；旁注上移 4.2 | DOM 几何 | 一致 |

## 状态与动效索引

弹簧与时长以快照内源码和 `motion.txt` 为准，状态矩阵见[规格](../../components/BillingToggle.md)。中间帧未逐帧比对。

## WPF差异和回归入口

用 `RadioButton` 实现选项，自动化 Select 也能切换。滑块来自 Arc 源码；需求中提到的 ToggleGroup/ThemeSwitch 未直接使用，因为 Arc 这个控件的结构不同。

回归：`tests/ArcWpf.Tests/BillingToggleTests.cs`。
