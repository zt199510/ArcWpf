# TreeView 组件规格

## 身份与状态

- 组件名：`TreeView`
- 用途：浏览文件夹、分类和其他嵌套层级。
- 所属里程碑：M3 导航与展示组。
- 状态：首版**实现完成，实际窗口已通过用户手动验收**；2026-11-11 的懒加载与虚拟化增强**实现完成，待验收**，见[验证记录](../verification/2026-11-11-treeview-tabs-enhancements.md)；2026-11-12 补齐首版缺失的行动效、选中滑动、图标与 AutomationPeer，**实现完成，待验收**，见[验证记录](../verification/2026-11-12-treeview-motion-automation.md)。用户此前的通过反馈不覆盖这两次修改。
- 来源：2026-10-26 项目采集批次，见 [参考卡](../references/components/TreeView.md) 和 [冻结清单](../../tests/Baselines/References/2026-10-26-tree-view/manifest.json)。
- 许可：来源页面标记 Free；许可页说明 Free 组件为 MIT。仅作行为/视觉参考，不直接复制上游代码或依赖。

## 范围与依据

本次必做：嵌套节点、展开/收起、受控/非受控展开集合、单选、禁用节点、可见行键盘导航、Home/End、深度缩进、当前行高亮、主题和 Reduced/Off 动效。

2026-11-11 增补必做（桌面 Agent 文件树需要，Arc 源码没有对应功能，属 ArcWpf 设计）：懒加载、加载中/失败状态、虚拟化、已展开分支的子项增删自动反映。

明确暂缓：多选/复选框汇总、拖拽排序、搜索过滤、右键菜单、编辑重命名、RTL 专用分支和树表格列。

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

| 懒加载文件夹，未读取 | 与关闭文件夹相同（子项为空也显示 Chevron） | 展开时调用 `ChildrenLoader` | 与文件夹相同 | `LoadState` NotLoaded → Loading |
| 读取中 | Chevron 换成转动的弧线；Off 时弧线静止 | 再点一次收起并取消读取 | ArrowRight 不离开该行 | 取消令牌置位，`LoadState` 回到 NotLoaded，迟到的结果丢弃 |
| 读取完成 | 子项出现 | 之后收起再展开不重新读取 | 键盘焦点留在原行 | `Children` 被结果替换，`LoadState` Loaded |
| 读取失败 | 文件夹自动收起，Chevron 和文件夹图标变 Danger 色，ToolTip/HelpText 为错误信息 | 再展开一次即重试 | 同文件夹 | `LoadState` Failed、`LoadError`、`ChildrenLoadFailed`、`ExpandedChanged` |

节点 id 必须在整棵树中唯一。可见行按深度优先顺序展平；每行包含 depth、position、setSize 等 Automation 所需信息。

## 参考依据与差异

来源冻结状态：**已固定**。文件、SHA-256 和获取日期见冻结清单。

| 结论或参数 | 问题类型/权威来源 | 精确引用 | 证据等级 | 验证状态 |
|---|---|---|---|---|
| 节点是 id/label/children/icon，根节点数组组成树 | API | Markdown API、Registry `TreeNode` | 文档确认 + 源码确认 | 已固定 |
| 受控/非受控展开 | 状态/API | Markdown `defaultExpandedIds`/`expandedIds`；TSX `internalExpanded` | 文档确认 + 源码确认 | 已固定 |
| 可见行展平，关闭分支不渲染 | 性能/布局 | Markdown Performance；TSX `flatten` | 文档确认 + 源码确认 | 已固定 |
| Arrow、Home/End、Enter/Space | 键盘 | Markdown Keyboard；TSX `onKeyDown` | 文档确认 + 源码确认 | 已实现，程序化通过 |
| 每层18px、行36px、标签省略号 | 视觉/响应式 | Markdown Responsive、CSS | 文档确认 + 源码确认 | 离屏测量通过（96 DPI） |
| tree/treeitem、roving tabindex、层级信息 | 无障碍 | Markdown Accessibility、TSX attributes | 文档确认 + 源码确认 | 已实现（层级信息除外，WPF 无 UIA Level） |

### 冲突或有意差异

| 冲突或有意差异 | 备选方案 | 采用方案与理由 | 影响范围 | 验证方式与状态 |
|---|---|---|---|---|
| React `expandedIds` 数组 | `ObservableCollection`、DP 集合 | 使用 `IReadOnlyList<string>`/`ObservableCollection<string>` 设计稿，避免公开可变共享默认值 | API/绑定 | 实现前固定具体类型 |
| React `onSelect` 回调 | ICommand、路由事件、两者 | `SelectedNode` 只读状态 + `Selected` 路由事件；可选 `Command` 后续评估 | API | 设计选择，待实现 |
| ARIA treeitem 属性 | 自定义 AutomationPeer | `TreeViewAutomationPeer`（Tree、Selection）与 `TreeViewRowAutomationPeer`（TreeItem、ExpandCollapse、SelectionItem、PositionInSet/SizeOfSet）；WPF 没有 UIA Level 的提供途径，`aria-level` 无法映射 | 无障碍 | `AutomationSeesATreeOfItemsThatExpandAndSelect` 通过；屏幕阅读器未运行 |
| CSS 颜色混合文件图标 | 纯语义令牌 | 主题按 CSS 的 oklab 混合算出 `ArcWpf.Brush.TreeFile{TypeScript,Stylesheet,Markdown,Json}`，明暗各一份 | 视觉 | Wine 明暗截图目视通过 |
| CSS color 过渡（文字、文件夹图标 160ms） | 代码给每行建可动画画刷 | 冻结的主题画刷无法原地补间颜色，改为同位置两层（次要色、前景色）交叉淡化；中点颜色与 CSS 插值略有不同 | 视觉 | 终态程序化断言；过程未逐帧比对 |

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

必要模板部件：`PART_RowsHost`。默认行模板的根是公开的 `TreeViewRow`（`Button` 派生，负责行内过渡和 TreeItem 自动化），它按名称驱动 `Hover`、`Label`/`LabelStrong`、`FolderBase`/`FolderStrong`、`Folder`/`FolderOpen`、`Chevron`；树按名称驱动 `Selection`、`Branch`。缺少的部件跳过，不阻断节点数据和键盘行为；自定义行模板不用 `TreeViewRow` 时只失去这些过渡和 TreeItem 语义。

### 懒加载（2026-11-11，已实现）

- `TreeNode.HasLazyChildren`：标记“子项在第一次展开时读取”的文件夹；`Children` 为空也按文件夹显示。
- `TreeNode.LoadState`（只读：NotLoaded / Loading / Loaded / Failed）、`TreeNode.LoadError`（只读，失败原因）。
- `TreeView.ChildrenLoader`：`Task<IEnumerable<TreeNode>> (TreeNode node, CancellationToken token)`。可以在任何线程完成，结果由控件切回界面线程；同步抛出的异常按失败处理。
- `TreeView.Reload(node)`：把节点标记为未读取；节点展开着就立即重新读取，旧子项保留到新结果到达。
- `TreeView.ChildrenLoadFailed` 路由事件（`Node`、`Error`）；`TreeView.IsSpinning` 只读，动效 Off 时为 false。
- 规则：凡是“可见、已展开、懒加载、状态为 NotLoaded 或 Failed”的节点都会开始读取，所以 `DefaultExpandedIds`/`ExpandedIds` 里恢复的懒加载路径会逐层读取。失败后节点被移出展开集合，避免自动反复重试。
- 更换 `ChildrenLoader` 会取消进行中的读取，不重新读取已完成的分支。

### 子项变更

已展开分支的 `Children` 增删会自动重建可见行，同一轮消息里的多次变更只重建一次（弱事件订阅，节点不会让控件无法回收）。收起的分支不监听：变更在它下次展开时自然出现。叶子节点第一次获得子项不会自动变成文件夹；文件树的目录节点应设 `HasLazyChildren`。

### 虚拟化（2026-11-11，已实现）

默认模板的行宿主是 `VirtualizingStackPanel`（像素滚动，Standard 模式），滚动条在行宿主内部。高度受限（`Height`、`MaxHeight` 或父布局给出有限高度）时只生成视口附近的行；放在 `StackPanel` 这类不限高的容器里仍会生成全部行，行为与首版相同。

- 可见行仍是整体重建后一次性换入（一次 Reset），滚动位置保持。
- 键盘移动到尚未生成的行（Home/End、方向键）时先滚动到该行再给焦点。
- 重建行会销毁持有键盘焦点的行；重建后焦点还给当前行，不触发滚动。这同时修复了首版里“键盘展开/收起后焦点掉到窗口、方向键失效”的缺陷。
- 宽度随内容自适应时，宽度只按已生成的行计算，滚动中可能变化；大树应让宽度由父布局决定。

## 视觉与动效

- 树外壳：Surface、Border、1 DIP 边框、Panel 圆角；不得使用黑色默认 Focus Adorner。
- 行高36 DIP，行间2 DIP；行（含选中与悬停底色）整宽，深度只缩进内容：左 10 + 18×(深度−1)，列为 Chevron 14、间距 8、图标 16、间距 8、标签。文字单行省略号。
- 分支线：深度 > 1 的每一行在父级 Chevron 中心（16.5 + 18×(深度−2)）画 1 DIP 竖线，上下各缩 4。
- 图标：lucide ChevronRight 14（线宽 1.9）、File/Folder/FolderOpen 16（线宽 1.7）。文件夹默认 TextSecondary，展开或选中为 Foreground；文件按扩展名着色（.ts/.tsx、.css、.md、.json），其余 TextMuted。
- 悬停：SurfaceMuted 底色，标签变 Foreground；选中：SurfaceMuted 底色，标签 Foreground。禁用节点整行 0.45 不透明度（来源无禁用态，属 ArcWpf 设计）。

### 动效清单（tree-view.tsx / .module.css）

| 触发 | 属性与参数 | WPF 实现 | 证据 |
|---|---|---|---|
| 子项进入 | 高度 0→38 smooth 弹簧；不透明 0→1 fast 160ms enter；x −6→0 smooth；打开的文件夹之后第 n 行延迟 min(n−1,7)×35ms，其余进入行无延迟 | 容器（ItemsControl 的 ContentPresenter）的 Height/Opacity/TranslateTransform，结束后回到自动高度 | `OpeningAFolderGrows...`；Wine 时间序列 |
| 子项退出 | 高度→0 smooth；不透明→0 instant 120ms standard；x→−4 同时长；退出期间不接收输入、立即去掉选中底色 | 行留在列表里直到高度动画结束再移除 | 同上 |
| 退出中再展开 | 同一批行从当前值长回 | 复用原行与容器 | `ReopeningAClosingFolder...` |
| 分支线进入 | scaleY 0→1（顶部原点）、不透明 0→1，standard 240ms enter，与行同延迟 | 行模板 `Branch` | 程序化 |
| 选中底色 | 共享 layoutId，在行间 morph 弹簧（.42）滑动 | 新行的 `Selection` 从旧行位置平移到 0；滑动期间该行 ZIndex −1，从其他行文字下方经过 | `SelectionGlides...` |
| Chevron | 0°↔90° snappy 弹簧（.26） | `TreeViewRow` 每次新建 RotateTransform 并动画 | `ChevronTurns...` |
| 文件夹图标 | popLayout：新图标不透明 0→1（fast enter）、缩放 .6→1（snappy）、模糊 2→0；旧图标不透明→0、缩放→.6、模糊→2（instant standard） | `Folder`/`FolderOpen` 两层，BlurEffect 结束后移除 | 同上 |
| 悬停/选中颜色 | color、background 160ms standard | 底色与两层标签、文件夹前景层的不透明度补间 | `ChevronTurns...` 终态 |
| 行位置 | `layout="position"` smooth | 前面行的高度动画带动后面的行，不另设位移 | Wine 时间序列 |

- 大批变化（一次增删超过 256 行）或子项顺序改变时整体换入，不做行动效。
- Reduced/Off：行、底色、图标立即到位；颜色不过渡（来源 `prefers-reduced-motion` 去掉 transition）。从 Full 切到 Reduced/Off、卸载、换模板时，退出中的行立即移除，其余行停止动画并落在终态。
- 读取中的弧线：Reduced 仍转动，Off 静止（见懒加载）。
- 由宿主整体加载快照的 JSONViewer 不使用这些行动效（它有自己的展开动画）。

## 验收场景

1. 创建 `app` 文件夹及两个文件、根级 `README.md`，默认展开 app，确认深度、图标和可见顺序。
2. 点击/Enter/Space 文件夹，确认展开/收起、`ExpandedChanged` 和子项可见性。
3. 使用 ArrowDown/Up、Home/End、ArrowLeft/Right，确认只在可见行移动并正确进入父/首子。
4. 选择文件和文件夹，确认单选高亮、`SelectedNode` 和 `Selected` 事件。
5. 禁用节点，确认不响应激活但保持层级显示；关闭分支的子项不进入 Tab/箭头顺序。
6. 受控 `ExpandedIds` 与绑定更新往返；快速反向展开确认无旧动画完成回调。
7. 在 100%/150%/200% DPI、窄宽度和深层树下测量行高、缩进、文字省略号、分支线和无黑色焦点框。
8. Automation 检查树名称、层级、展开/选择状态和 roving focus；切换明暗主题与 Reduced/Off。

## 完整度矩阵

| 交付项 | 本次范围 | 实现状态 | 验证证据 | 剩余差异/下一步 |
|---|---|---|---|---|
| API与绑定 | 必做 | 已实现 | 来源 API 已固定 | — |
| 控件外形与内部对齐 | 必做 | 已实现（2026-11-12 改为整行宽、补 FolderOpen、文件类型色、深层分支线、悬停底色） | `RowsAreFullWidth...` 测量；Wine 明暗截图 | 真实 Windows 字体下未看 |
| 内容组合与状态 | 必做 | 已实现 | TSX flatten/keyboard 已固定 | 自定义 `Icon` 首版模板里从未显示，2026-11-12 修正 |
| 动效与打断清理 | 必做 | 已实现，待验收（2026-11-12） | `TreeViewTests` 动效 5 项（真实时钟）；Wine 时间序列与截图 | 逐帧流畅度、真实 Windows 未运行 |
| 输入/自动化/主题 | 必做 | 已实现，待验收（2026-11-12） | `AutomationSeesATreeOfItemsThatExpandAndSelect` | UIA Level 无法提供；屏幕阅读器未运行 |
| 原版主示例与扩展示例 | 必做 | 已实现 | 页面 Usage 已固定 | Gallery 文件树 + 状态扩展 |
| 懒加载、取消、失败与重试 | 必做（2026-11-11） | 已实现，待验收 | `TreeViewTests` 懒加载 4 项；Wine 真实点击 | 无 Arc 来源；真实 Windows 未运行 |
| 虚拟化与键盘进入未生成的行 | 必做（2026-11-11） | 已实现，待验收 | `TreeViewTests` 虚拟化 2 项、焦点 2 项；Wine 真实滚轮和按键 | 真实滚动帧率未测 |
| 子项增删自动反映 | 必做（2026-11-11） | 已实现，待验收 | `ChildrenAddedToAnOpenBranchAppearWithoutBeingAskedFor` | 收起分支不监听（设计） |

### 与规格不符的既有项（2026-11-11 发现，2026-11-12 已处理）

2026-11-11 核对时发现上表标为“已实现”的展开/收起高度与逐项进入动效、选中背景弹簧滑动、Chevron 旋转过渡、FolderOpen 图标与文件类型颜色、自定义 `AutomationPeer` 在代码里不存在，行高亮还随层级缩进。2026-11-12 全部补齐，见上方动效清单和[验证记录](../verification/2026-11-12-treeview-motion-automation.md)。同时发现并修正：分支线只在第 2 层显示、自定义 `Icon` 不显示、选中标签仍为次要色、悬停没有底色。
