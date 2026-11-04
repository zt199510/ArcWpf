# InViewTitle 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/in-view-title（免费 Registry，MIT）。
- 快照：`tests/Baselines/References/2026-11-03-in-view-title/`，含 Registry 源码、CSS、页面和 motion 令牌，证据等级**源码确认**；几何与截图为**运行观察**。
- 批次：2026-11-03，项目采集标识，非官方版本。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 示例 | 舞台高 104.4，标题 43.8165/48.2，字距 -.03em，左对齐垂直居中 | DOM 几何 | 一致 |
| 说明行 | y118.4，14/19.6，TextSecondary，五种文案 | DOM 几何 | 位置一致；Arc 交叉淡入，Gallery 立即切换 |
| 控制区 | y162，SegmentedControl 329.9×44，间距 10，Replay 94×36 | DOM 几何 | 结构一致，分段宽随字体变化 |

## WPF差异和回归入口

- “已通过”判断依赖滚动事件：未放在 `ScrollViewer` 内时以窗口内容为视口。
- 回归：`tests/ArcWpf.Tests/InViewTitleTests.cs`（含 ScrollArea 集成）。
