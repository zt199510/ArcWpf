# JSONViewer 组件规格

状态：Arc Registry 已冻结；JSONViewer 开始实现，目标覆盖 JSON 树展开/折叠、复制值/路径、搜索、分页、TreeView 键盘和明暗主题。真实窗口滚动、焦点、DPI 和逐帧视觉待验收。

公开 API：`Data`、`RootName`（默认 root）、`DefaultExpandDepth`（默认 1）、`ExpandedIds` 双向集合、`Searchable`、`Query` 双向文本、`PageSize`（默认 50）、`Copyable`、`ShowPath`、`MaxHeight`（默认 420）、`Label`。提供 `CopyValue()`、`CopyPath()`、`ExpandAll()`、`CollapseAll()` 以及 `Copied`/`Selected` 事件。

模板部件：`PART_Search`、`PART_Tree`（复用 TreeView）、`PART_Path`、`PART_CopyValue`、`PART_CopyPath`、`PART_ResultCount`。`TreeView` 的每个 `TreeNode.Id` 是 JSONPath，Label 包含键、类型/数量和 primitive 值；搜索只影响可见行与展开集合，不重写 Data。

键盘和状态遵循来源 Tree pattern；未知值类型回退 `other`。对象/数组子项按 `PageSize` 生成，末尾的 More 节点展开下一页。主题使用语义前景、边框、Surface 和类型色；Reduced/Off 停止展开/高亮动效。

验收场景：嵌套 object/array、默认展开深度、搜索命中 key/value 并自动展开祖先、大小写不敏感、分页、复制 primitive/branch/path、TreeView 方向键/Home/End、清除搜索、空/null 数据、明暗主题、换模板和卸载清理。
