# Avatar 规格

状态：本次实现完成、待人工验收；来源冻结批次2026-10-27-avatar，无官方版本号。Arc免费MIT参考/改编，[参考卡](../references/components/Avatar.md)。只做单头像，头像组/账户菜单不属于范围。实际界面由用户人工测试，不使用computer-use，只运行Avatar专项。

## 公开契约与布局

Control基类；DisplayName(string，空字符串默认)映射原版name，避免占用WPF Name；Source(ImageSource/null)使用WPF原生图片与URI类型转换；Size=Medium，AvatarSize含Small/Medium/Large/ExtraLarge；Status=None，AvatarStatus含None/Online/Offline。单向DP、不接管DataContext。只读Initials、IsImageLoading、HasImageError。默认不可聚焦/无Tab停靠/无FocusVisualStyle，不增加hover、点击或禁用装饰。UIA Image角色，名称为DisplayName加online/offline，显式AutomationProperties.Name优先，内部图片/文字/点不单独读。

直径28/36/48/88，字号10/12/14/22，边框1、背景SurfaceMuted、Foreground、Medium字重。根Grid保持溢出可见，头像面与照片独立圆形裁剪；Image UniformToFill居中覆盖，状态点不被头像裁掉。状态点外径8/10/12/14，surface描边1.5/2/2/2，额外1 DIP边框色70%透明外环；右下角0/0/0/4。尺寸约束整个控件；显式非正方形Width/Height时面居中取最短边为圆（WPF适配），外部太小可裁剪。默认尺寸不随父容器拉伸。

首字母按前两个空白分隔词，采用Unicode文本元素与Invariant大写，避免JS part[0]截断代理对（有意修正）；单个中文词取第一个字，空白名字显示空。Source缺省即时首字母；加载中只显示背景；下载/解码失败恢复首字母。已解码图片立即显示；真正下载完成才淡入。Source切换解绑旧图片事件、忽略过期回调；失败不清除Source绑定。卸载解绑，重载重新核对当前Source；外部BitmapImage下载由调用方/WPF拥有，不强行取消共享下载。

## 动效完整度（本次必做）

| 项目 | 源码参数 | 实现/验收 |
|---|---|---|
| 图片加载完成 | opacity0→1、blur4→0，240ms enter | 内部Image时钟；缓存/已解码无动画 |
| 失败回退 | 首字母opacity0→1，240ms enter | 内部TextBlock；无Source直接显示 |
| 状态新增/替换 | opacity0/scale.6→1，snappy .26、bounce0 | OwnedMotion采样近似；首次挂载不动画 |
| 状态退出 | opacity→0/scale→.6，160ms | 退出点保留至完成，不占布局；最多一个旧点 |
| Reduced/Off | 图片与回退无动画，点立即替换 | 直接终态、清理时钟 |
| 打断/隐藏/卸载/换模板 | 清理旧回调/时钟 | 状态从当前有效值反向，重入最多一个退场点；不保留物理速度 |

模板部件PART_Layout（整体圆形布局宿主）、PART_Face、PART_Image、PART_Initials、PART_Status、PART_StatusExit。缺失部件安全降级。颜色用动态语义资源，新增AvatarOnline与AvatarStatusOutline不改变其他控件令牌。

## 完整度矩阵

| 交付项 | 范围 | 状态 | 验证 |
|---|---|---|---|
| Source/名字/尺寸/状态绑定、UIA | 必做 | 已实现 | Avatar专项 |
| 四尺寸、照片裁剪、首字母、点与外环 | 必做 | 已实现 | 布局/离屏 |
| 异步成功/失败/过期与生命周期 | 必做 | 已实现 | 本地HTTP可控响应专项 |
| 图片/回退/点动效与模式 | 必做 | 已实现 | STA真实时钟 |
| Gallery主样本与扩展、主题 | 必做 | 已实现 | 离屏，人工待验收 |
| 实际鼠标/读屏/多DPI/性能 | 用户人工验收 | 未运行 | 不以离屏替代 |

允许差异：WPF图片管线没有Web lazy/sizes/ArcProvider；调用方可用BitmapImage.DecodePixelWidth控制解码尺寸。Web字形与系统字体不同，WPF模糊/采样弹簧非逐帧等价。尚未建立经用户批准的回归基准。

## 实施与证据

实现：src/ArcWpf.Controls/Avatar，独立主题已合并；Gallery反馈组新增Avatar入口，保留此前Badge及导航改动。专项9/9通过，含本地TCP/HTTP响应触发真实BitmapImage下载完成/解码失败、过期回调、真实卸载重载、STA动画终态、四尺寸点位与离屏轮廓。详见[验证记录](../verification/2026-10-27-avatar.md)。

[WPF BitmapSource官方API](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.imaging.bitmapsource?view=windowsdesktop-10.0)确认IsDownloading及下载/解码事件。同步URI构造错误发生在ImageSource创建前，由调用方处理；Gallery提供地址错误提示。Source是调用方拥有的图像对象，不做额外下载缓存或自动播报。

主题资源：AvatarOnline使用冻结success（明色OKLCH .49/.19/150，暗色.78/.18/150）；AvatarStatusOutline使用Border色70% alpha。其余复用Surface/SurfaceMuted/Foreground/TextMuted/Border；仅新增两个键，不修改既有控件颜色。消费者可覆盖上述资源。状态点外环负Margin是CSS spread1的几何映射，已测外环相对点起点-1、总宽+2 DIP；不是补偿文字对齐。

未验收：真实窗口滚动/输入/Tab、图片与状态动态观感、100%/150%/200%DPI、读屏、高对比度及大规模性能。没有原版照片许可副本，Gallery用原创几何人像代替；该素材不是Arc原站视觉基准。没有本次必做实现缺项，尚未宣称完整视觉还原。未提交/推送。