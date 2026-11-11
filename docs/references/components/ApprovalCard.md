# ApprovalCard 参考卡

## 来源与固定范围

- 页面：https://uiarc.dev/components/blocks/agent-approvals（Arc Pro，无公开版本号），2026-11-04 采集。
- 只使用公开文档和公开页面显示的几何与计算样式；未获取 Pro 源码、私有 Registry 包或页面实现脚本。参考文件在 `work/refs/batch12/b-agent-approvals/` 不入库。
- SHA-256：`markdown.txt` f6a2328e4a83bd3fb2a5427abfdf933929e221af92a1b3a4ea959e123c39ab56；`page.html` a675278770030791b938cd5550cf712dabd46cd17df22acb119836de414b9be9。
- 批次：2026-11-04 第十二批。

## 可直接对照的主示例

| 项目 | 参考值 | 证据 | 当前实现/待核对 |
|---|---|---|---|
| 卡片 | 圆角 24，1px 弱边框，内容距边 20 | 运行观察 | 一致 |
| 标题 | 22/500，代码 20.24 等宽 | 运行观察 | 一致 |
| 参数框 | 圆角 16，内边距 6，行高 32，键列 96，值 13 等宽 | 运行观察 | 一致 |
| 决策行 | 高 36；Approve 带 38×18 的 Enter 键帽 | 运行观察 | 标准宽度一致；窄宽隐藏键帽并收紧按钮内边距，属 WPF 适配 |
| 菜单 | 234 宽，内边距 6，行高 36，右对齐触发器，间距 6 | 运行观察 | 宽一致；高 86（Arc 84，含边框差异） |
| 风险 | 盾牌图标加文字，低/中/高 对应 success/warning/danger | 文档描述 | 一致 |

## WPF差异和回归入口

差异见[组件规格](../../components/ApprovalCard.md)。回归：`tests/ArcWpf.Tests/ApprovalCardTests.cs`，Gallery 页 `ApprovalCardPage`。
