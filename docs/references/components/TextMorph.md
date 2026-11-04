# TextMorph 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/text-morph（免费 Registry，MIT）。
- 快照：`tests/Baselines/References/2026-11-03-text-morph/`，含 Registry 源码、CSS、页面和 motion 令牌，证据等级**源码确认**；几何与截图为**运行观察**。
- 批次：2026-11-03，项目采集标识，非官方版本。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 示例 | 521.6×143.2，标题 72/79.2，字距 -.03em，居中，Publish 宽 231.9 | DOM 几何 | 字宽随替身字体不同 |
| Next status | 高 36，圆角 18，内边距 13，14 中等 | 计算样式 | 一致（Medium 无替身） |
| Follow 胶囊 | 内边距 0 14 0 11，间距 6，图标 15；开启后底色和描边为前景色 | 计算样式、截图 | 一致 |

## WPF差异和回归入口

- 字形是逐字 inline-block，源码本身就没有字偶距，本实现相同。
- `Tracking` 由 `TrackedText` 绘制；WPF 的 `TextBlock` 没有字距。
- 回归：`tests/ArcWpf.Tests/TextMorphTests.cs`。
