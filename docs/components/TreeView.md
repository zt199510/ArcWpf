# TreeView 组件规格

## 身份与状态

- 组件名：`TreeView`
- 用途：浏览文件夹、分类和其他嵌套层级。
- 所属里程碑：M3 导航与展示组。
- 状态：**来源已固定，待实现**。
- 来源：2026-10-26 项目采集批次，见 [参考卡](../references/components/TreeView.md) 和 [冻结清单](../../tests/Baselines/References/2026-10-26-tree-view/manifest.json)。
- 许可：来源页面标记 Free；许可页说明 Free 组件为 MIT。仅作行为/视觉参考，不直接复制上游代码或依赖。

## 范围与依据

本次必做：嵌套节点、展开/收起、受控/非受控展开集合、单选、禁用节点、可见行键盘导航、Home/End、深度缩进、当前行高亮、主题和 Reduced/Off 动效。

明确暂缓：多选/复选框汇总、拖拽排序、懒加载、虚拟化、搜索过滤、右键菜单、编辑重命名、异步错误状态、RTL 专用分支和树表格列。

## 状态与交互

| 状态/触发 | 外观 | 操作结果 | 焦点/键盘 | 数据与事件 |
|---|---|---|---|---|
| 文件节点 | File 图标、无 Chevron | 点击/Enter/Space 选中 | 可见行 roving focus | `SelectedNode`/`Selected` |
| 关闭文件夹 | Folder 图标、Chevron 0° | 点击/Enter/Space 展开 | ArrowRight 展开 | `ExpandedIds` 增加节点 id |
| 展开文件夹 | FolderOpen 图标、Chevron 90° | 点击/Enter/Space 收起 | ArrowLeft 收起 | `ExpandedIds` 移除节点 id |
| 当前选中 | SurfaceMuted selection overlay，文字 Foreground | 保持单选 | 选中行保留焦点 | `SelectedNode` 更新 |
| 禁用节点 | 降低不透明度 | 不可选择或展开 | 跳过激活，仍按路径显示 | `IsEnabled=false` |
| ArrowDown/Up | 不改变展开状态 | 移到下/上一可见行 | roving tabindex 只保留一个0 | 焦点行更新 |
| ArrowLeft/Right | 按文件夹状态切换 | 关闭/打开或移到父/首子 | 不触发选择，除非随后激活 | 展开事件按实际状态触发 |
| Home/End | 不改变树状态 | 跳到首/末可见行 | 焦点转移到对应行 | 不触发选择 |
| Reduced/Off | 无高度/位移/旋转动画 | 状态和事件不变 | 键盘不变 | 清理旧动画时钟 |

节点 id 必须在整棵树中唯一。可见行按深度优先顺序展平；每行包含 depth、position、setSize 等 Automation 所需信息。

## 参考依据与差异

来源冻结状态：**已固定**。文件、SHA-256 和获取日期见冻结清单。

| 结论或参数 | 问题类型/权威来源 | 精确引用 | 证据等级 | 验证状态 |
|---|---|---|---|---|
| 节点是 id/label/children/icon，根节点数组组成树 | API | Markdown API、Registry `TreeNode` | 文档确认 + 源码确认 | 已固定 |
| 受控/非受控展开 | 状态/API | Markdown `defaultExpandedIds`/`expandedIds`；TSX `internalExpanded` | 文档确认 + 源码确认 | 已固定 |
| 可见行展平，关闭分支不渲染 | 性能/布局 | Markdown Performance；TSX `flatten` | 文档确认 + 源码确认 | 已固定 |
| Arrow、Home/End、Enter/Space | 键盘 | Markdown Keyboard；TSX `onKeyDown` | 文档确认 + 源码确认 | WPF 待实现 |
| 每层18px、行36px、标签省略号 | 视觉/响应式 | Markdown Responsive、CSS | 文档确认 + 源码确认 | WPF DIP 待测 |
| tree/treeitem、roving tabindex、层级信息 | 无障碍 | Markdown Accessibility、TSX attributes | 文档确认 + 源码确认 | WPF Automation 待实现 |

### 冲突或有意差异

| 冲突或有意差异 | 备选方案 | 采用方案与理由 | 影响范围 | 验证方式与状态 |
|---|---|---|---|---|
| React `expandedIds` 数组 | `ObservableCollection`、DP 集合 | 使用 `IReadOnlyList<string>`/`ObservableCollection<string>` 设计稿，避免公开可变共享默认值 | API/绑定 | 实现前固定具体类型 |
| React `onSelect` 回调 | ICommand、路由事件、两者 | `SelectedNode` 只读状态 + `Selected` 路由事件；可选 `Command` 后续评估 | API | 设计选择，待实现 |
| ARIA treeitem 属性 | 自定义 AutomationPeer | 使用 WPF UIA ExpandCollapse/Selection/Name 语义，不伪造 ARIA | 无障碍 | Automation 测试待补 |
| CSS 颜色混合文件图标 | 纯语义令牌 | 文件类型颜色保留为有限、可覆盖的资源；不复制 CSS `color-mix` | 视觉 | 明暗主题窗口核对 |

## WPF 公开契约（设计稿）

建议 `TreeView : Control`，内部维护可见行展平和 roving focus；不直接继承 WPF `TreeView`，避免其默认层级容器与 Arc 的共享选择/动效模板冲突。

`TreeNode`：

- `Id`：必填、整棵树唯一。
- `Label`：显示文本。
- `Children`：只读节点集合；有子项即文件夹。
- `Icon`：可选对象，默认按文件/文件夹几何显示。
- `IsEnabled`：默认 true。

`TreeView`：

- `Nodes`：根节点集合。
- `DefaultExpandedIds`：首次加载的非受控展开集合。
- `ExpandedIds`：受控展开集合；与 `ExpandedChanged` 同步。
- `SelectedNode`：只读当前节点。
- `Label`：默认 `"File tree"`，用于 Automation 名称。
- `Selected`、`ExpandedChanged`：路由事件。

必要模板部件：`PART_RowsHost`、`PART_Selection`（可选）、`PART_ScrollViewer`（可选）。缺少可选部件不得阻断节点数据和键盘行为。

## 视觉与动效

- 树外壳：Surface、Border、1 DIP 边框、Panel 圆角；不得使用黑色默认 Focus Adorner。
- 行高36 DIP，行间2 DIP，内层缩进18 DIP；文字单行省略号。
- Chevron、图标、分支线放独立布局单元，垂直居中；分支线和图标不参与命中测试。
- Full：打开空间增长、子项按 item stagger 进入；selection 在行间 spring；Chevron snappy 旋转。
- Reduced：行和图标立即到位，保留展开/选择状态；Off 停止旧时钟。
- 模板替换、卸载、快速展开反向必须清理旧动画和事件订阅。

## 验收场景

1. 创建 `app` 文件夹及两个文件、根级 `README.md`，默认展开 app，确认深度、图标和可见顺序。
2. 点击/Enter/Space 文件夹，确认展开/收起、`ExpandedChanged` 和子项可见性。
3. 使用 ArrowDown/Up、Home/End、ArrowLeft/Right，确认只在可见行移动并正确进入父/首子。
4. 选择文件和文件夹，确认单选高亮、`SelectedNode` 和 `Selected` 事件。
5. 禁用节点，确认不响应激活但保持层级显示；关闭分支的子项不进入 Tab/箭头顺序。
6. 受控 `ExpandedIds` 与绑定更新往返；快速反向展开确认无旧动画完成回调。
7. 在 100%/150%/200% DPI、窄宽度和深层树下测量行高、缩进、文字省略号、分支线和无黑色焦点框。
8. Automation 检查树名称、层级、展开/选择状态和 roving focus；切换明暗主题与 Reduced/Off。

## 完整度矩阵（开发前）

| 交付项 | 本次范围 | 实现状态 | 验证证据 | 剩余差异/下一步 |
|---|---|---|---|---|
| API与绑定 | 必做 | 尚未实现 | 来源 API 已固定 | 固定 WPF 集合/只读状态类型 |
| 控件外形与内部对齐 | 必做 | 尚未实现 | CSS 已固定 | WPF 行/缩进/图标模板 |
| 内容组合与状态 | 必做 | 尚未实现 | TSX flatten/keyboard 已固定 | 展开、选择、禁用 |
| 动效与打断清理 | 必做 | 尚未实现 | motion tokens/TSX 已固定 | OwnedMotion 生命周期 |
| 输入/自动化/主题 | 必做 | 尚未实现 | Markdown Keyboard/Accessibility | AutomationPeer |
| 原版主示例与扩展示例 | 必做 | 尚未实现 | 页面 Usage 已固定 | Gallery 文件树 + 状态扩展 |
