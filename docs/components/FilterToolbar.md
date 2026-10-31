# FilterToolbar 组件规格

状态：已按 2026-10-31 冻结源码重做，构建通过；专项测试与实际窗口 **未运行/待验证**（Linux 环境）。事实见 [参考卡](../references/components/FilterToolbar.md)。

## API

`FilterToolbar : ContentControl`：`Filters`（`ObservableCollection<FilterChip>`，控件在移除/清除/添加时直接编辑）、`Fields`（`FilterField`，有字段时显示 Add filter）、`Label`（`Active filters`）、`AddFilterLabel`（`Add filter`）、`AddFilterAlignment`（`FilterMenuAlignment.End`）、Content（附加操作，位于 Add filter 与 Clear all 之间）；事件 `FilterRemoved`、`FilterAdded`（`FilterChipEventArgs`）、`FiltersCleared`。`FilterMenu : Control` 可单独使用：`Fields`、`Active`、`Label`、`Alignment`、`FilterSelected`、`IsOpen`。模型 `FilterChip`、`FilterField`、`FilterOption`（支持字符串隐式转换）。旧版 Query/Placeholder/ClearLabel/HasActiveFilters/QueryChanged 与 `FilterToolbarFilter` 已移除（原版无搜索框）。

WPF 选择：原版 onAdd 由调用方合并；本控件内置“同 id 替换、新字段按 Fields 顺序插入”（即源站 demo 的逻辑）。

## 范围矩阵

| 项 | 状态 |
|---|---|
| 工具栏外形、胶囊、移除按钮、empty 提示、Clear all | 已实现 |
| 槽位宽度弹簧、胶囊入/出场、换行滑回、frame 高度跟随、值 MorphText | 已实现；槽位裁剪为矩形（原版 pill 圆角裁剪） |
| FilterMenu 按钮→面板形变、face/内容进度显现、两步、back 按钮、标题滚动、高度跟随 | 已实现；用 Popup 承载（原版就地渲染），圆角改为与形变同步的弹簧 |
| 菜单键盘（上下/Home/End/左右/typeahead/Escape）、高亮指针滑动/键盘跳变 | 已实现 |
| 对齐/翻转/向上；外部按下、失焦、窗口失活关闭；焦点归还 | 已实现；可用空间按窗口内容区计算（原版另考虑裁剪祖先） |
| 状态播报（LiveRegion） | 已实现 |
| ≤520px 纵向布局 | 暂缓 |
| Gallery 主示例 Project filters | 已实现；头像以首字母代替照片，宽度 720 待核对 |
| 实际窗口、明暗主题、Reduced/Off、读屏、多 DPI | 未运行/待验证 |
