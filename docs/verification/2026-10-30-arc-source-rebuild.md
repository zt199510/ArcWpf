# ExpandingSearch / DateRangePicker / TimePicker / ScrollArea 源码复刻验证（2026-10-30）

## 来源

此前（c409412）Registry 被拒，四控件为无源实现。本次 Registry 可访问，已冻结至 `tests/Baselines/References/2026-10-30-{expanding-search,date-range-picker,time-picker,scroll-area}/`，manifest 记录 SHA-256；源站 demo 数据从页面 chunk 读取（本地副本在 `work/live/`，未版本化）。

## 实际结果

- `dotnet build src/ArcWpf.Controls -p:EnableWindowsTargeting=true`：0 错误。
- `dotnet build samples/ArcWpf.Gallery -p:EnableWindowsTargeting=true`：0 错误。
- `dotnet build tests/ArcWpf.Tests -p:EnableWindowsTargeting=true`：0 错误。

## 未运行/待验证

- 专项测试 ExpandingSearchTests(5)、DateRangePickerTests(4)、TimePickerTests(4)、ScrollAreaTests(3)：Linux 无法运行 WPF，需在 Windows 运行。
- 实际窗口：几何比例、动效节奏、明暗主题、Reduced/Off、多 DPI、IME、读屏均未观察；不得视为视觉通过。

## 已知差异

见各规格范围矩阵：DateRangePicker 共享布局飞行改交叉；ExpandingSearch 行位置弹簧未做；TimePicker 外部点击关闭无退场动画；ScrollArea 无 scroll-snap；Gallery 头像/照片为占位。
