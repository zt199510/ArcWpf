# 数据展示控件像素对照记录

范围：CodeBlock、JSONViewer、SortableDataTable、TreeTable。按用户要求对照 `https://uiarc.dev/components/*` 重做外观和交互。

## 来源

- 视觉基准：Playwright 1.49.1 + Chromium，视口 1280x1000、1x、亮色，截取线上页面控件并读取 DOM 几何和计算样式（证据等级：线上实测）。
- 实现依据：`tests/Baselines/References/2026-11-02-*` 的冻结源码与 CSS。
- 线上令牌与项目较早冻结的基础令牌有差异，新增的 `Code*`、`Json*`、`Table*`、`Tree*`、`Status*` 令牌只供这些控件和 Gallery 演示使用，未改全局 Success/Warning 和 Badge。
- 演示数据取自线上页面；TreeTable 子行通过展开线上页面取得。JSONViewer 的 `events` 180 条按线上规律生成，非逐条一致。

## 结果

| 层级 | 结论 |
|---|---|
| 构建 | `ArcWpf.slnx` 0 错误 |
| 相关测试 | CodeBlock、JSONViewer、SortableDataTable、TreeTable、TreeView **37/37 通过**（Wine） |
| 全量测试 | 未运行：用户要求只做当前任务相关检查 |
| 离屏渲染 | 亮暗主题已渲染，亮色已与 Arc 截图比对 |
| 真实窗口 | 未运行：当前环境是 Linux + Wine，无真实 Windows 窗口、物理输入和 DPI 环境 |

外框尺寸（WPF / Arc 元素）：CodeBlock 522x119 / 521.6x119，JSONViewer 522x472 / 521.6x472，SortableDataTable 522x351 / 521.6x351，TreeTable 522x442 / 521.6x442。

逐像素差异（扣除截图的小数像素偏移后）：

| 控件 | 有差异像素 | 差值超过 32/255 |
|---|---|---|
| CodeBlock | 7.8% | 5.3% |
| JSONViewer | 5.6% | 3.6% |
| SortableDataTable | 7.5% | 5.3% |
| TreeTable | 5.0% | 3.1% |

**这不是像素完全一致。** 抽样列上的分隔线和填充色与 Arc 一致（SortableDataTable 的 x=300、x=482 两列 0 行差异，TreeTable 的 x=300 列 0 行差异，JSONViewer 的 x=482 列 0 行差异）；图标和文字起点的墨迹范围大多相差 0-1px。其余差异主要来自文字字形与抗锯齿，但未做分区统计，比例未单独量化。

对照环境的字体是临时替身：Liberation 改名为 Segoe UI 和 Consolas，只存在于测试前缀内。真实 Windows 上文字宽度会再变化。

## 已知差异

- **字体：** Arc 正文用 Inter，WPF 用 Segoe UI，因此文字宽度和换行不同（如 "Aurora redesign" 在 Arc 折两行）。用户已决定不内置 Inter，这部分差异予以接受。
- **字距与字重：** WPF 无 `-0.01em` 字距；Medium 字重取决于字体是否提供。
- **滚动条：** 改为悬停才显示且不占布局，以匹配无头 Chromium 的基准。Windows 版 Chrome 的 `scrollbar-width: thin` 会占位，与此处不同。
- **宽度取整：** `UseLayoutRounding` 把 521.6 取整为 522，右侧元素有不足 1px 的偏移。
- **搜索标记：** WPF 的 `Run.Background` 没有圆角和 1px 外扩。
- **未实现的动效：** 行退出、当前行高亮滑动、操作区淡入、排序 FLIP、排序箭头翻转、计数换字模糊、复制对勾描线。
- **未实现的响应式：** 来源在 420/620 px 以下的窄屏布局，以及 TreeTable 的 `hideBelow`。
- **未实现的 API：** JSONViewer 的 `onCopy`/`onSelect` 事件。
- **行号：** 需求列表提到行号，但 Arc 原版没有，因此 `ShowLineNumbers` 默认关闭。

## 顺带修复

- TreeView 在获得焦点时重建行，会销毁刚获得焦点的行；现在只有当前行变化才重建。
- TreeView 的预览鼠标处理会吞掉行内按钮的点击；行内按钮现在保留自己的点击。

## 产物

对照截图在 `artifacts/2026-11-02-data-controls-pixel/{arc,wpf}/`（已被 git 忽略）。
