# ComboBox 参考卡

原名 Combobox：[文档](https://uiarc.dev/components/combobox/markdown)、[页面](https://uiarc.dev/components/combobox)。免费公开Registry，许可页确认免费组件MIT；[项目采集批次与哈希](../../../tests/Baselines/References/2026-10-26-combobox/manifest.json)。批次非官方版本；未操作原站，不推断部署预览与Registry同版本。源码/CSS/Foundation/Motion完整冻结，仅参考行为/视觉，不引入React/Motion/Lucide运行库。

| 对象 | 源码/文档确认 | WPF映射 |
|---|---|---|
| 主样本 | Usage: Time zone，空初值，UTC/Central European/Pacific；cet别名zurich/berlin，pst别名san francisco | Gallery原文/初值；宽400为项目展示选择，不冒充官网容器 |
| 输入行 | 直接input，Search图标16，gap8，padding左右12，minheight44，radius18 | Input/TextBox派生，保留PART_ContentHost；单独图标/文字/clear/箭头列 |
| 占位 | 打开时显示query，所选标签作为占位；关闭显示所选label | SelectedValue与QueryText分离；关闭丢弃查询 |
| 菜单 | offset8，radius26/padding5，row36/padding左右12/radius21；max-height min300/40vh | 原生Popup同宽、屏幕翻转/夹紧；40vh采用宿主窗口高度40% |
| 搜索 | trim + locale lowercase；label与keywords包含匹配；查询变化active=-1 | CurrentCultureIgnoreCase，私有选项数组，不修改共享视图 |
| 选择 | 选中关闭/清query/聚焦input；Clear清值并打开；禁用不可选 | 字符串SelectedValue默认双向；ListBox原生单选容器与UIA |
| 颜色 | 外框/clear160ms，选项80ms | 私有画刷，动态主题与公共绑定保留 |
| 菜单动效 | 进入opacity160/spring snappy.26、y-6/scale.97；退出120、y-4/scale.98 | OwnedMotion采样近似；反向从当前值、不保速 |
| 列表高度 | smooth .4 ResizeObserver驱动 | 只动画内部裁剪宿主高度，菜单锚点与外框不改变 |
| 文案/空态 | 选中label y.35em/blur4→0/opacity240；清空fade160；空态y4/blur4/opacity240 | 编辑视觉内部动画，不动画用户Text/Width；空态独立宿主 |
| clear/箭头 | clear .6/blur2、spring.26/opacity160、退出120、按压.96；箭头180/580ms | 复用已有原生操作按钮按压/颜色逻辑；退场保留操作列直到完成 |

原站focus-within有焦点环；WPF遵循项目键盘焦点视觉，鼠标不显示键盘专用框。原版Up从无active时索引公式可能落倒数第二项；WPF按桌面习惯Up到最后一项。Tab离开关闭为WPF适配。用户要求不做computer-use，运行视觉交由用户人工验收。
