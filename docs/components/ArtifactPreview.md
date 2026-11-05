# ArtifactPreview

状态：待验收。运行结果的标签页预览：代码、JSON、Markdown、文件。事实见[参考卡](../references/components/ArtifactPreview.md)。

Arc 没有同名组件，也没有 Markdown 组件。这是 ArcWpf 的组合设计，**没有原版可供比对**。

## 范围与 API

`ArtifactItem`（`Title`、`Kind`、`Content`、`FileName`、`Language`、`Detail`、`FilePath`、`Preview`）；`ArtifactPreview`：`Items`、`SelectedIndex`、`BodyMaxHeight`、`EmptyText`；事件 `OpenRequested`、`DownloadRequested`。无效 JSON 显示原因和原文。

## 复用情况

复用 `Tabs`、`CodeBlock`、`JSONViewer`、`StreamingMessage`（新增内部 `HidesFoot`）、`OverlayScrollViewer`、`CopyButton`。Tabs 的切换动画由其自身负责。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 切换标签 | Tabs 自带：滑动、淡入、高度弹簧；既有实现 | `Tabs` | 真实输入 art-*；`SelectedIndexAndTabs...` | — |
| Markdown | 整段一次显示，不重放流式；WPF适配 | `Markdown` | `MarkdownRenders...` | — |

## 已知差异

- Markdown 只支持回复渲染器的子集：段落、标题、列表、粗体、行内代码、代码块；表格和图片不支持。
- 所有标签内容在添加时一次构建，未对超大结果做延迟加载。
- Open、Download 只发事件。
- 验证环境为 Wine，字体为替身；真实 Windows 未运行，见[验证记录](../verification/2026-11-04-batch12-replication.md)。
