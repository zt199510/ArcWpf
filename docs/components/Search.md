# Search 组件规格

来源见[参考卡](../references/components/Search.md)。继承Arc Input（WPF TextBox），保留Text、选区、撤销、粘贴、IME、验证和自动化。使用`Text="{Binding Query, UpdateSourceTrigger=PropertyChanged}"`进行实时过滤，控件不接管DataContext、不访问网络、不自带过滤器。

|交付项|本次范围|状态|差异/证据|
|---|---|---|---|
|原生编辑、Label/Placeholder/Description/ErrorMessage继承|必做|已实现|原生Text默认提交语义不变；绑定/撤销测试通过|
|搜索图标、固定清除槽、明暗/聚焦/填充/禁用/只读|必做|已实现|布局测量；真实窗口浅色默认/输入态已观察|
|ClearCommand、Escape、清空后焦点与撤销|必做|已实现|程序化合成保护；实际鼠标清除和焦点回归通过|
|清除按钮进入/退出/按压Full/Reduced/Off|必做|已实现|OwnedMotion WPF近似；打断/退场测试通过，实际动态观感待验收|
|Gallery主示例、实时过滤扩展、测试|必做|已实现|实际输入Alex过滤单条，清除恢复|
|结果弹层/远程请求/防抖/快捷键唤起|不适用|不实现|分别属于Expanding search和消费业务|

新增只读DP HasQuery(bool)，Text.Length>0（空白也可清除）；ClearCommand RoutedUICommand。标准PART_ContentHost保留，PART_Label继承Input，PART_Clear为可选Button，PART_ClearVisual为动画内部Border。移除可选部件仍可输入和Escape清空。Clear通过原生文本编辑事务清空，保留绑定与Undo。无Enter搜索命令（来源未定义）。

动画仅占用PART_ClearVisual的Opacity/RenderTransform/Effect，不改动控件公开的Text/Opacity/Transform；按当前可见值打断，退出完成写入持久0基值再移除时钟，隐藏按钮但保留24 DIP槽。卸载/隐藏/替换模板停止时钟。边框、搜索图标、清除按钮悬停前景/背景使用来源 fast 160ms、standard (.22,1,.36,1) 颜色补间。动态资源与公开 BorderBrush/Foreground 仍提供目标值，仅模板内部私有画刷副本承载动画；连续变化从当前颜色打断，完成后移除时钟。Reduced/Off 直接到颜色终态；资源探针刷新立即同步，隐藏/卸载/换模板停止。系统减少动态效果偏好未自动映射。验证状态见[记录](../verification/2026-10-26-search.md)。

布局：外层StackPanel标签/字段/说明/错误；字段Grid列18/8/*/8/24，两侧12。搜索图标和清除槽固定，文本列可缩；根不设固定宽。沿用Input已验证宿主的-2 DIP文本内置偏移补偿，本组件单独测量首字符与占位起点。焦点边框为来源focus-within，不是键盘专用焦点环；清除按钮另外使用FocusVisualStyle。

验收：清除前后宿主实际宽度不变；首字符/占位起点差<=.5 DIP；绑定清除/Undo、Escape合成保护、只读禁用、自动化名称与Clear入口、模板重用、动画退场后不闪回和Full/Reduced/Off。实际IME、鼠标/Tab、DPI/读屏待实际验证。


