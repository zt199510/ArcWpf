# Sparkline

状态：待验收。可逐点查看的小型折线图，带标题值和变化。事实见[参考卡](../references/components/Sparkline.md)，验证见[验证记录](../verification/2026-11-03-batch8-replication.md)。实现在 `src/ArcWpf.Controls/Sparkline/Sparkline.cs`。

## 范围与 API

`Data`、`Label`、`Value`、`Change`、`Tone`、`DrawWidth`（160）、`DrawHeight`（52）、`Labels`、`FormatValue`（`Func<double,int,string>`）、`ShowArea`（true）、`IsInteractive`（true）。源码 `width/height/area/interactive` 因与 WPF 属性冲突改名。绘图区可聚焦，自动化为 Slider + RangeValue。键盘：方向键、PageUp/PageDown（点数/6）、Home/End、Escape。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 首次入视口：线从左到右绘制，面积跟随笔尖，落定后出现端点 | .84s inOut；源码确认 | `Sparkline.StartDraw/Draw` | `PenStopsPartWayAlongTheLine`；mid-spark 帧 | 真实窗口未观察 |
| 换数据：从屏幕上的形状变形 | `spring.smooth`，共享 x 采样；源码确认 | `Sparkline.OnData/OnMorph` | `NewDataEndsOnTheNewCurve`；sp-1/sp-2 对照图 | 真实窗口未观察 |
| 悬停/拖动/按键：游标弹簧到点并沿线移动，后段变暗，端点 7→10 | `spring.snappy`、.16s；源码确认 | `Sparkline.Aim/Marks/Draw` | `CursorSpringsToThePointAndHome`；sp-scrub 对照图 | 真实窗口未观察 |
| 文字变化：数字按位滚动，未变位不动，宽度弹簧 | `spring.morph`、y±.3em；源码确认 | `RollText` | `RollTextKeepsUnchangedDigitsStill` | 真实窗口未观察 |
| 减弱动效：绘制和游标直接到位，文字淡入淡出 | 源码确认 | `Sparkline.Moving`、`CopyMotion` | 未单独运行 | 真实窗口未观察 |

## 已知差异

- SVG 描边 `pathLength` 改为按长度截断折线（WPF 适配）。
- Arc 的 sp-1/sp-2 截图带焦点环，是探测脚本先聚焦绘图区所致，不属于这些状态的默认外观。
