# ElasticSlider

状态：待验收。有弹性的单值滑块，带档位和随速度摆动的数值标签。事实见[参考卡](../references/components/ElasticSlider.md)，验证见[验证记录](../verification/2026-11-03-batch8-replication.md)。实现在 `src/ArcWpf.Controls/ElasticSlider/ElasticSlider.cs`。

## 范围与 API

继承 `RangeBase`（`Maximum` 默认 100）。`Label`、`Step`（1）、`LargeStep`、`Marks`（`ElasticSliderMarkCollection`）、`Format`、`ShowValue`、`StartIcon`、`EndIcon`；事件 `ValueCommitted`。控件本身接收焦点，自动化为 Slider。键盘同原生 range：方向键、Shift+方向、PageUp/PageDown、Home/End。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 按下：滑块放大 1.28，轨道增厚 | `spring.snappy`；源码确认 | `ElasticSlider.Down` | es-drag 对照图 | 真实窗口未观察 |
| 拖动越界：轨道拉伸变细，图标和滑块外推 | `rubber`，上限 26；源码确认 | `Follow/Paint` | `PullingPastAnEndStretchesAndSpringsBackWithOvershoot`；es-over 对照图 | 真实窗口未观察 |
| 释放：带速度回弹，会穿过静止位 | `physical(.5,.42)`；源码确认 | `End` | 同上（断言最小值 <0） | 真实窗口未观察 |
| 经过档位：7px 内吸附，刻度 1→2.1→1 | .34s enter；源码确认 | `DetentAt/Pulse` | `DetentHoldsAndReleases` | 真实窗口未观察 |
| 标签：滞后跟随并随速度摆动，边缘夹紧 | k420 c30 m.7，±11°；源码确认 | `Frame/Paint/Tag` | es-drag/es-max 对照图 | 真实窗口未观察 |
| 按键到极限：轨道按 260px/s 冲击后回弹 | 源码确认 | `Press` | `KeyAtALimitBumpsTheBand` | 真实窗口未观察 |
| 减弱动效：无拉伸，直接到位，标签只淡入淡出 | 源码确认 | `Reduced` 分支 | 未单独运行 | 真实窗口未观察 |

## 已知差异

- Arc 在首次鼠标拖动时也显示焦点环（Chrome 对脚本 `focus()` 的启发式，运行观察）；WPF 只在键盘操作时显示。若要跟随需用户决定。
- WPF 没有 grab 光标，用 Hand 近似；禁用元素不触发命中，`not-allowed` 光标不生效。
- 触控振动未实现，桌面 WPF 没有对应 API。
