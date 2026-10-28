# ContextMenu 验证记录（2026-10-28）

## 实现

- 新增 `ArcWpf.Controls.ContextMenu`，支持宿主附加、鼠标位置/宿主相对打开、键盘导航、外部点击关闭和焦点归还。
- 复用 MenuItem、MenuTheme 的条目模板、共享 highlight、危险状态和 Popup Surface。
- Gallery 新增 ContextMenu 页面。

## 验证

- 命令：`dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --no-restore --filter FullyQualifiedName~ContextMenuTests`。
- ContextMenuTests：3/3 通过。
- 未运行其他控件测试，未使用 computer-use。

## 待人工验收

实际右键命中、Shift+F10 定位、Popup 边缘翻转、外部点击、读屏、明暗主题和多 DPI。


## Arc 样式补齐

根据官方 Markdown 补齐 checked 勾号、最大高度 300 DIP 内部滚动、ContextMenu 键、滚动/resize 关闭和 Popup 缩放淡入。ContextMenuTests 将覆盖这些状态，实际窗口仍待人工验收。


## Popup 底部灰条修正

截图中的底部灰条来自 ContextMenu 将共享 highlight 用 Margin 定位，Margin 参与 Grid 测量并撑大了 Surface。现已改为 TranslateTransform 定位，与 Menu 的高亮实现一致，不再改变 Popup 外框尺寸。ContextMenuTests 3/3 通过。

