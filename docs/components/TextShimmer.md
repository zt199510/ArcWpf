# TextShimmer

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/TextShimmer.md)，验证见[验证记录](../verification/2026-11-03-batch7-replication.md)。

## 范围与 API

本项目 API：`Text`、`IsActive`（默认 true）、`Duration`（默认 1.8）、`LineRatio`、`BaseColor`（默认文字色 12% 混入 TextMuted）。忙碌时 `AutomationProperties.ItemStatus` 为 Busy。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 循环 | 线性扫过 Duration，间隔 Duration×.32，首次延迟 .12，暂停后从原处继续 | `Pass`、`Restart` | 测试（定格）+截图 | Wine 下未测实际循环节奏 |
| 光带 | 宽 2.2em，七段衰减，Oklab 混色 | `PaintLabel`、`OkMix` | 测试+定格截图 ts-s0..4 | CSS 会钳制越界色标，WPF 把渐变扩到文字外，两端极小差异 |
| 结束 | 光带滑出 min(.42,dur×(1-t)×.5) enter；沉淀 .36 standard（减弱 .12） | `OnActive` | 测试+截图 ts-done | 无 |
| 换标签 | 旧 y -.24em、模糊 subtle，.16；新 y .3em、模糊 soft，.24 enter；宽度弹簧 | `Swap` | 测试 | 逐帧未比对 |
| 减弱/关闭 | 不扫光，只显示底色 | `Restart` | 测试 | 无 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
