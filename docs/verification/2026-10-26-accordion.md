# Accordion 验证记录（2026-10-26）

## 已完成

- Arc Accordion Registry、Markdown、页面 HTML、共享动效令牌和许可页已冻结到 `tests/Baselines/References/2026-10-26-accordion`，来源和 SHA-256 见 `manifest.json` / `manifest-hashes.json`。
- 实现原生 `Accordion` / `AccordionItem`，支持单开/全部收起、双向 `ExpandedIndex`、ItemsSource、Medium/Large、键盘导航、自动化展开收起、主题和 Full/Reduced/Off 动效。
- Gallery 加入 FAQ 主样本、绑定/禁用/长内容/嵌套扩展；独立 Consumer 加入 Accordion。
- 新增 Accordion 程序化测试，覆盖状态、绑定、布局、键盘候选、图标几何、自动化、动效时钟和示例构造。
- 根据用户截图修复标题点击后的焦点框：移除由 `IsKeyboardFocusWithin` 常驻控制的模板边框，改用 ToggleButton 的 `FocusVisualStyle`，仅保留键盘焦点视觉。

## 验证结果

- `dotnet build tests/ArcWpf.Tests/ArcWpf.Tests.csproj -c Debug -p:OutDir=artifacts/accordion-focus-fix-out/`：通过，0错误；保留既有项目的6项分析器/未使用事件警告。使用独立输出目录以避开运行中的 Gallery 文件锁。
- Accordion 专项：9项通过。
- 全量回归：186项通过。
- 实际窗口交互、截图、DPI、读屏、触摸和性能：用户当前阶段明确暂不做，保持未测；没有启动可见窗口。
