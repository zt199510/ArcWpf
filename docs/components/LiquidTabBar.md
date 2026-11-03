# LiquidTabBar

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。**Arc Pro 组件，开源分发前需项目负责人确认许可。** 事实见[参考卡](../references/components/LiquidTabBar.md)，验证见[验证记录](../verification/2026-11-03-batch6-replication.md)。

## 范围与 API

本项目自定：`Tabs`（`LiquidTab`：`Id`、`Text`、`Icon`、`Count`）、`SelectedId`、`ShowLabels`（Selected/All/None）、`Label`；事件 `SelectionChanged`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 切换 | 两缘先后移动（.275、.476，延迟 .05，拟合） | `Relayout` | 测试+截图 ltb-inbox | 参数为拟合 |
| 悬停 | 朝目标方向伸出 6.44 | `Lean` | 测试 | 无 |
| 文字进出 | 延迟 .32、时长 .48（推测） | `TabView.Refresh` | 测试 | 参数为推测 |
| 模式切换 | 宽度 .42（推测） | `Relayout` | 测试+截图 ltb-all、ltb-icons | 参数为推测 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
