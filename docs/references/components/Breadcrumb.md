# Breadcrumb 参考基准

[原页](https://uiarc.dev/components/breadcrumb)、[Markdown 合约](https://uiarc.dev/components/breadcrumb/markdown)、[Registry](https://uiarc.dev/r/breadcrumb.json)、[许可证](https://uiarc.dev/license)、[冻结清单](../../../tests/Baselines/References/2026-10-26-breadcrumb/manifest.json)。

`2026-10-26-breadcrumb` 是项目采集批次，不是上游发行版本。快照包含页面、Markdown、Registry JSON、TSX、CSS、ArcProvider 参考文件、共享动效令牌和许可页。来源页面标记该组件为 Free；许可证页说明 Free 组件采用 MIT。本项目只参考行为和视觉，不复制 React、Motion、lucide 或 ArcLink 运行时代码。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 文案、内容组合 | `Workspace / Settings / Billing`，最后一项是当前页 | Markdown Usage、Registry TSX | Gallery 使用同源文案 |
| 初始值与状态 | 前两项可导航，最后一项 `aria-current=page` | Markdown Accessibility、TSX `current` | WPF 用 `IsCurrent` 与 AutomationPeer |
| 控件本体尺寸/圆角 | 无外壳固定尺寸；文字行高、链接内边距、Chevron 14/16px | CSS `.list`、`.list li` | 使用 WPF 文本和矢量 Chevron |
| 字体、颜色、边框 | 非当前项 TextSecondary，当前项 AccentStrong/Medium；链接下划线悬停出现 | CSS `.list a`、`[aria-current]` | 使用 ArcWpf 语义资源 |
| 内部对齐锚点 | Flex 行内对齐，Chevron 独立布局单元并居中 | CSS、TSX | WPF `WrapPanel` + 独立 Path 单元 |
| 外部间距 | 行间/列间 gap，窄容器换行 | CSS `.list`、Markdown Responsive | WPF `WrapPanel` |
| 展示容器宽度/背景 | 组件本身透明，不截断、不折叠路径；单个 crumb 不换行 | Markdown Responsive、CSS | Gallery 由页面容器控制 |

## 状态与动效索引

- 只有最后一项为当前页，当前页渲染文本而不是链接或按钮。
- 非当前项可使用 `href` 或 `onClick`；WPF 映射为可选 `ICommand`。
- 新增 crumb 从左侧 8px、轻微 blur 和透明状态进入；已有 crumb 保持静止，兄弟位置用 smooth spring 调整。
- Reduced motion 直接增加/移除，不做位移和 blur。
- 路径可换行，单个 crumb 保持不换行；不自动截断或折叠长路径。

## WPF 差异和回归入口

- WPF 使用 `BreadcrumbItem` 集合、`ICommand` 和路由 `ItemInvoked` 事件；不复制 `href`、React MouseEvent 或 ArcProvider Link 注入。
- WPF 用 `AutomationPeer`/AutomationProperties 表达导航名称、当前页和按钮名称；不把 `aria-current` 当成 WPF API。
- 当前实现已建立控件、主题、Gallery 和程序化测试；实际窗口焦点、DPI、长路径换行和动效观感仍待验收。
