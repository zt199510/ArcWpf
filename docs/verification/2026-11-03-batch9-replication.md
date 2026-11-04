# 2026-11-03 第九批复刻验证

状态：**待验收**。范围：ShareAccess、MetricTicker、RealtimeStream、TocProgress、WalletStack（均为 Arc Pro）。

## 环境

Linux + Wine + Xvfb，字体为 Liberation 改名替身（字宽、字重、纵向 1px 差异属环境差异），不是真实 Windows 窗口。

## 构建与测试

- `dotnet build` 库、Gallery、测试项目：0 错误。
- 专项测试：`--TestCaseFilter` 只选 `ShareAccessTests|MetricTickerTests|RealtimeStreamTests|TocProgressTests|WalletStackTests`，**35 通过，0 失败**。
- 全量测试：**未运行**（按用户要求只测这些控件）。

## 真实输入（Wine 窗口内注入鼠标和键盘）

| 控件 | 操作与结果 |
|---|---|
| WalletStack | 悬停扇开到 1.00；向下拖动中位置 0.58，松手落到 1.00（`2 of 4`）；点击打开并显示 2 笔交易；Esc 关闭 |
| MetricTicker | 悬停后打开 p95 面板，漂移停住（0.5s 内 0.01）；按住拖动位移约 85；移开后面板关闭 |
| RealtimeStream | 悬停保持，显示 `Paused, 4 new`，视图位移 0；移开恢复 `Live`；点击状态按钮暂停和恢复 |
| TocProgress | 滚轮滚到 384/1195，当前项 1，`5 min left`；点击第 9 项滚到 1113；到底显示 `Finished` |
| ShareAccess | 键入 `m` 出 3 条建议；方向键加 Enter 生成 chip；`bad@` 生成无效 chip 并禁用邀请；两次 Backspace 删除；Enter 发送经 `Sending` 后新增成员；菜单方向键与 Esc，焦点回到触发器；复制后 `Link copied`，约 2s 后复位 |

## 像素对比（差值 >32 的像素占比）

| 状态 | 占比 | 状态 | 占比 |
|---|---|---|---|
| share-access | 5.20% | metric-ticker | 3.16% |
| sa-suggest | 4.87% | mt-panel | 5.20% |
| sa-chips | 5.55% | realtime-stream | 4.12% |
| sa-invite | 5.59% | rs-hover | 5.82%（尺寸差 16，不可比） |
| sa-removed | 6.70% | rs-paused | 4.80%（尺寸差 16，不可比） |
| sa-link | 8.14%（含 Arc 焦点环） | wallet-stack | 1.46% |
| sa-sending | 9.50%（尺寸差 16，不可比） | ws-hover | 2.41% |
| sa-sent | 9.49%（尺寸差 16，不可比） | ws-next | 19.10%（Arc 无悬停、有焦点环，状态不同） |
| sa-copied | 9.45%（尺寸差 16，不可比） | ws-open | 23.68%（Arc 打开第 1 张，脚本打开第 2 张，不可比） |
| toc-progress | 7.62%（文章正文不同） | tp-mid / tp-hover / tp-end | 10.25% / 10.20% / 9.18%（文章正文不同） |

实时数据和漂移位置每次运行都不同，MetricTicker 与 RealtimeStream 的占比只作布局参考。对比图在 `artifacts/batch9-2026-11-03/`（不入库）。

## 对比和真实输入中发现并修复

- ShareAccess 键入即崩溃：建议层与输入框不是祖先关系，`TransformToAncestor` 抛异常（真实键盘发现）。
- MetricTicker 悬停面板崩溃：同样的变换错误。
- MetricTicker 单元格只有文字部分响应鼠标：改为整格命中。
- MetricTicker 漂移后右侧单元格消失：默认布局裁剪随变换移动，已禁用并加回归测试。
- RealtimeStream 悬停时状态仍是 `Live`：改为与暂停相同的显示（对比 Arc 发现）。
- WalletStack：光斑方形硬边（半径算错）；高度多 12、详情低 12；金属纹理过强、阴影过深；黑卡偏暗；玻璃和彩色卡缺高光；网络图标缺口与圆点位置。
- 定时器提为 Normal 优先级：Wine 下默认 Background 优先级会被持续渲染长时间延后。

## 未运行/待验证

- 真实 Windows 窗口、触控、高 DPI、真实字体：未运行，当前环境没有。
- 动画中间帧和手感：Wine 下无垂直同步，帧时序不可信，未取得可信结论。
- 减弱动效：只有部分路径经过代码，未在窗口中单独运行。
- 深色主题：未对比；`ChartLine`、`ChartAnomaly` 深色值和 `TocTrack` 深色值未全部测量。
- Windows 上持续重绘的性能：未测量。
