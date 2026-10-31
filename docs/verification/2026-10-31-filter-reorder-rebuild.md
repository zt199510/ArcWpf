# FilterToolbar / ReorderableList 源码复刻验证（2026-10-31）

## 来源

d8bc0f0/7e8103f 为无源 fallback。本次 Registry 可访问，已冻结至 `tests/Baselines/References/2026-10-31-{filter-toolbar,reorderable-list}/`（manifest 含 SHA-256）；demo 数据取自源站页面 chunk（`work/live/`，未版本化）。

## 实际结果

- Controls、Gallery、Tests 以 `-p:EnableWindowsTargeting=true` 在 Linux 构建：0 错误。

## 未运行/待验证

- FilterToolbarTests(3)、ReorderableListTests(3)：需 Windows 运行。
- 实际窗口：菜单形变与 Popup 对位、拖拽命中、焦点、明暗主题、Reduced/Off、读屏、多 DPI 均未观察，不得视为视觉通过。

## 已知差异

见两份规格范围矩阵。
