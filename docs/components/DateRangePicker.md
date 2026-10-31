# DateRangePicker 组件规格

状态：实现中。Arc `date-range-picker` Registry 在 2026-10-31 当前环境无法访问，未伪造来源参数；实现复用 ArcWpf Calendar/DatePicker 的已冻结模板和令牌。

## API

`DateRangePicker : Control`：`StartDate`、`EndDate`、`MinimumDate`、`MaximumDate`、`BlackoutDates`、`Label`、`Placeholder`、`IsOpen`，以及 `RangeChanged` 事件。

## 范围

- 双 Calendar Popup，开始/结束日期选择和自动排序。
- 最小/最大日期、BlackoutDates、Clear、Escape/外部关闭和焦点路径。
- 复用 Arc Calendar 样式；Popup 采用 660 DIP 双栏布局。
- Range band、hover 预览、响应式窄屏和来源专属动效待 Registry 恢复后补齐。
