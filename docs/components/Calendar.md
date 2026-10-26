# Calendar 组件规格

2026-10-26 用户确认：“Calendar 已经完成 可以开始 Search了”。记录为当前版本用户通过反馈；历史未测专项仍保留，月份动效不据此宣称已实现。

来源与几何见[参考卡](../references/components/Calendar.md)。本次目标：独立原生 Calendar，并修复 DatePicker 内部仍使用系统日历外观的问题。

标题按钮范围修正：月份标题保留WPF点击切换年视图，按钮背景/命中范围为文字测量宽度加左右各12 DIP内边距，高32 DIP；左侧星号列仅提供可用空间，按钮左对齐，不铺满该列。与右侧操作区保留12 DIP间隔，窄宽度仍省略。用户反馈无内边距版本范围过小，因此增加留白。属于WPF标题交互适配（Arc原版标题不是该原生模式切换按钮）。

|交付项|本次范围|实现状态|证据/剩余差异|
|---|---|---|---|
|Calendar 继承系统 Calendar，共享原生目标样式|本次必做|已实现|不替换 DatePicker 私有 Calendar；实例关联测试通过|
|SelectedDate/DisplayDate/边界/BlackoutDates/文化/键盘/自动化|本次必做|已实现，部分待窗口验收|原生年/十年导航测试通过；多选API保留但不作为Arc单选还原|
|标题、导航、六周圆形日期、今日点、选中/月外/禁用/焦点|本次必做|已实现，待视觉验收|328/286/240布局测试通过，Popup 320；键盘焦点人工未测|
|ShowToday、SelectTodayCommand|本次必做|已实现|ShowToday附加DP bool默认false、继承到模板，可用于系统Calendar；命令绑定/范围/BlackoutDates测试通过|
|DatePicker共享样式、Today头部、Clear分隔底栏|本次必做|已实现|原生编辑提交、Popup程序化测试通过|
|主题、Gallery、专项测试|本次必做|已实现|明暗离屏渲染；真实窗口专项未验收|
|选中圆盘滑动、进入/清除|本次必做（用户追加）|已实现，实际观感待验收|共享圆盘；morph .42s、snappy .26s、进入透明度.16s、退出.14s|
|月份spring、跨午夜刷新|明确暂缓|未实现|月份静态切换，继承WPF今日计算|
|CSS容器查询、JS函数禁用日期|明确暂缓|未实现|用WPF布局和日期范围集合|

选中动效的触发、参数、重入、属性归属、Reduced/Off与清理合同及测试证据见[选中动画记录](../verification/2026-10-26-calendar-motion.md)。CalendarSelectionIndicator仅为模板视觉，不接管原生选择；月份/标题过渡和按压缩放不在本次已完成声明中。

公共类型 `ArcWpf.Controls.Calendar`。继承原生日期属性/事件/SelectionMode，不重新声明或接管 DataContext。`ShowToday` 是附加DP并提供实例包装，普通单向默认绑定；`SelectTodayCommand` 为 RoutedUICommand，目标为原生 Calendar，单选/非禁用且今日可选时可执行，使用 SetCurrentValue 更新 SelectedDate、DisplayDate、DisplayMode。DatePicker使用同一命令并在成功后关闭浮层。

用法：`<arc:Calendar Width="328" ShowToday="True" SelectedDate="{Binding Date, Mode=TwoWay}" />`。系统 Calendar 可显式合并 `Calendar/Themes/CalendarTheme.xaml`，设置 `Style="{StaticResource ArcWpf.Calendar.Style}"` 与 `arc:Calendar.ShowToday="True"`。DatePicker默认CalendarStyle已设置，可继续由使用方覆盖。

实现结果见[验证记录](../verification/2026-10-26-calendar.md)。WPF额外在Blackout日期上显示淡斜线以提供非颜色提示。Today新增按钮在原生CalendarAutomationPeer的子项枚举中未出现，仍需补充辅助技术适配和读屏验收；原有日期Grid/Selection语义保留。

模板必要部件遵循WPF：Calendar.PART_CalendarItem；CalendarItem.PART_HeaderButton/PART_PreviousButton/PART_NextButton/PART_MonthView(7×7)/PART_YearView(4×3)。保留 CalendarItem 创建的日期按钮与日期事件。主题资源键 `ArcWpf.Calendar.Style`、`ArcWpf.Calendar.NavigationButtonStyle`；不全局替换所有系统Calendar。

验收：实际布局有42个日期格，328/286/240宽不裁剪、圆形等宽高；导航改变DisplayDate，年/十年可用；BlackoutDates拒绝选择；Today保留绑定并尊重限制；DatePicker仍使用其BlackoutDates所属原生实例，Popup打开/关闭和文化输入通过；明暗主题资源更新。实际鼠标/键盘/焦点/IME、多DPI与读屏未运行时必须标待验收。
