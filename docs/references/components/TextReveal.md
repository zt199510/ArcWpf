# TextReveal 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/text-reveal（免费 Registry，MIT）。
- 快照：`tests/Baselines/References/2026-11-03-text-reveal/`，含 Registry 源码、CSS、页面和 motion 令牌，证据等级**源码确认**；几何与截图为**运行观察**。
- 批次：2026-11-03，项目采集标识，非官方版本。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 标题 | 52/57.2，字距 -.03em，三行平衡换行：Small details make / every interaction / feel considered | DOM 几何 | 换行一致 |
| 正文 | y187.6，宽 440，16/22.4，TextSecondary，Delay .32 | DOM、动画采样 | 一致 |
| Replay | y260.3，94×36 | DOM 几何 | 一致 |

## WPF差异和回归入口

- `text-wrap: balance` 由 `WordFlow` 用二分搜索近似：取不增加行数的最小宽度。浏览器算法可能不同，只验证了本示例。
- 用户提示复用 TextColoring：它是 TextBox 重绘辅助，不适用，未复用；动效令牌已复用。
- 回归：`tests/ArcWpf.Tests/TextRevealTests.cs`。
