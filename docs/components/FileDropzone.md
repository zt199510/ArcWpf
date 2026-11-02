# FileDropzone

状态：WPF 视觉层已补齐，已完成构建与 Gallery 启动检查；真实拖放、文件选择器、DPI 和主题切换仍待人工窗口验收。

来源：`tests/Baselines/References/2026-11-02-file-dropzone/`。使用 WPF `AllowDrop`、`DragEnter/Drop` 语义和 `OpenFileDialog`，支持多文件、数量上限和文件列表。模板已对齐 Arc 的主要层级：圆角 surface、虚线边缘、拖拽 wash/glow、纸张堆叠提示、类型文件图标占位、缩略图契约、文件大小/状态、进度条和重试/取消/移除操作。上传异步回调、进度、取消、失败/取消重试和移除条目已暴露。

已实现的 WPF 扩展契约：`Note`、`DropLabel`、`MaxSize`、`ListPlacement`（`below`/`inside`）、`CompactAt`，以及 `FileDropzoneItem.Preview`、`FilePath`、`Retryable`、`FormattedSize`。超过 `MaxSize` 的文件会进入失败状态且不会启动上传。

与 Arc Web 的有意适配差异：WPF 使用 `FilePath` 承载本地文件路径，不直接暴露浏览器 `File`；模板使用 WPF `Rectangle` 虚线边缘和原生 `ProgressBar`，拖拽光晕为静态资源层叠加，连续指针跟随和逐帧弹簧仍待后续动效迭代。Ctrl+V 现在支持剪贴板文件列表和图片（图片会写入临时 PNG）；自动对象 URL 生命周期和容器查询响应式断点仍待后续扩展。
