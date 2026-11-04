# TextReveal

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/TextReveal.md)，验证见[验证记录](../verification/2026-11-03-batch7-replication.md)。

## 范围与 API

本项目 API：`Text`（换行符为硬换行）、`Kind`（Heading/Paragraph）、`Delay`、`Tracking`、`LineRatio`、`MaxCharacters`（默认 24，≤0 不限），方法 `Replay()`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 进入 | 词从裁切框升起 .62em→0，.76 enter | `Replay` | 测试+逐帧 tr-f0..3 | 逐帧与 tr-mid 目视一致，未数值比对 |
| 淡入/清晰 | 透明度 .44 standard；模糊 8（段落 4）→0，.58 enter | `Replay`、`TextMotion.Blur` | 测试+逐帧 | 无 |
| 错开 | step=min(.04,.48/词数)，延迟=Delay+位置×step+行×step | `Rebuild` | 测试 | 无 |
| 底部羽化 | 裁切框底部 .18em 渐隐，静止后移除 | `Word.Mask` | 测试+逐帧 | 无 |
| 减弱 | 只淡入，.16 standard，无延迟 | `Replay` | 测试 | 无 |
| 关闭 | 不播放 | `Replay` | 未单独测试 | 无 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
