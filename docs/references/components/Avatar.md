# Avatar 参考卡

来源：https://uiarc.dev/components/avatar 、 /components/avatar/markdown 、 /r/avatar.json 。冻结目录tests/Baselines/References/2026-10-27-avatar，manifest.json记录URL/日期/SHA-256；包含CSS/TSX/ArcProvider/foundation/motion/license。免费MIT，Copyright (c) 2026 Elia Kuratli。项目采集批次不是官方版本。未观察浏览器，未使用TKCode.Arc。

主示例取Markdown Owner：Maya Chen、lg48、online、src=/people/maya.jpg。照片文件本身未提供许可/固定副本，Gallery使用项目自绘人像示意图替代并标注；没有声称是原站照片。初始姓名/尺寸/状态来自文档，容器居中留白为Gallery选择。

结构：inline-grid根span（role img/name加status）→图片或首字母→绝对定位状态点；根overflow visible，图片自己圆角。源码confirmed：28/36/48/88直径、10/12/14/22字、点8/10/12/14，xl偏移4，其余0。头像边框1，点边框sm1.5其余2，阴影式外环1（border70% alpha）。颜色来自foundation success、text-muted、surface、surface-muted、border；不添加focus/hover视觉。

动效索引：avatar.tsx useLayoutEffect检测complete，data-loading图片opacity0/blur4；CSS240ms enter；失败首字母240ms淡入；AnimatePresence initial=false，status key变化snappy .26，退出160ms；Reduced所有上述动画关闭。缓存图片不淡入。源码与部署同版未知，来源证据不能证明WPF渲染通过。

WPF对应：Control+Grid/裁剪Image/首字母TextBlock/两个状态Border；ImageSource原生下载与解码事件，Unicode首字母修正；规格见[Avatar](../../components/Avatar.md)。字体、异步图片管线、模糊及弹簧适配需人工核对，输出只为离屏检查。
