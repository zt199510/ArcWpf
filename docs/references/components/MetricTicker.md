# MetricTicker 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/metric-ticker（Arc Pro，Registry 返回 404，无公开版本号），2026-11-03 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。
- 参考文件不入库，放在 `work/refs/batch9/metric-ticker/`。SHA-256：`markdown.txt` 1ae38d89ef1d34906094538939f02b5f9fadd644af82ae93e2cfb499048cce68；`page.html` 5f294423eb3a1c60264953a58414c81a437c877ff5a7bb478fad11d58bba812d。
- 批次：2026-11-03 第九批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 视口 | 高 69.2，圆角 18，边框 1 | 运行观察 | 一致 |
| 主示例 | 521.6 宽，Product health，七项指标 | 运行观察 | 指标与格式一致；Gallery 数据为本项目模拟 |
| 数字滚动 | 个位先落定，高位依次晚 25ms；9→0 环绕 | 文档描述与运行观察 | WPF 适配：`OdometerText` |
| 增量色 | 静止为 70% 语义色与 muted 混合，闪烁时 14% 底色 | 运行观察 | 一致 |
| 面板 | 248 宽，距视口 9，走势 56 高 | 运行观察 | 一致 |
| 漂移 | 默认 32 DIP/s，悬停约 0.5s 内停稳 | 文档描述与运行观察 | 时长为近似，待核对 |

## WPF差异和回归入口

差异见[组件规格](../../components/MetricTicker.md)。回归：`tests/ArcWpf.Tests/MetricTickerTests.cs`，Gallery 页 `MetricTickerPage`。
