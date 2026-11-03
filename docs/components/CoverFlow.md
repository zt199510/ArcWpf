# CoverFlow

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。**Arc Pro 组件，开源分发前需项目负责人确认许可。** 事实见[参考卡](../references/components/CoverFlow.md)，验证见[验证记录](../verification/2026-11-03-batch6-replication.md)。

## 范围与 API

本项目自定：`ItemsSource`（`CoverFlowItem`：`Image`、`Title`、`Subtitle`）、`SelectedIndex`、`Label`；事件 `SelectionChanged`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 滑动 | 弹簧 .3（拟合） | `Glide` | 测试+截图 cf-next | 参数为拟合 |
| 拖动 | 一张卡宽对应一格，两端阻力 | `Drag`、`Resist` | 测试 | 阻力系数为推测 |
| 甩动 | 速度推移，最多一张 | `Landing` | 测试 | 系数为推测 |
| 远卡淡出 | 距离 2 到 2.7 线性（由 DOM 拟合） | `Pose` | 测试 | 无 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
