# ImageCompare

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/ImageCompare.md)，验证见[记录](../verification/2026-11-03-batch5-replication.md)。

## 范围与 API

`Before`、`After`、`Position`、`Orientation`、`BeforeLabel`、`AfterLabel`、`ShowLabels`、`Label`、`AspectRatio`、`PositionChanged`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 拖动 | 跟手，边缘橡皮筋 | `ImageCompareMath` | 测试+截图 | 鼠标拖动由测试入口驱动 |
| 转向 | 分隔线旋转 | `_theta` 弹簧 | 测试+截图 img-v | 中间帧未比对 |
| 手柄变形 | 圆变胶囊 | `_capW/_capH` | 未对比 img-drag | 待真实窗口 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
