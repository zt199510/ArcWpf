# RealtimeStream

状态：待验收。实时折线：窗口随时间平滑滚动，新点从右侧进入，异常带标记，悬停保持并逐点读数。事实见[参考卡](../references/components/RealtimeStream.md)，验证见[验证记录](../verification/2026-11-03-batch9-replication.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`Points`（`IList<StreamPoint>`，ObservableCollection 自动跟随）、`Label`、`Unit`、`Window`（30s）、`Interval`（1s）、`PlotHeight`（200）、`Paused`、`PauseOnHover`、`Min`、`Max`、`EmptyLabel`、`FormatValue`、`FormatTime`、`Refresh()`；事件 `PausedChanged`。`StreamPoint(Time, Value, Anomaly?)`。

## 复用情况

任务点名的 `Timeline`、`ChatThread`、`ScrollArea` **均未复用**：Arc 的 realtime-stream 实际是画布折线图，不是事件流。复用 `OdometerText`、`SwapText`、帧循环。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 持续滚动 | 视图滞后最新点一个间隔；落后超 1.5 个间隔时 1.5 倍追赶；文档描述 | `Frame` | `TheViewTrails...` | — |
| 悬停保持 | 状态变 `Paused, n new`；运行观察 | `Held`、`SyncHead` | 真实鼠标：rs-hover；`HoverHolds...` | — |
| 恢复 | 平滑回到实时，时间常数 .14s；文档描述 | `Resume`、`_gliding` | `HoverHolds...` | — |
| 异常标记弹出 | 一次过冲；文档描述 | `DrawAnomalies` | realtime-stream | 真实窗口未观察 |
| 纵轴缓动 | 跟随数据范围；文档描述 | `EaseAxis` | `AxisLeavesRoom...` | 系数为近似 |
| 减弱动效 | 视图直接贴到最新点；文档描述 | `Sync` | 未单独运行 | 待验证 |

## 已知差异

- 纵轴范围是由截图反推的近似值（待核对）；深色模式图表颜色未测量。
- Gallery 数据为本项目模拟。
- Wine 下帧率不代表 Windows；持续重绘的真实开销未在 Windows 测量。
