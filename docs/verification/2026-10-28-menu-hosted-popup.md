# Menu 弹出层修正：ButtonGroup / SplitButton 呈现（2026-10-28）

## 现象

用户反馈：Menu 控件导致 ButtonGroup 与 SplitButton 的菜单呈现异常。两者都通过重写 Menu 模板复用 Menu 的 Popup、条目样式和高亮逻辑，问题均在共享部分。

## 根因（对照冻结源码 dropdown-menu / button-group / split-button CSS 与模板逐项核对）

| 问题 | 根因 | 影响 |
|---|---|---|
| 悬停/禁用条目出现系统蓝底、灰底 | 条目 DataTemplate 内嵌原生 WPF `Button`，未设模板，Aero 默认 chrome 在 IsMouseOver/IsPressed/禁用时自行绘制背景 | 三处菜单；上一轮误判为 ListBox 选中模板，归一化容器不能消除 |
| 共享高亮偏离当前行 | `PART_Highlight` 为默认 Stretch 对齐，设置 Height 后被重新居中，再叠加 TranslateY | 多于一项时高亮不在行上 |
| 分隔线穿过条目文字 | 分隔线与条目在同一 Grid 单元重叠 | Menu 页 Delete 等 SeparatorBefore 项 |
| 无图标条目文字缩进 24 | 图标列固定 24 宽，无图标仍占位；来源为 17 宽 + 10 间距、无图标不占位 | SplitButton 菜单 |
| 高亮与行横向差 1 | ListBox 默认模板 Border Padding=1 | 三处菜单 |
| 禁用条目进入动画后变为不透明 | 进入动画把容器 Opacity 动画到 1，覆盖样式的 .45 | 禁用项 |
| SplitButton 菜单位置 | 模板 `Placement="Bottom"`，Menu 的自定义回调不生效，菜单左对齐 36 宽 chevron 向右伸出；来源 `align="end" sideOffset={4}` | SplitButton |
| SplitButton chevron 旋转偏移 | 24 坐标 Path 放在 16×16 框内未缩放，几何中心不在框中心，旋转时摆动 | SplitButton |
| Home/End 方向相反 | `NextEnabled(-1, 1)` 取模后从最后一项开始 | 键盘 |

## 修正

- MenuTheme：条目改为纯 Grid（无内嵌 Button），容器模板为“分隔线 + `PART_Row`”，`IsEnabled` 绑定数据项；新增共享 `ArcWpf.Menu.ItemsHostStyle`（无 ListBox chrome、StackPanel 面板），三处模板统一使用。
- Menu.cs：高亮固定 Left/Top 对齐，按 `PART_Row` 的布局位置（排除进入动画位移）设置 X/Y/宽/高；危险项高亮使用新令牌 `MenuDangerHighlight`（danger 8% 混 surface，sRGB 近似 oklab）；移除 `NormalizeItemContainers` 绕行；禁用项进入动画终值 .45；Home/End 修正，PageUp/PageDown 同 Home/End；新增内部 `SideOffset`，纵向间距为宿主边框 + SideOffset。
- SplitButton：chevron 改为 16px Viewbox + 24 Canvas（与 ButtonGroup 一致）；Popup 改为 Custom，`AlignOffset=1`、`SideOffset=4`；菜单最小宽度按来源 12rem = 192。
- ButtonGroup：模板改用共享 ItemsHostStyle 与高亮对齐；对齐/偏移不变（end，sideOffset 6，alignOffset −1），纵向间距现计入 1px 组边框。

## 验证

- 构建：`dotnet build ArcWpf.slnx -p:EnableWindowsTargeting=true` 成功，0 错误。
- 程序化测试（Wine 9 + Xvfb，自包含 win-x64 反射运行器调用 MSTest 方法，非 VSTest、非 Windows 原生）：MenuTests、SplitButtonTests、ButtonGroupTests 共 20/20 通过，含新增 `HighlightCoversSelectedRowAndLabelsStartAtItemPadding`（屏幕坐标比较高亮与行、无图标标签起点 11）。新测试首轮暴露高亮在进入动画期间取值偏 2px，已改为布局位置后通过；未在旧实现上单独运行该测试（旧模板无 `PART_Row`）。
- 实际窗口（同一 Wine 环境，Gallery 截图，仅本地留存，SHA-256 前 16 位）：
  - SplitButton 页：菜单右缘与拆分按钮外缘对齐、下方 4；标签无缩进；chevron 原地旋转 180°；悬停高亮与“导出”行重合（e258769e136601e1）。
  - ButtonGroup 页：菜单与组外缘对齐；图标 17+10 排列；Delete 危险色高亮（d46c18d9fda34d53）。
  - Menu 页：分隔线位于 Delete 上方且不进入高亮；Archive 禁用为半透明（11c6d9d037a505b3、d8f423bc0e5d0300）。
  - Wine 下 Popup 圆角外黑角为分层窗口透明不受支持，非控件问题。
- 待 Windows 原生验收：悬停动画手感、明暗主题、DPI 缩放下的 Popup 偏移、读屏。

## 第二轮：SplitButton 状态与 Menu 退场（同日）

对照 split-button.module.css 与 dropdown-menu/button-group `.menu[data-state="closed"]` 补齐上一轮列为剩余差异的项目：

| 项 | 来源 | WPF 实现 |
|---|---|---|
| 触发区宽度 | `.trigger { width: 38px }` | `PART_Trigger` Width 38，主区域 Padding 16（`--space-4`），高度为 ControlHeight.Medium |
| 分隔线 | `.trigger` 整高 border-left，background 18% 混入 foreground | 整高 1px，新令牌 `SplitButtonDivider` |
| 悬停/按压/打开底色 | hover foreground 88%，active 与 `[data-state=open]` 80%，`duration-fast` 过渡 | 新令牌 `SplitButtonHover`/`SplitButtonPressed`（中性色 L 线性混合，同 ButtonGroup 做法）；SplitButton 以 OwnedMotion 颜色过渡驱动两侧 Fill，静止为同色相透明；Fill 圆角为控件圆角 −1 |
| 主动作按压 | 整个胶囊 `scale(.97)`，`duration-instant` 进入、`spring` 回弹；触发区只变色 | 主按钮改用专用模板（保留标签过渡部件，去掉自身缩放与阴影），PART_Surface 整体缩放；Reduced/Off 无缩放 |
| 菜单退场 | 130ms 淡出 + scale .985，`pointer-events: none`，中途重开从当前值反向 | Menu 在 IsOpen=false 时保留 Popup 执行退场，完成后关闭；退场中不可命中且不再调用条目；重开反向回到静止；Reduced 只淡出，Off 立即关闭；动效级别切换或模板替换时仍会关闭 Popup |
| 宿主状态同步 | chevron 在 data-state 变为 closed 时立即回转 | Menu 新增内部 `OpenStateChanged`，ButtonGroup/SplitButton 用它取代 Opened/Closed，回转与底色不等退场结束 |

验证：构建 0 错误；Wine 运行器 Menu/SplitButton/ButtonGroup/Button 39/39 通过（新增 `CloseFadesOutBeforeThePopupLeavesAndReopenReverses`、`TriggerAndMainFillFollowHoverPressAndOpenState`）。全量 366 项中 352 通过、14 失败；同一运行器在未修改的 main 上为 349 通过、同样 14 项失败（Gallery 渲染截图类 NullReference、Avatar 网络下载、导航计数 34/42、Slider/PresenceStack 精度），与本次修改无关。Gallery 截图确认静止、主区悬停、触发区悬停、打开（触发区按压色）与关闭后的状态；退场中途帧在 Wine 下因分层窗口透明不受支持显示为黑色，淡出外观须在 Windows 验收。悬停触发区超过 ToolTip 延迟时 Gallery 在 Wine 下崩溃（X RENDER BadMatch，已知 Wine 问题）。

## 剩余差异

- 菜单进入/退场未复刻 `--menu-y` 的 5px/2.5px 位移，仅有淡入淡出与缩放。
- 未实现 Arc 的 `secondary` 变体与主标签逐字 morph（来源 MorphText），SplitButton 公开 API 未包含这两项。
- 颜色混合为 sRGB/中性 L 近似，不是 oklab color-mix 的精确值。
