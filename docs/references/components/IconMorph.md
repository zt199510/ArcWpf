# IconMorph 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/icon-morph（免费 Registry，MIT）。
- 快照：`tests/Baselines/References/2026-11-03-icon-morph/`，含 Registry 源码、CSS、页面和 motion 令牌，证据等级**源码确认**；几何与截图为**运行观察**。
- 批次：2026-11-03，项目采集标识，非官方版本。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 示例 | 4×2 宫格，间距 8，格 124.4，圆角 26，图标 28 @y33.8，标签 12/16.8 @y73.8 | DOM 几何 | 一致 |
| 格子状态 | 静止 Border+TextSecondary；按下 BorderStrong+Foreground；Added/Copied 为 ArcSuccess；Dark 反色；喜欢 ArcDanger | 计算样式、截图 im-on | 一致 |
| Copied | 约 1.8s 后自动恢复 | 页面行为 | Gallery 实现一致 |

## WPF差异和回归入口

- 源码自带全部路径和参数，直接按它改写，未组合 ActionSwap、ThemeSwitch（用户提示复用）；只复用了 `SpringDriver`、`Tween`、`ArcEasing`。
- Gallery 格子的颜色变化是立即的，Arc 的过渡未测量。
- 回归：`tests/ArcWpf.Tests/IconMorphTests.cs`。
