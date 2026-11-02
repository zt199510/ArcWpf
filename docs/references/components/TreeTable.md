# TreeTable 参考卡

Arc 页面：`https://uiarc.dev/components/tree-table`。2026-11-02 冻结 Registry、CSS、Markdown、页面、动效令牌和许可证；文件及 SHA-256 见 `tests/Baselines/References/2026-11-02-tree-table/manifest.json`。组件为 Free/MIT。

源码确认：TreeTable 是带列的层级 treegrid。行包含 id/data/children/hasChildren；列提供 cell、sortValue、width、align、hideBelow。支持层级展开、父级三态选择、同级排序、懒加载子项、加载失败重试、最大高度滚动和空状态。键盘遵循 treegrid：方向键、Home/End、PageUp/PageDown、* 展开同级、Space 选择、Enter 激活或重试、Ctrl+A 全选。

WPF 适配：复用 TreeView 的路径/焦点/展开思想和 SortableDataTable 的列/排序语义；使用自有行面板保留层级、列轨道和 loading/error 行，不把 DataGrid 的平面模型强行扩展为树。真实窗口逐帧排序、懒加载和触摸仍单独验收。
