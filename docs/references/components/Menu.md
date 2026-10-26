# DropdownMenu 参考基准

[原页](https://uiarc.dev/components/dropdown-menu)、[Markdown 合约](https://uiarc.dev/components/dropdown-menu/markdown)、[Registry](https://uiarc.dev/r/dropdown-menu.json)、[冻结清单](../../../tests/Baselines/References/2026-10-26-menu/manifest.json)。`2026-10-26-menu` 是项目采集批次，不是上游发行版本；当前快照包含页面、Markdown、Registry、CSS、motion tokens 和许可页。

| 问题 | Arc 静态证据 | WPF 适配 |
|---|---|---|
| 组件身份 | Dropdown menu，短命令列表，触发器锚定 | 项目类型暂命名 `Menu`，需在 API 文档中明确 |
| 触发器 | label、可选 icon、ChevronDown；标签变化时文字与宽度 morph | Button 模板；标签宽度和文字过渡必须补齐 |
| 菜单几何 | `sideOffset=6`、`align=end`、`collisionPadding=12`；最小12rem、最大可用宽度 | Popup 自定义 placement + 可用区域限制 |
| 条目 | label、icon、disabled、destructive、separatorBefore；短列表、无虚拟化 | MenuItem 数据对象 + ICommand 映射 |
| 键盘 | Enter/Space/ArrowDown 打开；上下循环、Home/End、typeahead、Escape | Button/ListBox 输入适配，禁用项跳过 |
| 高亮/动效 | 一个共享 highlight；鼠标 snappy spring，键盘即时跳转；菜单缩放淡入，条目 stagger | 模板 Overlay + OwnedMotion，Reduced 去空间运动 |
| 生命周期 | Portal 仅在打开时挂载；外部点击/Tab/Escape 关闭并回焦点 | Popup 关闭清理、焦点归还、旧动画取消 |

Registry 依赖 `@radix-ui/react-dropdown-menu`、`motion` 和 `lucide-react`，这些只作为行为、几何和动效证据，不进入 ArcWpf 产品运行依赖。
