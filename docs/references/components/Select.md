# Select 参考卡

[原页](https://uiarc.dev/components/select)、[批次与哈希](../../../tests/Baselines/References/2026-10-26-select/manifest.json)。批次2026-10-26-select非官方版本。免费Registry组件，MIT依据随许可页冻结。源码和静态HTML/CSS/公开预览脚本作为证据，未操作页面。

| 对象 | 确认事实 | WPF映射 |
|---|---|---|
| 主样本 | Team；Design初选；Design/Engineering/Research；Choose the team that owns this work.；roomy宽540 | 同文案/初值/最大宽 |
| 字段 | label14/Medium；字段gap8；hint12/TextMuted | 标签与说明独立行；触发框高44 |
| 触发框 | border1/radius18/padding左右12/gap12；不按压缩放 | 内部透明ToggleButton打开菜单，外框保持固定 |
| 菜单 | offset4/collision12；padding6/viewport上下2；radius20；高上限320；宽与触发器相同 | Popup独立窗口；工作区不足夹紧/翻转；源CSS min-width触发器可能压过max384，本实现优先同宽/屏幕可达 |
| 选项 | minheight36/radius14/padding左11右34；右10的16勾号；disabled opacity.45 | SelectItem原生ComboBoxItem派生；默认矢量图标自主绘制，不打包Lucide资产 |
| 文案切换 | 向后选项从下方.35em进入/旧值向上.3em退出；240/160ms，blur4/2 | 文本快照+当前内容；自定义ItemTemplate不制作错误文本快照，立即换内容 |
| 菜单动画 | 进入240ms opacity0/translate6/scale.97；退出120ms translate3/scale.98 | IsDropDownOpen逻辑关闭后退出宿主；Reduced120ms淡化；Off即时 |
| 箭头/指示器 | 旋转180° spring580ms；选中勾号240ms延迟40、scale.6/blur2 | 内部独占动画属性，清理后保持终态 |

允许适配：原生ComboBox键盘选择/文本前缀搜索；滚动使用WPF滚动条代替28px上下滚动按钮；浮层复用项目单层主阴影，非CSS双阴影像素等效。完整实际交互和DPI待验收。
