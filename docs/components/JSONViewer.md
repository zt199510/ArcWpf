# JSONViewer 组件规格

状态：已按 Arc 线上页面重做外观和交互（见 [像素对照记录](../verification/2026-11-02-data-controls-pixel.md)）。离屏渲染与 Arc 截图几何对齐；真实窗口滚动、焦点、DPI 和逐帧视觉未运行。

公开 API：`Data`、`RootName`（默认 root）、`DefaultExpandDepth`（默认 1）、`ExpandedIds` 双向集合、`Searchable`、`Query` 双向文本、`PageSize`（默认 50）、`Copyable`、`ShowPath`、`MaxHeight`（默认 420）、`Label`。提供 `CopyValue()`、`CopyPath()`、`ExpandAll()`、`CollapseAll()`。来源的 `onCopy`/`onSelect` 回调尚未映射为事件；当前行通过只读 `SelectedPath` 读取。

模板部件：`PART_Toolbar`、`PART_SearchBox`、`PART_Search`、`PART_Matches`、`PART_ResultCount`、`PART_Previous`、`PART_Next`、`PART_ExpandAll`、`PART_CollapseAll`、`PART_Tree`（复用 TreeView，配专用样式）、`PART_PathBar`、`PART_Path`、`PART_PathType`、`PART_CopyPath`。`TreeNode.Id` 是 JSONPath，`Label` 是读屏描述，`TreeNode.Data` 携带行绘制数据，由 `JsonRowLine` 排版；复制值/路径是每行的 `JsonActionButton`。搜索只影响可见行与展开集合，不重写 Data。滚动条悬停才显示且不占布局。

键盘和状态遵循来源 Tree pattern；未知值类型回退 `other`。对象/数组子项按 `PageSize` 生成，末尾的 More 节点展开下一页。Reduced/Off 或可见行超过 400 时不做行展开动效。主题使用语义前景、边框、Surface 和类型色；Reduced/Off 停止展开/高亮动效。

验收场景：嵌套 object/array、默认展开深度、搜索命中 key/value 并自动展开祖先、大小写不敏感、分页、复制 primitive/branch/path、TreeView 方向键/Home/End、清除搜索、空/null 数据、明暗主题、换模板和卸载清理。
