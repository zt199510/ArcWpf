# TextMorph

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/TextMorph.md)，验证见[验证记录](../verification/2026-11-03-batch7-replication.md)。

## 范围与 API

本项目 API：`Text`、`Tracking`（em）、`LineRatio`（行框/字号，默认字体自然行高）。字号、字体等沿用 `Control` 属性。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 文字变化 | 相同字符（按“字符-序号”作键）沿布局滑动，弹簧 morph .42 | `Sync` | 测试+截图 tm-publishing | 无 |
| 新字母 | 透明度 0→1、缩放 .8→1、y .14em→0、模糊 soft→0，.28 enter，延迟 .05+min(序,8)×.032 | `Enter` | 测试 | 逐帧未比对 |
| 移除字母 | 透明度→0、缩放 .86、y -.1em、模糊 soft，.18 standard，留在原位 | `Leave` | 测试 | 逐帧未比对 |
| 宽度 | 差值 >.5 时弹簧 morph，字体变化立即跟随 | `Sync`、`Apply` | 测试 | 无 |
| 边缘羽化 | frame 右侧 W 到 W+.5em 渐隐，静止时移除 | `_mask` | 定格截图 tm-mask | CSS 遮罩还会裁掉框外内容，WPF 只羽化右缘 |
| 减弱/关闭动效 | 立即替换 | `Moving` | 测试 | 无 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
