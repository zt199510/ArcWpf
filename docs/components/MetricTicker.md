# MetricTicker

状态：待验收。实时指标条：放不下时循环漂移，数字滚动，变化时增量闪烁，悬停展开走势面板。事实见[参考卡](../references/components/MetricTicker.md)，验证见[验证记录](../verification/2026-11-03-batch9-replication.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`Metrics`（`IList<TickerMetric>`，按 Id 匹配）、`Overflow`（Marquee/Scroll）、`Speed`（32）、`FlashDuration`（1.6s）、`HistoryLength`（24）、`Locale`、`Label`。`TickerMetric`：`Id`、`Label`、`Value`、`Format`、`Unit`、`Delta`、`Positive`、`History`、`Caption`。

## 复用情况

复用弹簧与帧循环、`SwapText`、`FocusVisible`。任务点名的 `AnimatedCounter`、`TextMorph` **未直接复用**：需要 9→0 环绕和逐位 25ms 错峰，单独写了 `OdometerText`（`Motion/LiveKit.cs`）。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 漂移与悬停减速 | 32 DIP/s，时间常数 .16s；文档描述与运行观察 | `Frame` | 真实鼠标：悬停后 0.5s 位移 0.01 | 时长近似 |
| 数字滚动 | 逐位错峰 25ms；运行观察 | `OdometerText` | `NumbersFollow...`；metric-ticker | — |
| 增量闪烁 | 1.6s 后回到静止色；运行观察 | `Live.FlashUntil` | `NumbersFollow...` | — |
| 面板展开与跟随 | 缩放 .86→1 + 走势描线；运行观察 | `StepPanel` | 真实鼠标：mt-panel | — |
| 拖动与甩动 | 按住跟手，松手带速度；文档描述 | `BeginDrag`/`MoveDrag`/`EndDrag` | 真实鼠标拖动 | 甩动手感未在真实窗口评估 |
| 减弱动效 | 不循环，改为普通滚动；文档描述 | `Looping` | 未单独运行 | 待验证 |

## 已知差异

- Wine 下持续漂移会占满渲染，Gallery 的实时帧率不代表 Windows。
- Gallery 数据为本项目模拟。
