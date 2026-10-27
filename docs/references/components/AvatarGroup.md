# AvatarGroup 参考卡

来源：https://uiarc.dev/components/avatar-group 、 /components/avatar-group/markdown 、 /r/avatar-group.json 。冻结目录tests/Baselines/References/2026-10-27-avatar-group；manifest.json记录URL/获取时间/SHA-256，含Avatar依赖、foundation、motion和许可。项目批次无官方版本号，免费MIT Copyright (c) 2026 Elia Kuratli。未用TKCode.Arc，未观察浏览器，部署页面和Registry是否完全同版未知。

文档主样本：Maya Chen（online、src=/people/maya.jpg）、Leo Park、Sara Ruiz、Tom Hale、Ines Ma；max3、label Editors、默认md。WPF前三人+2，Maya用原创几何示意图代替；容器为Gallery选择，不是原站截图复刻。

源码结构group→slot（缩放/宽度/透明度）→lift（位移）→Avatar+tip，另有overflow lift及滚动count。CSS确认：D=28/36/48，group左padding6、lift margin-left=-6；2px Surface边框、1px Border外环。hover fan按(index-(count-1)/2)*4；目标lift上移2、两侧让位3；不影响组宽。tip上距8、左右padding8上下4、字号12、160ms/进入延迟60ms，颜色Foreground/Background反转。

动效包括TSX slot .42 morph、计数方向+.4em/- .4em/blur2、240/160ms；CSS lift240、ring160、tip160+60ms。Reduced槽即时、无所有lift位移、计数120ms淡化、tip即时但ring仍过渡。源码没有点击、选中、自动focus或Popover；不额外添加。

对应[规格](../../components/AvatarGroup.md)记录WPF引用身份与Unicode首字母、动画求解器、图片管线差异。静态来源不等同运行证据，实际界面仍待用户验收。
