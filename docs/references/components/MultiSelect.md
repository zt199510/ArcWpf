# MultiSelect 参考卡

来源：[Multi-select](https://uiarc.dev/components/multi-select)，免费 Registry；许可页确认免费组件 MIT。项目采集批次 2026-10-26-multi-select，无官方版本号；[冻结文件与 SHA-256](../../../tests/Baselines/References/2026-10-26-multi-select/manifest.json)。仅参考行为/视觉，不打包 React、Motion 或 Lucide。页面与 Registry 是否同一部署版本未知；未观察原站动态运行。

| 层级 | 源码确认 | WPF 映射 |
|---|---|---|
| 主例 | 文档 Usage：Labels，初选 bug，Bug/Feature/Docs | 采用文档样本，不把 Usage 冒充网站交互预览；展示宽度 400 为 WPF 选择 |
| 外框 | min-height44，radius-control18，边框1，padding5/11/5/12 | 标签/描述单独行；外框44；不缩放触发框 |
| 内容 | 单行、默认2项、剩余+N，标签最大144，右距5，字号12，padding4/8 | 裁剪内容区；标签删除增加14宽按钮，明确扩展 |
| 操作 | 箭头16，clear22，right36；有值时额外26空间 | 固定箭头列；清除位只在有值/退场时占位 |
| 浮层 | offset8，padding5，radius-panel26，option半径21/最小38，左右11；源码无滚动 | 复用 Select 的定位与生命周期模式；增加有限滚动和原生 Search |
| 颜色 | 外框/标签/clear160ms；选项80ms；禁用外框.5/选项.42 | 私有画刷补间，动态主题不覆盖公共绑定 |
| 标签 | slot width smooth .4；opacity160/120ms；scale .9→1 snappy .26；blur4→0/退出2 | 内部槽宽动画，退场保留元素；Reduced仅120ms淡化 |
| +N | 计数增大从下、减小从上.5em，240/120ms，blur2 | 双层文字滚动；槽宽随数值变化 |
| 其他 | placeholder120淡化；clear .6缩放/blur2，按压.96；箭头180°；菜单-6/.97→0/1，退出-4/.98；勾号pathLength240ms | WPF专用属性与时钟；弹簧采用现有采样近似，不保跨反向速度 |

搜索、逐标签删除、最大菜单高度、Tab离开关闭为本次 WPF 扩展。原版无自由输入新增项、网络请求或虚拟化，本次也不实现。未知值以原字符串显示，与源码一致。尚未建立已审查 WPF 视觉基准。

2026-10-26 用户反馈标签遮挡后的 WPF 适配：MaxVisible 作为上限，窄宽度折叠更多标签到 +N；天然文本宽度按DPI取整并留1物理像素。原站没有该响应式折叠规则；依据为本项目用户反馈与布局回归。
