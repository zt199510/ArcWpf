# IconMorph

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/IconMorph.md)，验证见[验证记录](../verification/2026-11-03-batch7-replication.md)。

## 范围与 API

本项目 API：`Icon`（Menu/Play/Plus/Copy/Theme/Chevron/Bell/Heart）、`State`、`Size`、`StrokeThickness`、`Turn`、`Pointing`、`Foreground`、`LikeBrush`。控件只绘制、不接收指针，名称和按下状态由外层按钮提供。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 形状变形 | 按源码路径插值；Menu/Play/Plus/Theme/Chevron 用弹簧 morph，Copy/Bell 用 smooth | `OnState`、`Draw*` | 测试+定格 im-big | 无 |
| Menu 中线 | 透明度 fast，scaleX→.2 snappy | `DrawMenu` | 定格 | 无 |
| Copy 对勾 | pathLength smooth，延迟 .06 | `_late` | 定格 | 无 |
| Bell 摇摆 | [0,16,-13,9,-6,3,0] .9s，舌 .04 后 | `Keyframe` | 测试+定格 | motion 的 easeOut 用二次缓出近似 |
| Heart 爆发 | 环 r4→13，七火花 .55 延迟 .05，心 [.72,1.14,1] .45 | `DrawHeart` | 测试+定格 | 心跳按两段 enter 近似；颜色用 sRGB 插值 |
| 减弱/关闭 | 立即到位，无一次性动效 | `Snap` | 测试 | 无 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
