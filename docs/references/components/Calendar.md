# Calendar 参考卡（2026-10-26）

来源：[Arc Calendar](https://uiarc.dev/components/calendar)、[Registry](https://uiarc.dev/r/calendar.json)。冻结目录：`tests/Baselines/References/2026-10-26-calendar`，包含原始 Registry、展开 TSX/CSS、Markdown、motion 依赖和许可页；逐文件 SHA-256 见 manifest-hashes.json。无上游版本号，以采集批次和哈希标识。免费组件适用 Arc MIT 声明，依赖 React、Motion、Lucide；产品不引入这些运行依赖。

证据：以下为源码/CSS确认，未进行源站运行观察。主用例 DeliveryDate 初始未选、minDate=当天、禁用周日、showToday。独立组件最大宽328，透明无外框；标题行最小34高、下距14；导航32×32、间隔2；星期24高、下距6；七列六周，间距4，日期为圆形；选中 accent/反色字，今日4像素小点，月外日期弱化，禁用降低透明度。小于260宽时源站标题与导航分两行。

WPF映射：Calendar/CalendarItem/CalendarDayButton/CalendarButton 保留原生日期与模式；共享样式 ArcWpf.Calendar.Style 可应用于系统 Calendar。accent 映射现有 ControlOn/ControlGlyph；其它采用同名语义 brush。日期格四边各2 DIP，星期行30 DIP；圆形用 Ellipse，避免超大圆角。独立宽328，DatePicker弹层宽320、内边距16、圆角Panel。WPF标题按文化格式、保留年/十年浏览，窄宽度标题省略（不冒充CSS响应式一致）。

动效目标：源码月份条带spring visualDuration .36、选中圆盘移动、标题交叉淡入。用户追加选中动画后，已实施SelectionDisc的共享圆盘：移动morph(.42,bounce0)、进入缩放snappy(.26,bounce0)、进入opacity .16、退出opacity/scale .14。采用现有ControlSpring临界阻尼解析解（omega=3.89/visualDuration），是WPF近似而非Motion内部求解器；保留中途位置与速度。文字反色按圆盘实际覆盖切换，区别于原版按距离计算的延迟。月份条带与标题动画仍暂缓。字体沿用项目 Windows 回退。原站截图/DPI未知；WPF回归结果另记。
