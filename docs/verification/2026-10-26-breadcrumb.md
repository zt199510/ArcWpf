# Breadcrumb 验证记录（2026-10-26）

## 来源与规格

- 已冻结 Arc Breadcrumb 页面、Markdown、Registry、CSS、ArcProvider、共享动效令牌和许可页，批次目录为 `tests/Baselines/References/2026-10-26-breadcrumb`。
- 已完成 [参考卡](../references/components/Breadcrumb.md) 与 [规格](../components/Breadcrumb.md)。

## 实现结果

- 已实现 `Breadcrumb` / `BreadcrumbItem`、ObservableCollection 路径更新、当前项、禁用项、ICommand、ItemInvoked、Chevron 分隔符和主题模板。
- 已接入 Gallery `BreadcrumbPage`，包含主路径、命令反馈、禁用项和窄容器示例。
- 已更新 Controls Generic.xaml 和 Gallery 顶层 Tabs。

## 程序化结果

- Breadcrumb 专项：4 项通过。
- 全量测试：204 项通过。
- 构建：通过；保留既有未使用事件和分析器警告。

## 待验收

- 实际窗口中的文字基线、Chevron 光学居中、窄容器换行、焦点、Automation、DPI 和动效观感尚未完成。
- 新增 crumb 的 8 DIP/blur 进入动效和自定义 AutomationPeer 尚未补齐；当前规格中保留为下一修订项。
