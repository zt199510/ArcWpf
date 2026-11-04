# ModelPicker 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/blocks/model-picker（Arc Pro，无公开版本号），2026-11-04 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。
- 参考文件不入库，放在 `work/refs/batch11/block-model-picker/`。SHA-256：`markdown.txt` 82807837f9d076426b31fcf89d0f7705ffa1499cb0de8b35c03641bf898e6ead；`page.html` 03d56328d359a7ed5fa1814bb68d32e907d8639af427eac317e1811d162179b0。
- 批次：2026-11-04 第十一批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 胶囊 | 高 40，全圆角 | 运行观察 | 一致 |
| 菜单 | 304 宽，圆角 26，行高 54 | 运行观察 | 一致 |
| 强度文案 | Quickest answers 等五档 | 页面文本确认 | 一致 |
| 总览 | 四格，小于 400 为 2×2 | 运行观察 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/ModelPicker.md)。回归：`tests/ArcWpf.Tests/ModelPickerTests.cs`，Gallery 页 `ModelPickerPage`。
