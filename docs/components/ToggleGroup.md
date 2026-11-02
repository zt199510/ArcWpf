# ToggleGroup

状态：已实现，专项 2/2 通过；真实窗口焦点、主题、DPI 和动效观感待验收。

来源：Arc 免费组件 [ToggleGroup](https://uiarc.dev/components/toggle-group)，冻结批次 `tests/Baselines/References/2026-11-02-toggle-group/`。

公开契约：`Label`、`Type`（Single/Multiple）、`Size`、ListBox 选择绑定。单选保持一个选择，多选可独立切换；箭头/Home/End 移动焦点，Space/Enter 激活；禁用项跳过焦点。WPF 复用 ListBox/ToggleGroupItem，保留原生选择和 UIA。
