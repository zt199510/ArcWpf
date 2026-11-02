# JSONViewer 参考卡

Arc 页面：`https://uiarc.dev/components/json-viewer`。2026-11-02 冻结 Registry、CSS、Markdown、页面、动效令牌和许可证；文件及 SHA-256 见 `tests/Baselines/References/2026-11-02-json-viewer/manifest.json`。组件为 Free/MIT。

源码确认：`data` 是任意 JSON-like 值；默认根名 `root`、默认展开深度 1、默认可搜索、分页大小 50、可复制值和路径、最大树区高 420px。树行按 WAI-ARIA tree pattern 展示，分支显示 object/array 类型和 key/item 数量，原始值按 string/number/boolean/null 分类着色。搜索同时匹配 key 和 primitive value，打开命中祖先并显示匹配计数；长分支以 Show more 分页。

交互：ArrowUp/Down 移动可见行，ArrowRight 展开或进入子项，ArrowLeft 收起或回父项，Home/End 跳首尾，Enter/Space 切换分支或加载下一页；Ctrl/Cmd+C 复制值，Shift 复制路径；`/` 聚焦搜索，Enter/Shift+Enter 跳匹配，Escape 清空，搜索中 ArrowDown 回到树。当前路径栏提供复制路径。

动效：行展开使用高度 spring，当前行背景平移，复制图标切换为 check/error；Reduced 或超过 400 可见行时直接落点。树区域有限高度滚动，窄宽度缩小缩进并隐藏路径类型。

WPF 映射：复用现有 ArcWpf `TreeView` 的节点、展开集合、键盘和 UIA 行语义；JSONViewer 自己维护路径索引、搜索命中和复制操作。分页与搜索不修改原始 data，Copy Value 对分支使用格式化 JSON，Copy Path 使用 JSONPath 风格字符串。
