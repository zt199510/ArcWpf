# Gauge

状态：待验收。270 度环形读数，带百分比、阈值状态和说明。事实见[参考卡](../references/components/Gauge.md)，验证见[验证记录](../verification/2026-11-03-batch8-replication.md)。实现在 `src/ArcWpf.Controls/Gauge/Gauge.cs`。

## 范围与 API

`Value`、`Minimum`（0）、`Maximum`（100）、`Label`、`Detail`、`Tone`（`ArcTone`：Accent/Success/Warning/Danger）、`Thresholds`（`IList<GaugeThreshold>`，XAML 用 `GaugeThresholdCollection`；`GaugeThreshold{From,Tone,Label}`，取已达到的最高一档）。控件不可聚焦，自动化为 ProgressBar + RangeValue（只读），状态文本如 `72%, Healthy`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 首次入视口：环 sweep 与计数同时填充 | `physical(.768)`；源码确认 | `Gauge.Drive/Frame` | `RingAndCountFillToTheValueOnceSeen`；gauge 对照图 | 真实窗口未观察 |
| 首次填充落定：状态文字出现，档位此后跟随屏幕上的计数 | `abs(count-pct)<1`；源码确认 | `Gauge.Frame` | `StateAppearsAfterTheFirstFillAndFollowsTheBand` | 真实窗口未观察 |
| 值变化：sweep 用 morph 弹簧，计数用 smooth，数字盒宽度 smooth | `spring.morph/.smooth`；源码确认 | `Gauge.Drive/Sync` | g-high/g-mid/g-low 对照图 | 真实窗口未观察 |
| 档位变化：环、轨道、状态文字变色 | .24s standard；源码确认 | `Gauge.Paint` | g-mid/g-low 对照图 | 真实窗口未观察 |
| 文字替换：旧文字上移淡出，新文字上升淡入 | y±.3em、blur 4→0/0→2；源码确认 | `CopyMotion`、`SwapText` | 未在真实窗口观察 | 真实窗口未观察 |
| 减弱动效：直接到位 | 源码确认 | `Gauge.Drive` | 未单独运行 | 真实窗口未观察 |

## 已知差异

- 主示例三档阈值的准确边界未在页面源码暴露，取 30、70，只确认 warning 起点在 (28,40]、success 起点在 (64,76]，待核对。
- SVG `pathLength` 改为按 sweep 每帧重建弧线（WPF 适配），端点圆帽位置一致。
