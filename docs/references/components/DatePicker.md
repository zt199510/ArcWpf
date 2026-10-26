# Arc DatePicker 参考基准

## 来源与固定范围

- 页面、源码、许可：https://uiarc.dev/components/date-picker；Markdown：https://uiarc.dev/components/date-picker/markdown；Registry：https://uiarc.dev/r/date-picker.json。
- 批次/日期：项目采集批次 `2026-10-26-date-picker`，2026-10-26；`page.html` SHA-256 `59E730E22F0C94914AE9BBEA4D1F7722320E6B1F630F33C9D3EA5B18C326C7B9`，`date-picker.md` SHA-256 `EBFC00C873C54A5247DA5A1BFB58D521D9C612361701D9724D3F5A181344748F`，`date-picker.json` SHA-256 `AAFF3C9E6CCE6E4451CF4EB4C30A303C8F22AB2F786FCBA7FB5B290D435B0FB0`。
- 快照：`tests/Baselines/References/2026-10-26-date-picker/`；当前实现参考 Arc 页面，非直接复制 React/Motion/Lucide 代码。
- 许可：页面标注 free/open source；本项目只作行为和视觉参考，不引入上游代码、motion 或图标包。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 文案、内容组合 | `Start date`，日期字段按钮、日历弹出层、Clear、可选 Today | Markdown Usage/API | Gallery 使用等价中文文案；已实现 |
| 初始值与状态 | 未选日期；可传 value；打开显示当前值或今天所在月 | Registry `date-picker.tsx` | 原生 SelectedDate/DisplayDate；已实现 |
| 控件本体尺寸/圆角 | 中号控制高度、圆角 control token；popover 约 320px | Registry CSS | 44 DIP 外壳、18 DIP 圆角、320 DIP 上限；WPF 适配 |
| 字体、颜色、边框 | surface/foreground/border 语义令牌 | Registry CSS | DynamicResource ArcWpf 令牌；已实现 |
| 内部对齐锚点 | 左侧日历图标、值、右侧 chevron | Registry JSX/CSS | WPF Button + DatePickerTextBox + 日历按钮；已实现 |
| 外部间距 | label 与字段、description 与字段分隔 | Registry CSS | 8 DIP / 8 DIP；已实现 |
| 展示容器宽度/背景 | popover min(320px, viewport-32px)，窄屏贴合字段 | Registry CSS | 320 DIP 上限；真实窗口宽度/DPI 待验收 |

## 状态与动效索引

Arc 来源要求打开/关闭、选中、Clear、Today、上下限和禁用日期；键盘 Enter/Space/ArrowDown 打开，Escape 关闭并归还焦点，日历方向键/Home/End/PageUp/PageDown 导航。来源描述日期部分滚动和弹出层 spring；WPF 首版保留原生 Popup/Calendar 交互，暂缓滚动分段与 spring，关闭/选中不改变绑定值。

## WPF差异和回归入口

- WPF 原生 DatePicker 负责文化格式、TextBox 编辑、SelectedDateChanged、Validation、Calendar、BlackoutDates、键盘与自动化。
- Arc 的 `disabledDates` 映射为 WPF `BlackoutDates`，使用方可按日期加入范围；不复制函数型 JS API。
- Arc 的 per-part rolling、Motion spring、响应式 CSS 和 Lucide 图标不在首期；用原生 Popup 和轻量模板保留意图。
- 实际窗口交互、焦点、中文输入、DPI、读屏和视觉仍待 Windows 窗口验收。
