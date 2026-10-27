# PasswordInput 参考卡

来源：[Arc Password field](https://uiarc.dev/components/password-field/markdown)，免费MIT Registry、CSS、motion/foundation及许可页冻结于[2026-10-27项目采集批次](../../../tests/Baselines/References/2026-10-27-password-input/manifest.json)，非官方版本。WPF PasswordBox及peer参考dotnet/wpf v10.0.0。静态页面当前预览为lazy placeholder，未取得已渲染主例，采用文档Usage的Password标签、空初值；Gallery400宽为项目选择，未操作网站。

| 项目 | 来源确认 | WPF映射 |
|---|---|---|
| 结构 | label下8；shell最小44，border1/radius18，左padding12/右6；button30 | 标题、44高输入壳、说明/错误独立行；按钮与编辑器各自列 |
| 编辑 | 单个input切type，placeholder与原生输入属性 | PasswordBox+仅显式显示时TextBox；普通密码不使用Text DP |
| 颜色 | surface/foreground，borderStrong；hover/focus变foreground；invalid变danger；border160 | 动态语义资源+私有画刷；error为WPF扩展可见行 |
| 操作 | 按钮30、eye18(viewbox24)、stroke1.75；hover surfaceMuted/foreground；shown foreground | 原生ToggleButton，键盘Enter/Space和Toggle UIA，名称可翻译 |
| eye | shown时斜线2,2→22,22，pathLength240/opacity120，线下mask宽5 | 同一眼睛几何+长度/透明动画与几何遮罩，不换图标 |
| reveal | 切换后input opacity.35/blur2→1/0，240 enter | 只动画编辑视觉，不逐字重建；不保留旧明文退出层 |
| 按压 | scale.96，按下100ms，松开CSS spring | 私有ScaleTransform，按下100/松开采样.42；颜色160 |
| 消息 | top8、12字号；高度smooth.4/opacity160，退出高度.4/opacity120 | 裁剪消息槽+有限宽测量，错误/说明同流程 |
| 消息单词 | 按index:word身份，unchanged不动；新y.35em/blur4/opacity240；旧y-.35em/blur2/opacity180 | 稳定字词WrapPanel+退出Canvas；中文无空格按整段，与源码split空格一致 |

源码注释称“on each change”，实际CSS keyframe仅在data-reveal shown/hidden切换时重启；WPF按切换触发，不对每次键入播放。Reduced：eye/按钮/输入即时，消息可短淡化且高度即时；Off全部即时。WPF阴影/字体/采样曲线差异保留，未声称像素或速度完全一致。
