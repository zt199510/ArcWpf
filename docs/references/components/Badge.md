# Badge 参考卡

来源：https://uiarc.dev/components/badge 、 /components/badge/markdown 、 /r/badge.json 、 /r/arc-foundation.json 、 /r/arc-motion-tokens.json 、 /license 。冻结目录 `tests/Baselines/References/2026-10-27-badge`，文件哈希见manifest.json。采集日期2026-10-27，项目批次不是官方版本。无用户截图/运行观察，当前部署页面是否与Registry完全同版未确认；TKCode.Arc未用于此组件。

| 项目 | 已确认事实/证据 | WPF映射 |
|---|---|---|
| 主示例 | Markdown Usage DeployStatus：Live/success/Check12或Draft/neutral/无图标 | Gallery以Draft起始，可切换Live；文档没有指定初始live布尔值，Draft为项目选择 |
| 页面容器 | 未运行浏览器；不推断预览宽度与交互 | 主示例居中区为Gallery展示选择 |
| 外壳 | badge.module.css：min-height26、border1、padding0 10、pill；sm22、0 8 | Border，实际半高圆角，默认不拉伸 |
| 内部 | body→content flex，gap5/sm4；icon auto，label/text无换行 | 自然测量宿主，独立图标/标签槽，退出层不参与测量 |
| 字体 | text-xs12/sm11、weight500、tracking-.01em、line-height1 | Segoe UI/中文回退，Medium字重；不复制Web字形和tracking |
| 颜色 | neutral secondary/muted/border；success/info/warning/danger CSS各自混合 | Badge专属语义资源，OKLab静态混合后裁到sRGB；见主题源码 |
| 状态 | span无内建键盘行为；fine-pointer hover背景10%/边框32% | 鼠标悬停，默认不可聚焦，不添加黑框 |

动效源码：textIn/textOut/iconIn/shown/fadeOnly，ResizeObserver只对text/glyphKey变化120ms内宽度变化弹簧；spring.morph .42、snappy .26，blur soft4/subtle2，duration standard.24/fast.16/instant.12。CSS背景/边框/文字色过渡单独实现。图标首次从无到有的内层AnimatePresence initial=false不强制弹入；从有到无父槽直接移除。详情及适配差异见[规格](../../components/Badge.md)。

静态HTML、源码只证明结构和参数，不能证明实际浏览器或WPF视觉。WPF自然文本高度、抗锯齿、弹簧求解器及颜色补间保留平台差异；用户实际界面待验收。
