# DiffView

状态：待验收。显示代码改动，供审批文件修改、查看 Agent 改了什么时使用。事实见[参考卡](../references/components/DiffView.md)。

Arc 有一个相近的 Pro 组件 Code suggestion：单文件、少量 hunk、逐段接受或拒绝。它的文档把大型多文件和并排对比留给专门工具，所以两者定位不同。本控件的功能是 ArcWpf 的设计，**行样式按它的公开样式值校准**。早先写的“Arc 没有 diff 组件”是错的：当时搜索目录时截断了结果。

## 范围

| 交付项 | 本次范围 | 实现状态 | 验证证据 | 剩余差异/下一步 |
|---|---|---|---|---|
| 从新旧文本计算差异 | 必做 | 已实现 | 随机往返测试 | 真实 Windows 未运行 |
| 解析 unified diff / git patch | 必做 | 已实现 | 专项测试 | 真实 Windows 未运行 |
| 单栏、并排，窄宽度回落单栏 | 必做 | 已实现 | 专项测试、真实输入 | 真实 Windows 未运行 |
| 折叠未改动区域，点击展开 | 必做 | 已实现 | 专项测试、真实输入 | 真实 Windows 未运行 |
| 多文件切换 | 必做 | 已实现 | 专项测试、真实输入 | 真实 Windows 未运行 |
| 上一处/下一处改动 | 必做 | 已实现 | 专项测试、真实输入 | 真实 Windows 未运行 |
| 行内改动高亮、语法着色 | 必做 | 已实现 | 专项测试、目视自查 | 按行着色，跨行注释不识别 |
| 大文件虚拟化与截断提示 | 必做 | 已实现 | 专项测试、真实输入 | 真实 Windows 未运行 |
| 行选择与复制 | 必做 | 已实现 | 专项测试、真实输入 | Shift+点击和拖动在 Wine 下无法验证 |
| 自动换行、字符级选择、接受/拒绝单个 hunk、行内评论、合并视图 | 暂缓 | 未实现 | — | 需要时再加 |

## 审批用途带来的约束

这个控件是用户决定是否放行修改的依据，**不能让改动看起来比实际少**：

- 不可见字符和双向控制符显示为可见标记（如 `[U+202E]`，用警告色），文件头写提示。复制仍得到原文。
- 只改换行符、只改文件末尾换行、改文件权限，都写提示，不显示成“无改动”。
- 截断、对齐失败、patch 读不全时都写提示。无法解析的内容不显示成“无改动”。

## WPF 公开契约

`DiffFile`（不可变，线程安全，可在后台构建）：

- `DiffFile.FromText(path, oldText, newText, oldPath = null, language = null)`：`oldText` 为 null 表示新建，`newText` 为 null 表示删除。
- `DiffFile.ParsePatch(patch)`：返回 `IReadOnlyList<DiffFile>`。
- 只读：`Path`、`OldPath`、`Status`、`Language`、`IsBinary`、`AddedLines`、`RemovedLines`、`Notes`、`Lines`。

`DiffView : Control`：

| 属性 | 类型/默认 | 说明 |
|---|---|---|
| `Files` | `IEnumerable<DiffFile>?` | 可绑定；实现 `INotifyCollectionChanged` 时跟随变化 |
| `SelectedFile` | `DiffFile?`，双向 | 默认第一个 |
| `Mode` | `DiffViewMode.Unified`，双向 | 用户选的布局 |
| `EffectiveMode` | 只读 | 窄于 `MinSplitWidth`、或只增只删时为 Unified |
| `MinSplitWidth` | 720 | |
| `ContextLines` | 3 | 改动两侧保留的上下文行 |
| `MaxRows` | 20000 | 超出部分不显示并提示 |
| `MaxBodyHeight` | ∞ | 父容器不限高时最高 480，否则虚拟化失效 |
| `EmptyText` | `No changes` | |

方法：`NextChange()`、`PreviousChange()`、`ExpandAll()`。只读 `ChangeCount`。

### 键盘

| 按键 | 行为 |
|---|---|
| 上下、PageUp/PageDown、Home/End | 移动行（ListBox 原生） |
| 左右、Shift+滚轮 | 横向滚动，行号列不动 |
| Shift+方向键、Shift+点击、拖动 | 选择多行 |
| Ctrl+C | 复制选中行原文；并排时复制最后点击的一侧，默认新版本 |
| Enter、Space | 展开选中的折叠行 |
| F7 / Shift+F7 | 下一处/上一处改动 |

## 实现选择

- 行是 `ListBoxItem` 子类，直接用 `DrawingContext` 绘制：大 diff 下比每行一棵 TextBlock 树轻，也能固定行号列。保留 ListBox 的虚拟化、选择、键盘和 UIA。
- 差异算法为 Myers，先去掉首尾相同的行。编辑距离超过 2048 时把中段当作整段替换，并写提示。
- 语法着色复用 CodeBlock 的分词，提取到内部 `CodeTokens`。**按行着色**，跨行注释和模板字符串的后续行不会被识别。
- 着色和行内高亮在行首次显示时才计算。

## 动效

来源无动效依据，首版不加：行、布局切换、展开都是立即完成，所以 Full/Reduced/Off 行为相同。头部按钮的悬停过渡来自 `ToolbarButton`。

## 验收场景

1. 修改、新建、删除、重命名各一个文件：行号、`+`/`-`、统计数字与内容相符。
2. 切换单栏和并排，当前行保持在视口内；窗口变窄后自动单栏。
3. 点折叠行展开；patch 模式下被 hunk 跳过的内容不可展开。
4. 上万行 diff：只创建视口内的行容器，滚动流畅。
5. 长行横向滚动时行号不动。
6. 只改换行符、含 `U+202E`、二进制、无法解析的 patch：都有明确提示。
7. 明暗主题运行时切换，已显示的行颜色更新。
8. 放进 StackPanel（不限高）时高度受限，仍然虚拟化；放进 Grid 时填满。

## 实施与证据

代码在 `src/ArcWpf.Controls/DiffView/`，测试 `tests/ArcWpf.Tests/DiffViewTests.cs`，Gallery 页 `DiffViewPage`。结果见[验证记录](../verification/2026-11-05-diff-view.md)。尚未建立回归基准。

## 已知差异

- 只能按行选择和复制，不能选中行内的部分文字。
- 没有接进 ApprovalCard 或示例程序。
- 着色仍是 CodeBlock 的分词，C# 等语言的关键字不全。
