# CardStack 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/card-stack；免费 Registry（MIT），快照 `tests/Baselines/References/2026-11-03-card-stack/`。
- 批次：2026-11-03，项目采集标识，非官方版本。证据等级：结构与参数为源码确认，尺寸为 DOM 几何。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 尺寸 | 根 320×392，舞台 328，按钮行 y 348，高 44 | DOM 几何 | 一致 |
| 内容 | Lisbon、Copenhagen、Porto、Kyoto、Edinburgh、Seville 共 6 张 | 运行观察 | 一致 |
| 卡片排版 | 内边距 20；目的地 36/39.6；说明 14/19.6 | 计算样式 | 一致 |
| 空状态 | Nothing shortlisted 或 Shortlist ready；说明列出城市；按钮高 36 | 运行观察 | 一致；标题与说明由演示提供 |
| 禁用 | Skip、Shortlist 透明度 .42，Undo 仍可用 | 计算样式 | 一致 |

## WPF差异和回归入口

- 主按钮不画边框：同色描边和填充在 WPF 圆角处会留下浅色接缝（WPF适配）。
- 拖动逻辑按 Arc 源码自写，未复用 Carousel：两者的手势与飞出模型不同。
- 回归：`tests/ArcWpf.Tests/CardStackTests.cs`。
