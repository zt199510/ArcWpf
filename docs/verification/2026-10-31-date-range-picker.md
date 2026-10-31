# DateRangePicker 验证记录（2026-10-31）

## 来源限制

`https://uiarc.dev/r/date-range-picker.json` 当前环境访问被拒绝，未完成 Registry/CSS 冻结；实现依据已冻结 Calendar/DatePicker WPF 基线。

## 待运行

- Controls/Gallery 构建。
- DateRangePicker 专项测试。
- 实际窗口的双栏比例、范围选中、Popup 焦点、BlackoutDates、明暗主题、窄宽度和多 DPI。

## 程序化结果

- Controls 构建：成功，0 错误。
- DateRangePicker 专项：3/3 通过，使用现有构建引用执行。
- Gallery 普通构建：旧 Gallery 进程仍锁定输出 DLL，未结束用户进程。
- 来源 Registry：当前网络策略拒绝访问，未伪造 Registry/CSS 快照。

