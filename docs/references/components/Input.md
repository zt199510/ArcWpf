# Input 参考基准

来源：[原页](https://uiarc.dev/components/input)、[冻结批次](../2026-10-25-initial.manifest.json)、[源码](../../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/input/input.tsx)、[CSS](../../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/input/input.module.css)。
图片：[用户参考图](../../../tests/Baselines/References/2026-10-25-user-feedback/input-reference.png)，哈希见 [图片清单](../../../tests/Baselines/References/2026-10-25-user-feedback/manifest.json)。截图DPI/环境未知，不建立全图像素一致性承诺。

| 对象 | 基准/证据 | WPF采用/限制 |
|---|---|---|
| 主示例 | Project name / Arc launch / Use a short, descriptive name.；用户图及此前页面HTML | 已使用同文案与初始值 |
| 宽度 | 参考图约540，之前原站预览CSSroomy上限540 | Gallery最大540、窄窗口收缩；不是控件固定宽 |
| 编辑面 | 源码：最小44高、圆角18、描边1、水平padding12 | 白色Surface；预览区浅灰属于Gallery |
| 标签 | 字号14、字重500、间隔8；源码 | Medium、行高20为WPF适配 |
| 说明 | 字号12、上间隔8；源码 | 行高18为WPF适配；与字段左边框对齐 |
| 文本起点 | 占位与输入共享内部起点；CSS意图 | 当前默认X=13含1边框；布局测量容差0.5 DIP |
| 颜色/焦点 | Surface/BorderStrong/TextMuted；聚焦仅边框变色（CSS） | 已去掉额外外环，错误优先 |
| 交互 | 原生编辑、说明与错误并存（源码） | 保留TextBox；IME仍需实际窗口验证 |
| 动效 | 原版消息高度、词/数字变化（源码） | 本次范围明确暂缓；消息与边框即时变化 |

重复Padding缺陷曾使占位X=13、输入X=27。现由宿主应用Padding并补偿本运行时TextBoxView的2 DIP水平留白；不可直接推广到其他文本控件。标签/说明的外部左对齐与内部文本起点是两种对齐，不应合并。

最新对齐修复：程序化回归通过，2026-10-27用户明确确认通过。见 [验证记录](../../verification/2026-10-25-input.md)、[规格矩阵](../../components/Input.md)。字体回退和跨DPI专项仍待验收。
