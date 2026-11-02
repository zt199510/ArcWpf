# ToggleGroup

状态：已按线上页面重做，专项通过；离屏对照见 [像素对照记录](../verification/2026-11-02-theme-toggle-actions-pixel.md)。真实窗口焦点、主题、DPI 和动效观感待验收。

来源：Arc 免费组件 [ToggleGroup](https://uiarc.dev/components/toggle-group)，冻结批次 `tests/Baselines/References/2026-11-02-toggle-group/`。

公开契约：`Label`、`Type`（Single/Multiple）、`Size`、`Value`/`Values`/`ValueChanged`、ListBox 选择绑定，项支持 `Icon`。单选用滑动填充（SpringDriver），多选相邻项填充跨过 2 的间隙并拉直相接圆角；只有一个项是 Tab 停靠点。小尺寸按 CSS 是 padding 8 + 图标 14，项宽 30、高 28。单选保持一个选择，多选可独立切换；箭头/Home/End 移动焦点，Space/Enter 激活；禁用项跳过焦点。WPF 复用 ListBox/ToggleGroupItem，保留原生选择和 UIA。
