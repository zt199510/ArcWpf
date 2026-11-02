# SortableDataTable 参考卡

Arc 页面：`https://uiarc.dev/components/sortable-data-table`。2026-11-02 冻结 Registry、CSS、Markdown、页面、动效令牌和许可证；文件及 SHA-256 见 `tests/Baselines/References/2026-11-02-sortable-data-table/manifest.json`。组件为 Free/MIT。

源码确认：输入 rows、columns、rowKey；列可排序、数值右对齐、宽度可固定；可选 selectable 增加全选/行选择、选中数量和 Clear。空数据显示 `No rows to show`。表格在内部水平滚动，窄宽度折叠为双行信息。排序在客户端完成，行按 spring 位移动效重排；Reduced 立即重排。

键盘：左右移动可排序表头，Enter/Space 排序；上下移动行选择框，Home/End 跳首尾，Escape 清空选择。WPF 适配使用 `DataGrid` 保留原生表头、滚动、选择和自动化语义；排序状态和选择绑定由控件统一管理，列宽不因排序重新生成。
