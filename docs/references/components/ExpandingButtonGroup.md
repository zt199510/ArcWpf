# ExpandingButtonGroup 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/expanding-button-group；免费 Registry（MIT），快照 `tests/Baselines/References/2026-11-03-expanding-button-group/`。
- 批次：2026-11-03，项目采集标识，非官方版本。证据等级：结构与参数为源码确认，尺寸为 DOM 几何。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 三行 | y 0（高 44）、y 64（小号高 36）、y 120（高 44） | DOM 几何 | 一致 |
| 内容 | Reply…Delete；Download/Copy link/Rename；Publish/Duplicate/Pin/Delete | 运行观察 | 一致 |
| 展开项宽 | Reply 97.03（含过去式占位） | DOM 几何 | 结构一致；数值受替身字体影响 |

## WPF差异和回归入口

- Gallery 的第三行 Delete 设为禁用、Archive 带 500ms 异步，用于演示状态，Arc 演示未必如此。
- 复用弹簧与 Tooltip 工具；展开逻辑按 Arc 源码自写，未直接组合 ButtonGroup、ExpandingSearch。
- 回归：`tests/ArcWpf.Tests/ExpandingButtonGroupTests.cs`。
