# TextShimmer 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/text-shimmer（免费 Registry，MIT）。
- 快照：`tests/Baselines/References/2026-11-03-text-shimmer/`，含 Registry 源码、CSS、页面和 motion 令牌，证据等级**源码确认**；几何与截图为**运行观察**。
- 批次：2026-11-03，项目采集标识，非官方版本。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 卡片 | 521.6×135.4，圆角 26，描边 1 | DOM 几何 | 一致 |
| 主行 | sparkles 18 @23,25；标题 18/25.2 中等 @53,21；meta 14/19.6 TextMuted @53,48.2 | DOM 几何 | 一致 |
| 子行 | y85.8 上描边；folder-sync 16 @23,102.6；文字 14/19.6 @49,100.8 | DOM 几何 | 一致 |
| 完成态 | 图标换成 ArcSuccess 对勾；Finish→Start again | 截图 ts-done | 一致 |

## WPF差异和回归入口

- **字距**：Arc 继承 -.16px 字距，本控件用 `TextBlock` 渐变前景绘制，没有字距，文字略宽。
- **可见性（WPF适配）**：Arc 用 inView(margin 64) 暂停，这里只用 `IsVisible`，滚出视口仍在运行。
- 用户提示复用 Skeleton：Skeleton 是透明度脉动，与渐变扫光不同，未复用。
- 回归：`tests/ArcWpf.Tests/TextShimmerTests.cs`。
