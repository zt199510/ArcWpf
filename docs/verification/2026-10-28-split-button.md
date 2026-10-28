# SplitButton 验证记录（2026-10-28）

## 实现

- 新增 `ArcWpf.Controls.SplitButton`，主动作区域与菜单区域独立处理。
- 右侧菜单复用 `Menu` 的 Popup、焦点、键盘和条目事件语义。
- Gallery 新增 SplitButton 页面，展示主保存动作和另存为/导出菜单动作。

## 验证

- 命令：`dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --no-restore --filter FullyQualifiedName~SplitButtonTests`。
- SplitButtonTests：2/2 通过。
- 未运行其他控件测试，未使用 computer-use。

## 待人工验收

主/副区域鼠标命中、Popup 对齐、Escape/焦点归还、明暗主题、Reduced/Off 和多 DPI。


## 2026-10-28 视觉边界修正

修复右侧 chevron 未继承 SplitButton 前景导致的不可见问题；MenuItem 容器取消默认焦点装饰，内层按钮不再抢焦点，Surface/ListBox/条目 Border 增加裁剪和零内边距，选中/悬停背景统一由 Popup 内共享 highlight 承担。SplitButton/Menu 专项合计 9/9 通过。


## 用户截图问题修正

根据截图确认：SplitButton chevron 可见性已改为继承 SplitButton 前景；Menu Popup 的 ListBox 焦点装饰已关闭，ListBoxItem 和内层按钮不再绘制默认焦点框；条目背景固定透明，选中/悬停只由内缩共享 highlight 绘制，Surface/ListBox/条目容器增加裁剪。专项回归 9/9 通过。



## 录屏对照补充

根据用户录屏和 Arc ButtonGroup CSS 基线，补充了 Menu/SplitButton chevron 的打开 180°旋转；默认 Menu 模板和 SplitButton 自定义 MenuTemplate 均使用同一状态路径。录屏文件已抽取检查，但 WPF MediaPlayer 对屏幕录制输出黑帧，未把抽取帧当作视觉证据；后续以用户人工窗口验收为准。



## 2026-10-28 MenuItem 选择状态修正

根据用户截图确认，之前蓝色整行来自 WPF ListBox 系统 Selection 模板，灰色条目来自独立 hover，两个状态叠加造成 Arc 视觉偏差。现已启用自定义 ListBoxItem 模板、固定透明背景、取消系统焦点装饰，并在鼠标移动时同步 SelectedIndex，使 hover/keyboard/selection 共用 PART_Highlight。相关专项 19/19 通过。



## 2026-10-28 系统选中背景隔离

专项测量发现 Popup 条目的容器会在 WPF ListBox 生成/选择时重新获得系统默认选中绘制。Menu 现在在容器生成、Popup 打开和 SelectionChanged 路径显式归一化容器：透明 Background、无 FocusVisual、OverridesDefaultStyle，并让鼠标移动同步 SelectedIndex。相关专项 19/19 通过。

## 2026-10-28 用户验收

用户在 Windows 上测试 `4ef5dc9` 无问题，上方“待人工验收”中的命中、Popup 对齐、焦点与主题视为通过；多 DPI 未单独确认。详见 [Menu 弹出层修正](2026-10-28-menu-hosted-popup.md)。
