# RulerSlider

状态：待验收。卷尺式取值：刻度在固定指针下滑动。事实见[参考卡](../references/components/RulerSlider.md)，验证见[验证记录](../verification/2026-11-03-batch8-replication.md)。实现在 `src/ArcWpf.Controls/RulerSlider/RulerSlider.cs`。

## 范围与 API

继承 `RangeBase`。`Label`、`Step`、`LargeStep`、`MajorEvery`（10）、`Spacing`（10）、`Format`、`Unit`；事件 `ValueCommitted`。只绘制可见刻度。横向滚动经 `WM_MOUSEHWHEEL` 读取。自动化为 Slider，状态含单位。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 拖动：1:1 移动，4px 内算点击 | 源码确认 | `Move` | `DragMovesTheTapeAgainstThePointerOneToOne` | 真实窗口未观察 |
| 甩动：惯性落到刻度，途中读数滚动，静止后提交 | power .42，τ 260ms；源码确认 | `End/Frame` | `FlickCoastsOntoATickAndCommitsAtRest` | 真实窗口未观察 |
| 抓住滑行中的带：就地停下 | 源码确认 | `Down` | `GrabbingACoastingTapeCatchesIt` | 真实窗口未观察 |
| 越界：橡皮筋后带速度回位 | 上限 44，`physical(.5,.1)`；源码确认 | `Move/End` | `PullingPastAnEndRubberBandsHome`；rs-end 对照图 | 真实窗口未观察 |
| 点击/按键/外部赋值：弹簧滑到目标并保留速度 | `physical(.26)`；源码确认 | `GlideTo` | `SmallMovementIsATapThatGlides`、`KeysStepAndOutsideValuesGlide` | 真实窗口未观察 |
| 读数：每位数字条滚动，可中途改目标；增减位数时宽度弹簧 | .24s standard、`spring.morph`；源码确认 | `RulerDigits` | `DigitsKeepTheirPlaceAsTheNumberGrows` | 真实窗口未观察 |
| 减弱动效：直接到位 | 源码确认 | `Reduced` 分支 | 未单独运行 | 真实窗口未观察 |

## 已知差异

- Arc 在首次鼠标按下时也显示焦点环（同 ElasticSlider，运行观察）；WPF 只在键盘操作时显示。
- 横向滚轮只走窗口消息，未在真实设备验证；`WheelForTest` 只覆盖数值逻辑。
- grab 光标用 Hand 近似。
