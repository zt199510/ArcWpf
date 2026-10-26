# Search 参考卡

2026-10-26冻结：[Search field](https://uiarc.dev/components/search-field)、[Registry](https://uiarc.dev/r/search-field.json)、Markdown和许可页，目录tests/Baselines/References/2026-10-26-search，哈希见manifest-hashes.json。上游无版本号，批次+哈希为身份。免费MIT组件，React/Motion/Lucide仅作源码参考，不引入运行依赖。原站无search条目，llms目录确认名称为search-field；Expanding search为独立组件。

源码/CSS确认：label与shell间距8；shell最小44高、边框1、圆角18、水平padding12、内部gap8；图标18，文本14；清除槽24×24永久占位，X图标16。空值图标TextMuted，输入聚焦或非空Foreground；focus/hover边框Foreground；Clear hover SurfaceMuted。主示例Search members/Name or email，空query，按输入过滤由使用方负责。

动效：清除按钮进入opacity0→1(.16)，scale .8→1(snappy .26)，blur2→0(.16)；退出opacity0/scale.8/blur2(.12)；按压scale.96(.12)。Reduced仅淡入(.12)、退出直接隐藏（源码优先于文档概括）；Off立即更新。WPF使用OwnedMotion近似曲线，内部视觉独占动画属性，固定24槽不改变输入宽度。源码未声明远程搜索、防抖或结果列表。字体Windows回退，原站截图/运行环境未采集。
