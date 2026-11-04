# InViewTitle

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/InViewTitle.md)，验证见[验证记录](../verification/2026-11-03-batch7-replication.md)。

## 范围与 API

本项目 API：`Text`、`Variant`（Blur/Word/Line/Tracking/Wipe）、`Lines`、`Once`（默认 true）、`Tracking`、`LineRatio`，方法 `Replay()`。自动查找祖先 `ScrollViewer`，因此可放在 `ScrollArea` 内。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 入视口 | 可见面积 ≥.45，或顶部已在视口上方 | `Measure`、`Evaluate` | 测试（ScrollArea 内） | 无 |
| 显示节奏 | 位移 settle enter；模糊 settle×.78；透明度 settle×.56 standard | `Show` | 测试 | 无 |
| 隐藏 | 全部 .18 standard，无错开 | `Hide` | 测试 | 无 |
| Blur/Word | y .2em+模糊 8 / y .9em 裁切；step=min(.06,.4/词数) | `BuildBlur`、`BuildWords` | 测试+截图 | 无 |
| Line | y 112%，错开 .08，settle .86，行距 lh+.06em | `BuildLines` | 测试+截图 iv-2 | 无 |
| Tracking | 词透明度+模糊 soft，字母 x (中心-i)×.06em，settle 1 | `BuildTracking` | 测试+截图 iv-3 | 无 |
| Wipe | x -.12em，遮罩 230% 宽 44%→56%，settle .9 | `BuildWipe`、`PaintWipe` | 测试+定格 iv-wipe | 无 |
| 减弱/关闭 | 立即显示 | `Evaluate` | 测试 | 无 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
