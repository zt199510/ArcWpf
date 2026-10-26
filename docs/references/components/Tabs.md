# Tabs参考卡

[原页](https://uiarc.dev/components/tabs)、[冻结清单](../../../tests/Baselines/References/2026-10-26-tabs/manifest.json)。2026-10-26-tabs为项目采集批次，非上游版本；免费Registry与MIT许可页已冻结。依赖仅作参数参考，不引入React/Radix/Motion。证据为源码/HTML/CSS/公开预览JS，未做实际交互观察。

| 对象 | 来源事实 | WPF采用 |
|---|---|---|
| 主预览 | Workspace views；Overview默认选中，Activity，Settings；最大420宽，居中标题与内容 | 同文案/顺序/初态 |
| 内容 | Overview: A calm summary of this workspace and its current health.；Activity: Emma reviewed the latest changes a few minutes ago.；Settings: Choose who can publish and receive notifications. | 来自冻结预览JS，不推测未打开面板 |
| 外壳 | Border1、radius18、SurfaceMuted；列表padding4/gap4 | 仅水平方向；标签整体不随按压缩放 |
| 标签 | minWidth88、minHeight36、padding左右12、radius15；字号14/Medium、TextMuted，选中AccentStrong | TabItem.Header和原生TabControl选择 |
| 指示器 | Surface、Border1、radius15、Resting阴影；morph .42移动 | 每个Tabs独立共享背景，位于标签下方 |
| 内容区 | 与标签条间距16，TextSecondary/14/line1.4 | 默认自动高度；动态内容后续仍可自然重排 |
| 内容动效 | 进入x±8、opacity240ms、smooth .4；退出反向6、120ms；高度smooth .4 | WPF独占内部高度；旧内容仅非交互图像，不挂第二份活控件 |
| Reduced | 指示器即时；内容进入120ms/退出100ms淡化，无高度动画 | Off全部即时 |
| 溢出 | 33宽两侧按钮、35边缘淡化，每次滚动视口75%；焦点/选中自动显露 | 无换行；禁用/隐藏项跳过；RTL适配 |

共享颜色AccentStrong浅色OKLCH .24、深色.91，补项目语义资源键；不更改现有控件颜色。字体沿用Windows回退。位图退出与Motion求解器差异在规格中说明。
