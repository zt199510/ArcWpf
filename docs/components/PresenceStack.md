# PresenceStack

2026-10-27：用户选择依据公开文档独立实现 WPF；不使用 Arc Pro 源码。参考卡见 ../references/components/PresenceStack.md。

## 实施契约

基类原生 Button；People 为 IEnumerable<PresencePerson>，默认独立 ObservableCollection。PresencePerson 使用非空稳定 Id（构造参数，只读）、Name、Source、Status（Active/Idle/Typing）、Detail，支持属性通知。重复 Id 采用第一项；集合修改须在 UI 线程。

Max 默认4，负数强制为0；Size Small/Medium/Large = 28/36/44；Align Start/End；SelfId；Label 默认 People here now；Heading 默认 Here now；IsOpen 双向默认；Announce 默认 true；IsPersonSelectionEnabled 默认 false，启用后触发 PersonSelected 路由事件并关闭。RingBrush 可覆盖背板颜色。Count、OverflowCount 只读。

|范围|实现目标|
|---|---|
|布局|固定预留 Max 个头像及计数位；堆叠仅 transform 位移；End 向左展开；空集合仍可打开空名单|
|状态|active绿点、idle灰点/淡头像、typing胶囊三点；本人提示|
|名单|288宽、有限滚动高度、全部成员姓名/状态/detail；默认非按钮行，显式启用选择|
|输入|根单按钮；hover/focus展开；左右键切换共享提示；Enter/Space原生Click；Escape/外部点击复用Popover|
|无障碍|Button + ExpandCollapse；脸和标签装饰；礼貌播报仅成员加入/离开；名单内容可读|
|动效|位置spring、进入缩放、160ms退出、计数方向滚动、共享标签移动及宽度、状态胶囊、typing循环、名单行高度、标题及detail滚动|
|生命周期|Full动态；Reduced/Off立即；隐藏/卸载停止循环并解除订阅；重新加载重建当前状态|
|Gallery|公开行为主示例与独立尺寸/对齐/空集合/动态变化测试区|

## 独立设计参数

公开文档数值：28/36/44、最大4、名单288、退出160ms、进入scale .4、hover上移2。

2026-10-27用户反馈后，公开HTML确认Medium静止步长24、预留宽132、前头像遮后头像。撤销上一版“遮盖68%所以步长.32D”的误读。Small/Large步长采用floor(.68D)推导（19/29），未声称源码已确认。展开步长D+4仍为WPF选择。

公开CSS确认名单padding6，头像32，行padding8/10，姓名+单行说明，标题与人数左右分列，滚动上限320；共享提示在D+8下方，高26/圆角13；状态点8/10/12，typing内容宽2.1dot、高dot+2，外加2px环，深底浅色点，周期1.1秒/错开.14秒。位置/标签0.32秒阻尼、数字0.18秒仍属独立WPF实现，不声称原版spring一致。

## 验收

只运行 PresenceStackTests，编译解决方案；程序化渲染与时钟证据不能代替实际窗口。按用户要求不操作桌面；真实鼠标、焦点/外部关闭、读屏、DPI和主题视觉由用户人工验收。

## 动效实现映射

|触发|内部属性与时序|打断与终态|
|---|---|---|
|加入|scale .4→1.06→1，320ms；位置320ms采样阻尼spring|同id返回复用；从当前值重定向，不保留速度|
|离开|scale→.4、opacity→0，160ms；其余头像立即开始补位|保留退出视觉但取消命中；完成后Dispatcher移除；返回取消清理|
|hover/键盘|stride floor(.68D)→D+4，320ms；当前头像Y→-2，200ms|宽度始终D+Max×floor(.68D)；End最后一槽锚定右边|
|共享姓名|X/Width 320ms；opacity150ms；首次宽度立即设置|单一Border，无逐头像ToolTip；截断/重入从当前值重定向|
|溢出/标题/detail|旧文本离场、新文本入场180ms；数字增向上、减向下|旧文本无UIA peer；中断以最新文本为准|
|状态|头像opacity、状态颜色200ms；状态宽/高200ms|内部可变brush颜色插值；主题重新取令牌|
|typing|三点Y 0→-2→0，1100ms循环，错开140ms|仅Full且可见运行；隐藏/卸载/Reduced/Off停止|
|名单|Popover自身scale/fade；行Height0↔56，进入240ms/退出160ms|名单内部有限滚动；静态行不是按钮，选择模式才创建Button命中路径|

Reduced/Off 对这些变化全部直接置终态；内部Popover在Reduced下映射Off。未实现系统设置自动映射，不宣称系统自动减弱。所有动画只写内部视觉属性，公开DP绑定保留。

## WPF 适配与当前状态

已实现上表、公开API和Gallery。通知使用UIA Notification/MostRecent近似礼貌播报；实际读屏时序待人工测试。名单使用有名称的Pane并标注dialog类型，未声称提供独立WindowPattern。Arrow方向随FlowDirection；尺寸/对齐为逻辑方向。非法重复Id保留第一项。未做网络presence适配器。

实现与专项验证记录见 [验证记录](../verification/2026-10-27-presence-stack.md)。真实窗口视觉、鼠标、Popup焦点和多DPI未验收。

主示例恢复公开页面文档卡结构与初始6人/+2，提供加入/离开/改状态和暂停。原图许可未知，使用本地原创头像及首字母替代；自动演示3.5秒为WPF选择。实际窗口效果待用户复验。
