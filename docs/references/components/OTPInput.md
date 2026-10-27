# OTPInput参考卡

来源：[Arc OTP input](https://uiarc.dev/components/blocks/otp-input)，[Registry](https://uiarc.dev/r/otp-input.json)。免费MIT；冻结目录tests/Baselines/References/2026-10-27-otp-input，版本缺失，以manifest项目批次/哈希固定。源码确认，未运行原站，不伪造浏览器/字体环境。

主例来自文档Usage：Verification code，We sent a 6-digit code to your email.，六格初始空。Gallery使用400 DIP展示容器（WPF选择）。

布局源码确认：field grid→WPF外层StackPanel；label bottom8→标签Margin；inputs flex fit-content→最大292宽的Grid；slot42×48/gap8→等宽列及间隔；radius-control18；消息top8/font12→独立消息行。CSS viewport≤360时gap6/height44，本次桌面保留48高并按控件可用宽压缩格，不等同浏览器断点。字体使用项目Body，非浏览器像素承诺。

动效源码确认：空/hover/filled/invalid颜色160ms；glyph进入opacity0/y.35em/blur4→1/0/0，240ms easeEnter，多格变化index×stagger.item；退场opacity0/scale.9/blur2，duration.instant120ms；ring从外部进入就地淡入/scale.94→1、内部焦点移动spring.snappy、opacity160ms；错误新非空文案触发x[0,-6,5,-3,2,0]360ms。消息字词与高度使用与PasswordInput同源MotionText/FieldMessage结构。Reduced取消位移/blur/shake，保留短淡化；Off全部即时。

WPF适配：原生TextBox逐格输入；数字过滤只含ASCII；连续字符串中间删除左移；首空位续填；Completed为本项目扩展；无Web autocomplete与text模式。ring属于编辑器活动格指示，鼠标与键盘都会显示（非仅键盘FocusVisual）。消息复用现有内部PasswordMessage实现，不改PasswordInput行为。

资源映射：新增ArcWpf.Brush.Accent（light OKLCH .33、dark .8）、AccentSubtle（同色alpha .1/.13）、OTPErrorSurface（Danger 7%与Surface 93%的sRGB混合）；AccentStrong沿用现有资源。新增资源未改旧资源值，OTP专用错误底色不影响其他控件。WPF文字glyph与原生透明文本共存，选择底色隐藏；活动格ring明确位置。

实现位置：OTPInput.cs负责输入/绑定/生命周期；OTPDigit.cs持有私有画刷及glyph进退；OTPInput.Visuals.cs负责焦点ring、shake和消息；主题在OTPInput/Themes/OTPInputTheme.xaml。ring snappy使用OwnedMotion .26秒采样近似，不承诺跨目标速度连续；消息字词退场指定.12秒，复用PasswordMessage的默认.18秒保持原PasswordInput路径参数。消息的整体出现由字词和高度共同表现，不复制DOM两层重复transform；字体、抗锯齿及极窄格圆角有WPF渲染差异。
