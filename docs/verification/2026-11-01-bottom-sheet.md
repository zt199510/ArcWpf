# BottomSheet 验证记录（2026-11-01）

来源冻结于 `tests/Baselines/References/2026-11-01-bottom-sheet/manifest.json`，Registry、CSS、Markdown、页面、许可及动效令牌均已取得，manifest 错误列表为空。此前网络沙箱阻断导致的“来源不可访问”结论已被本次只读获取取代。

WPF STA 显示窗口测得：800×600 DIP 宿主内，默认表面宽 576、高 552 DIP；最低 .45 档位的 Y 偏移为 282 DIP，遮罩强度 .78。抓手 PreviewKeyDown Up 切到最高档，偏移/遮罩成为 0/1；Click 返回最低档，档位事件触发。程序化拖拽释放可展开与关闭。兼容 `SnapHeight` 在 600 DIP 宿主内从 420 改到 300 DIP，宿主缩至 220 DIP 时表面被限制到 220 DIP。Full 打开启动位置驱动，切 Off 清理渲染订阅并立即落点；Drawer 基类测试仍通过。Gallery 的 Lisbon 行程主示例可构造，Controls/Gallery 构建无错误。

BottomSheet 与 Drawer 精确筛选 16/16 通过。以上是程序化窗口、路由键与几何证据；真实鼠标/触摸、长内容滚动、焦点进入遮挡内容、主题切换、多 DPI 和逐帧观感尚未做物理窗口观察。Web 的动态视口与 safe-area 在 WPF 无直接对应，保留为平台差异。
