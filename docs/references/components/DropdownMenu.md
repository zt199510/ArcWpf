# DropdownMenu 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/dropdown-menu（MIT，免费 Registry）。
- 批次：2026-11-03，项目采集标识，非官方版本。快照在 `tests/Baselines/References/2026-11-03-dropdown-menu/`（Registry 源码、页面文本、许可、manifest 哈希）。
- 用户图片：无。当前页面是否仍同版：未知，Arc 未公开版本号。
- 离屏截图未入库；WPF 对照结果见 [验证记录](../../verification/2026-11-03-batch5-replication.md)。

## 可直接对照的主示例

| 项目 | 参考值 | 证据与位置 | 当前实现/待核对 |
|---|---|---|---|
| 文案、内容组合 | 触发器 Project actions + ellipsis；Edit project、Download brief、分隔线、Archive | 源码确认+DOM | 一致 |
| 初始值与状态 | 关闭；指针打开无高亮，键盘打开选中首项 | 源码确认 | 一致；键盘路径未在真实窗口运行 |
| 控件本体尺寸/圆角 | 触发器高 36；菜单 192×129 | DOM 几何 | 菜单一致；触发器宽 163.8 对 170，替身字体所致 |
| 内部对齐锚点 | 菜单相对触发器 x=触发器宽−192（Arc −22），y=41.5 | DOM 几何 | x 按右缘对齐一致；y 为 42（取整差 0.5） |
| 展示容器宽度/背景 | 预览画布 .978 | 计算样式 | Gallery 通用画布 |

## 状态与动效索引

弹簧与时长以快照内源码和 `motion.txt` 为准，状态矩阵见[规格](../../components/DropdownMenu.md)。中间帧未逐帧比对。

## WPF差异和回归入口

派生自已复刻的 `Menu`，复用其 Popup、键盘和焦点逻辑。Popup 外围留阴影空间 40/6/40/56，由 `PlacePopup` 覆盖扣除；该留白不设背景，点在其上算外部点击。

回归：`tests/ArcWpf.Tests/DropdownMenuTests.cs`。
