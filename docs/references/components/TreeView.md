# TreeView 参考基准

[原页](https://uiarc.dev/components/tree-view)、[Markdown 合约](https://uiarc.dev/components/tree-view/markdown)、[Registry](https://uiarc.dev/r/tree-view.json)、[许可证](https://uiarc.dev/license)、[冻结清单](../../../tests/Baselines/References/2026-10-26-tree-view/manifest.json)。

`2026-10-26-tree-view` 是项目采集批次，不是上游发行版本。快照包含页面、Markdown、Registry JSON、TSX、CSS、共享动效令牌和许可页。来源页面标记该组件为 Free；许可页说明 Free 组件采用 MIT。本项目只参考行为与视觉，不复制 React、Motion、lucide 或源代码。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | WPF 待核对 |
|---|---|---|---|
| 文案、内容组合 | `app` 文件夹展开，包含 `page.tsx`、`layout.tsx`；根级 `README.md` | Markdown Usage | Gallery 使用同源文件树 |
| 行几何 | 行高36px，行间保留2px；每层缩进18px | CSS `.row`、`.item`、Markdown Responsive | WPF 以 DIP 映射，实际窗口待测 |
| 外壳 | 透明/表面背景、1px border、16px 圆角、6/8px 内边距 | CSS `.tree` | 使用 ArcWpf Surface/Border 令牌 |
| 选择 | 共享浅色 selection，当前文字 foreground | CSS `.selection`、`.item[aria-selected]` | WPF 共享 Border overlay |
| 图标 | Chevron、Folder/FolderOpen、File；图标和 branch aria-hidden | TSX、CSS | 使用 WPF Path/Geometry |
| 窄窗口 | 行固定高度，标签省略号；深层路径减少可用标签宽度 | Markdown Responsive、CSS `.label` | WPF TextTrimming |

## 状态与动效索引

- 只有可见行参与键盘顺序；关闭文件夹不生成子项。
- ArrowRight 打开文件夹或进入已打开文件夹的第一个子项；ArrowLeft 关闭或返回父级。
- Enter/Space 选择当前项，并对文件夹切换展开状态。
- 选择背景在行之间 gliding；Chevron snappy 旋转；打开时子项 stagger 进入。
- Reduced motion 直接显示/隐藏，保留相同的树状态和键盘语义。

## WPF 差异和回归入口

- WPF 使用 `TreeNode` 对象图、`ObservableCollection`/只读节点集合、`ExpandedIds` 或内部展开集合、`SelectedNode` 和路由事件；不复制 React props 或 ARIA 属性。
- 使用 `ItemsControl`/自定义可见行展平，而不是让每个文件夹都创建未展开的可视子树，保持关闭分支不渲染。
- 自定义 `AutomationPeer` 映射 tree/treeitem 语义；实际 UIA、DPI 和动效尚待窗口验收。
