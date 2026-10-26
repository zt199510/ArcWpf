# ExpandableCard 验证记录（2026-10-26）

## 已完成

- Arc ExpandableCard Registry、Markdown、页面 HTML、共享动效令牌和许可页冻结到 `tests/Baselines/References/2026-10-26-expandable-card`，来源和 SHA-256 见 `manifest.json` / `manifest-hashes.json`。
- 实现原生 `ExpandableCard`，包括标题/描述/正文、默认展开、双向状态、宽度上限、自动化和 Full/Reduced/Off 动效。
- Gallery 加入主样本和扩展示例，Consumer 加入独立引用。
- 新增 ExpandableCard 程序化测试，覆盖状态、内容挂载、布局、焦点样式、自动化、动效清理和示例构造。

## 验证结果

- `dotnet build tests/ArcWpf.Tests/ArcWpf.Tests.csproj -c Debug -p:OutDir=artifacts/expandable-card-out/`：通过，0错误；保留既有分析器/未使用事件警告。
- ExpandableCard 专项：7项通过。
- 全量回归：193项通过。
- 实际窗口交互、截图、DPI、读屏、触摸和性能：当前阶段明确暂不做，保持未测；没有启动可见窗口。

## 图标位置修正

- 根据用户截图，将 Chevron 从 StackPanel 负边距定位改为标题 Grid 行内右侧垂直居中，避免图标出现在标题上方。
- ExpandableCard 专项测试复跑：7项通过。
