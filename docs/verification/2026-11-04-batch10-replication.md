# 2026-11-04 第十批复刻验证

状态：**待验收**。范围：ActivityHeatmap、BarChart、LineChart、DonutChart（免费，MIT）和 FunnelChart（Arc Pro）。

## 环境

Linux + Wine + Xvfb，字体为 Liberation 改名替身（字宽、字重、纵向 1px 差异属环境差异），不是真实 Windows 窗口。

## 构建与测试

- `dotnet build` 库、Gallery、测试项目：0 错误。
- 专项测试：`--TestCaseFilter` 只选 `ChartMathTests|BarChartTests|LineChartTests|DonutChartTests|ActivityHeatmapTests|FunnelChartTests`，**52 通过，0 失败**。
- 全量测试：**未运行**（按用户要求只测这些控件）。

测试发现并修复的缺陷：数据在 Loaded 前赋值时，BarChart 的柱子和均值线、ActivityHeatmap 的单元格会直接到终态，入场动画被跳过。修复后由折叠线以下的宿主测试覆盖。

## 真实输入（Wine 窗口内注入鼠标和键盘）

| 控件 | 操作与结果 |
|---|---|
| ActivityHeatmap | 悬停显示 `4 contributions / Tue, Apr 29, 2025`，移到下一格气泡换位；点击选中出环并更新详情；离开后关闭；图例预览、固定、取消固定；切到 2023 得 `1,278 contributions in 2023`；方向键移动焦点 |
| BarChart | 擦洗显示 `41 / Total / Fri, Sep 18`，移到最新一根，离开回到均值 49；切月视图为 30 根、刻度 0–120；方向键与 Esc；切回周视图无残留（leaving=0） |
| LineChart | Tooltip `437, 200`；靠右时留在十字线左侧且不出界（x=217.7，宽 177.8）；翻转由 `TooltipFlips...` 覆盖；离开透明度 0；7d、90d 形变；隐藏序列后 max=300；全部隐藏显示 `Choose a series to show`；方向键 |
| DonutChart | 悬停 Search 抬起 1.00，中心滚到 16,840；按角度命中 Referrals；离开回 Visits；周视图折叠进 Other，月视图恢复；点击图例行隐藏后 23,770 |
| FunnelChart | 39 颗活跃粒子；Tooltip `Added to cart / 16,420 / Total / 27% kept from viewed product`，y=141.3；分离 7.0/193.0；分离 Tooltip 含 Mobile、Desktop 两行；mobile 39.7K、2.3%；desktop 31.6K、50%；方向键与 Esc |

## 像素对比（差值 >32 的像素占比）

| 状态 | 占比 | 状态 | 占比 |
|---|---|---|---|
| activity-heatmap | 5.31% | heat-hover | 6.79%（同一格，含滚动轨差异） |
| bar-chart | 2.31% | bar-hover | 1.91% |
| line-chart | 3.63% | line-hover | 3.78% |
| donut-chart | 2.85% | donut-hover | 2.84% |
| funnel-chart | 3.94%（粒子随机） | funnel-hover | 10.11%（粒子随机，Tooltip 因字宽留在另一侧） |
| funnel-split | 4.02%（粒子随机） | | |

对比图在 `artifacts/batch10-2026-11-04/`（不入库）。

## 深色主题

五个控件都切到深色并截图自查（`side-dark.png`）：配色、对比度和 `Series1..4` 深色值正常。**未与 Arc 深色做像素对比**（Arc 只采了 funnel-dark，且尺寸是 2 倍，不可比）。

## 未运行或证据有限

- 真实 Windows 窗口、触控、高 DPI：未运行。
- 动效减弱：只测了 `Off` 档（五个控件各一个测试）；`Reduced` 档走同一分支，未单独运行。
- 入场动画：确定性步进测试确认了错开和起止。Wine 下窗口打开很慢，`side-reveal.png` 截到时已接近结束，**不能作为中间帧证据**；真实节奏未肉眼观察。
- 动画流畅度和帧率：未测量。
