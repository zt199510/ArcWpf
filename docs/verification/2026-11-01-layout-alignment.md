# 新增控件布局对照记录（2026-11-01）

范围：BottomSheet、Card/MetricCard、HoverCard、ResizablePanels、Stepper、UsageMeter，以及 MoneyInput。每个控件对照自己的 Arc 页面与冻结源码（用户确认）。基线提交 `0f5795f`，改动未提交。

来源：`tests/Baselines/References/2026-11-01-*`、`2026-10-31-money-input` 的 Registry CSS/TSX 与 `page.html`；页面 `page.html` 引用的站点 CSS 于 2026-11-01 读取，未持久化。

令牌注意：站点 CSS 中 `--text-3xl` 有两个值，1.875rem 在 `@layer theme`（Tailwind 默认），2.25rem 在不分层的 `:root`。不分层规则优先，实际为 36px。

## 修改与依据

| 控件 | 层级 | 修改前 → 修改后 | 证据 |
|---|---|---|---|
| Stepper | 内部 | 标记占位 36 → 28（外环外溢 4）；圆点与文字左对齐 | 源码 `--marker`/`--halo` |
| Stepper | 内部 | 等宽列 → `repeat(n-1,1fr) auto`，末步贴右（`StepperColumnsPanel`） | stepper.tsx:153 |
| Stepper | 内部 | 连接线不对称 → 左 36、右 8、y 13；纵向 13,34,0,6；条目下距 20/紧凑 24 | 源码 |
| Stepper | 外形 | 待进行描边 Border → BorderStrong；当前/错误 1.5 | 源码 |
| UsageMeter | 内部 | 金额 28 → 36；区块间距 12/16/10 → 16；图例按 auto-fit minmax(108,1fr)、gap 2、外扩 8 | 源码 |
| UsageMeter | 外形 | 图例按钮落回 WPF 默认外观 → Arc 样式，圆角 12 | 渲染观察；原因见下 |
| UsageMeter | 内部 | “12.4 GB GB of 15.0 GB used” → “12.4 GB of 15 GB used” | usage-meter.tsx:190、287 |
| Card | 外形 | 静止有阴影 → 无；媒体区方角盖住圆角边框 → 圆角裁剪 | card.module.css |
| MetricCard | 外形 | 圆角 26 → 34；数值 30 → 36 | metric-card.module.css、animated-counter CSS |
| MoneyInput | 内部 | 货币菜单右缘差 9 → 对齐外壳内缘 | money-input.module.css `.menu` |
| Gallery（7 页） | 展示容器 | 左对齐裸控件 → 居中画布；主示例改用各页文案、初始值与尺寸 | page.html、`docs_canvas` |

图例外观根因：样式是 Generic.xaml 中的字符串键，代码 `TryFindResource` 运行时查不到（已记录于 replication-workflow.md 的跨主题文件复用风险）。现放在 `PART_Legend.Resources`，从该面板查找。

## 验证结果

运行环境：Linux + Wine 9.0 + Xvfb，Windows 版 SDK 10.0.402，96 DPI。字体为 Liberation 改名的测试字体（非 Segoe UI），所以文字宽度只是近似，中文在此环境显示为方块。

构建标识（Debug，sha256 前 16 位）：`ArcWpf.Controls.dll` 140dbb91754bb631，`ArcWpf.Gallery.dll` 512ee3583c478009。

- 构建：`dotnet build ArcWpf.slnx -p:EnableWindowsTargeting=true`，0 错误。
- 专项：Stepper、UsageMeter、Card、MoneyInput、BottomSheet、HoverCard、ResizablePanels 共 100 项，99 通过，1 失败。
- 失败项 `MoneyInputTests.CurrencyButtonIsReadyAfterTemplate`：未修改的 `0f5795f` 上同样失败（`ApplyTemplate()` 返回 false），非本次引入，未修改；尚未在真实 Windows 上确认是否仅 Wine 环境失败。
- 新增回归：`HorizontalGeometryFollowsTheSourceColumns`、`LegendKeepsArcStyleAndReadoutShowsOneUnit`、`SurfacesFollowTheirOwnSourceTokens`。未对旧实现单独运行，只能证明新实现通过。
- 离屏渲染：7 页与 Stepper 纵向模式均已生成截图并人工查看结构。截图在临时目录，未归档。
- 未解释现象：MoneyInput 页在修复 Stepper 页之前的两轮渲染中报过异常，之后连续多轮未复现，根因未确认。

## 待验证与剩余差异

未运行：真实 Windows 窗口。字体、DPI、动效、弹出层实际位置（含货币菜单修正）、HoverCard 悬停弹出、BottomSheet 打开后的画面都待用户复验。全量测试未运行。

- 字体：Arc 用 Inter/Geist，WPF 用 Segoe UI；金额的 `-0.03em` 字距未实现。
- MoneyInput：工具栏窄宽度下不换行（460 宽主示例不受影响）。
- ResizablePanels：手柄列 6 DIP、中线 1，Arc 为 1px 手柄加外扩命中区，相邻面板背景与中线间有约 2.5 DIP 空隙。
- MetricCard：紧凑模式按控件宽度 <380 判断，Arc 按视口宽度；Gallery 用 380 宽避开。
- HoverCard：胶囊打开态的 8% 前景混合背景未实现，只换了描边。

Gallery 中无来源、属本页取值的内容（已在代码注释说明）：UsageMeter “Add files” 每次 +1.4 GB；ResizablePanels 宽度文字 “N px”；Stepper 非 Workspace 步的面板内容；HoverCard 弹层除姓名外的资料沿用文档 Usage 示例；Card 页 SSR 为懒加载占位，版面未核对，仅核对了模板与 Usage 文案。
