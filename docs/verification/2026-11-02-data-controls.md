# 数据展示控件复刻验证记录（2026-11-02）

> 外观与部分 API 已被后续重做取代，以 [像素对照记录](2026-11-02-data-controls-pixel.md) 为准。

来源冻结批次：

- `tests/Baselines/References/2026-11-02-code-block/manifest.json`
- `tests/Baselines/References/2026-11-02-json-viewer/manifest.json`
- `tests/Baselines/References/2026-11-02-sortable-data-table/manifest.json`
- `tests/Baselines/References/2026-11-02-tree-table/manifest.json`

实现范围：

- CodeBlock：轻量 token 高亮、文件头、CopyButton、行号、内部横向滚动、`maxLines` 展开/收起、明暗语义资源。
- JSONViewer：复用 TreeView，支持 JSONPath 行 id、默认展开、搜索祖先展开、复制值/路径和有限高度树区。
- SortableDataTable：基于 WPF DataGrid，支持列定义、排序、稳定行键、选择状态、清空选择、空状态和内部滚动。
- TreeTable：复用 TreeView/DataTable 的节点和列语义，支持层级展开、同级排序、选择、空状态和有限高度滚动。

证据：四控件精确专项 **15/15 通过**；`dotnet build ArcWpf.slnx --no-restore` **0 错误**；四个 Gallery 页面均可构造。当前测试覆盖 API、模板部件、窗口布局、展开/折叠、搜索、复制、排序和嵌套行。

未覆盖：真实物理鼠标/触摸、长文本逐帧视觉、读屏、100/150/200% DPI、DataGrid 复杂编辑语义、TreeTable 异步懒加载的实际网络路径。上述项目保留为后续平台验收，不冒充源码复刻已完成的证据。
