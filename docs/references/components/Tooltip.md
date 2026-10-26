# Tooltip参考卡

[原页](https://uiarc.dev/components/tooltip)、[冻结清单](../../../tests/Baselines/References/2026-10-26-tooltip/manifest.json)。2026-10-26-tooltip是项目采集批次，非上游版本；免费Registry/MIT，源码/HTML/共享预览JS证据，无截图或交互观察。

| 对象 | 来源事实 | WPF采用 |
|---|---|---|
| 主预览 | 信息图标，可聚焦；aria-label Project visibility information；提示Only workspace members can see this project；旁文Hover or focus the icon | 相同文案，矢量信息图标，自有可聚焦按钮 |
| 气泡 | 最大240，padding12/16，border1，radius18；Foreground底色/Background文字；border为Background14%与Foreground混合；Raised阴影，14/400/1.4 | 对应语义画刷；单层Raised主阴影 |
| 定位 | 默认Top，源码支持四方向，offset8/collision12；无箭头 | 复用Popover定位数学，WPF Popup |
| 延迟 | 首次250ms，任意提示打开期间及关闭后300ms为warm | Dispatcher内共享状态，无永久计时器；键盘焦点立即 |
| 进入/退出 | 首次160ms opacity/translateY3/scale.97到1（Bottom为-3，Left/Right仍+3）；warm120ms仅淡入；退出120ms淡出/scale.98 | 内部独占属性，快速重开从当前值继续 |
| 内容 | string/number更新240ms进入y+.3em/blur4；120ms退出y-.3em/blur2；尺寸morph .42 | 字符串/数字专属文本层，自定义TipTemplate静态呈现 |
| Reduced | CSS为120ms纯淡化；页面文字写90ms | 采用冻结源码120ms，记录冲突；Off即时 |

没有已审查WPF截图基准。包装控件、屏幕定位、焦点与可访问性适配见[规格](../../components/Tooltip.md)。
