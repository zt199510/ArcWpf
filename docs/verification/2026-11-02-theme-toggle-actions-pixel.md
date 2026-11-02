# ThemeSwitch / ToggleGroup / ActionSwap / UndoToast / ExpandableText 像素对照记录

日期：2026-11-02。按用户要求对照 `https://uiarc.dev/components/*` 重做外观和交互。

## 来源

- 视觉基准：Playwright + Chromium，视口 1280x1000、1x、亮色，每页独立浏览器上下文，截取线上控件并读取 DOM 几何和计算样式（证据等级：线上实测）。
- 实现依据：`tests/Baselines/References/2026-11-02-*` 的冻结来源；UndoToast 无 Registry，只用公开文档和公开页面的显示几何、CSS。
- 截图工具和对照图在会话临时目录与 `artifacts/2026-11-02-theme-toggle-actions-pixel/`（不入库）。

## 结果

| 层级 | 结论 |
|---|---|
| 构建 | `ArcWpf.slnx` 0 错误 |
| 相关测试 | 五个控件 40/40 通过（Wine） |
| 全量测试 | 未运行：用户要求只做当前任务相关检查 |
| 离屏渲染 | 亮色已与 Arc 截图比对；暗色只检查了本项目渲染，未与 Arc 暗色逐张比对（ThemeSwitch 除外） |
| 真实窗口 | 未运行：当前环境是 Linux + Wine，无真实 Windows 窗口、物理输入和 DPI 环境 |

尺寸（WPF / Arc）：

| 控件 | 尺寸 |
|---|---|
| ToggleGroup | 120x44、158x44 / 相同 |
| ThemeSwitch | 138x36 / 143.1x36（文字宽度） |
| ActionSwap 静止 | 98、140、127 / 101.1、147.8、131.3，高 36（文字宽度） |
| ExpandableText | 326x95、展开 330 / 325.6x94.8、展开 331.3 |
| UndoToast 卡片 | 521.6x540 / 相同；Toast 372x54 |

逐像素差异（允许 ±1px 平移，去掉 2px 边缘）：

| 截图 | 有差异像素 | 差值超过 32/255 |
|---|---|---|
| theme-switch | 22.2% | 13.6% |
| theme-switch-dark | 23.3% | 14.5% |
| toggle-group-0 | 8.1% | 0.2% |
| toggle-group-0-joined | 8.2% | 0.2% |
| toggle-group-1-right | 5.7% | 0.0% |
| action-swap-rest-0 | 20.1% | 11.5% |
| action-swap-rest-1 | 21.0% | 13.2% |
| action-swap-settled-1 | 18.6% | 11.2% |
| expandable-text-rest | 24.0% | 12.9% |
| expandable-text-open | 27.8% | 19.3% |
| undo-rest | 12.6% | 8.7% |
| undo-two | 24.7% | 6.2% |
| undo-restored | 36.3% | 9.0% |

**这不是像素完全一致。** 差异主要来自文字：图标类的 ToggleGroup 超阈值差异接近 0，文字越多差异越大。ActionSwap 文字墨迹的上下边界与 Arc 基本一致（Add to library 顶部差 1px），横向因字宽不同而错开。UndoToast 还包含自绘头像；undo-two 里 Arc 截图有鼠标悬停行，undo-restored 的闪烁随截图时刻变化，这两张不能只看百分比。

对照环境的字体是临时替身：Liberation 改名为 Segoe UI，只存在于测试前缀内。真实 Windows 上文字宽度会再变化。

## 已知差异

- **字体：** Arc 用 Inter，WPF 用 Segoe UI。用户已决定不内置 Inter，文字宽度、字重观感和换行差异予以保留。
- **字距：** WPF 没有 `-0.01em` 字距。
- **演示素材：** 商品图是占位形状，头像是自绘渐变，未使用 Arc 图片。
- **UndoToast 环：** 起点和方向证据较弱，按顶部、顺时针处理。

## 对照中修复的缺陷

- ExpandableText 末行淡出几乎看不见：相对坐标遮罩被裁剪掉的子元素高度摊薄，改为绝对坐标；溢出状态变化后也刷新遮罩。
- UndoToast 行距 60.8 比 60.2 多出 0.6，第 6 行累计约 3px：关闭行容器的布局取整。
- UndoToast 恢复闪烁按公开 CSS 重写（accent 11%，内缩 1，.12s 延迟，1.8s，18% 峰值）。
- 暗色下 Toast 的 Undo 是黑字：`Button` 不继承前景色，显式绑定。
- ActionSwap 文字偏高约 2px；折叠裁切放到边框内。

## 待验收

Windows 真实窗口的观感、物理鼠标和键盘焦点、多 DPI、读屏、Reduced/Off 动效、逐帧动效。
