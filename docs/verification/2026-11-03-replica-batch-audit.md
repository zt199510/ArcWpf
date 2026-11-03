# Carousel / RadioCards / CookieConsent / Reactions / SwipeActions 复刻审计

日期：2026-11-03
范围：对照 uiarc.dev 与冻结的 Arc Free Registry，重新审计并重写这五个控件。

## 审计结论

本记录取代 [上一轮验证](2026-11-03-interaction-batch.md) 中的完成判断。上一轮构建和测试都通过，但并排比较后五项都远未达到复刻要求：

- Gallery 内容与 Arc 演示不同，各页出现原生灰色按钮。
- Reactions 胶囊直接显示类型名 `ArcWpf.Controls.Reaction`。
- Carousel 没有真实幻灯片；RadioCards 圆角 14（Arc 为 20），选中环用 BackEase，而 Arc 是临界阻尼弹簧。

因此五个控件和 Gallery 页已重写，公开属性和事件尽量保留，模板部件有破坏性变更，见各组件规格。

## 验证环境

Linux + Wine 9 + Xvfb，离屏渲染，字体为改名的 Liberation。结果不等于真实窗口验收。

## 测试

运行 `CarouselTests`、`RadioCardsTests`、`CookieConsentTests`、`ReactionsTests`、`SwipeActionsTests`、`ArcDataGridTests`：56 项通过，0 项失败。全量测试未运行。

## 像素对比

对比图和数据在 `artifacts/replica-batch-2026-11-03/`（未入库）。Arc 的照片和头像区域已遮罩，允许 ±1px 整体平移。

| 状态 | 差异 >32 | 差异 >8 |
|---|---:|---:|
| carousel | 5.34% | 8.15% |
| carousel-play | 6.21% | 9.19% |
| radio-cards | 3.79% | 6.66% |
| cookie-consent | 2.13% | 4.63% |
| cookie-panel | 5.44% | 10.35% |
| cookie-chip | 0.00% | 0.05% |
| reactions | 8.03% | 11.44% |
| swipe-actions | 8.34% | 13.34% |
| swipe-open | 8.21% | 12.78% |
| swipe-after | 6.85% | 10.92% |

这些数字没有达到逐像素一致。几乎无文字的 cookie-chip 为 0.00%，抽样的 29 个填充、边框和墨色点中，绝大多数与 Arc 完全相同，其余落在抗锯齿或小数坐标上。其他状态的差异目测集中在文字（Inter 与 Liberation 的字宽和字重不同），但未按区域逐一量化，也未在 Windows 的 Segoe UI 下测量。

尺寸实测：选择器 262×76、行菜单 176×120，与 Arc 相同；各舞台高度差不超过 1px，来自 Arc 截图的小数坐标。

## 未运行或未覆盖

- 真实窗口、真实鼠标拖拽和触摸：Carousel 的拖动、甩动和橡皮筋没有自动化测试；SwipeActions 的释放逻辑只测了指针处理之后的部分。
- 多 DPI、IME、屏幕阅读器、深色主题对比（深色的新令牌由公式推导，未实测）。
- 动效只测了目标值和弹簧参数，没有和 Arc 逐帧比较。
- 菜单与选择器只渲染了 Popup 的内容，没有在真实窗口中看定位。

## 已知差异

- Gallery 使用原创渐变代替 Arc 的产品照片和头像，因其许可不明且不在 Registry 内。
- 收件箱计数和 Carousel 播放图标未实现模糊滚动；Gallery 状态文字只有上升淡入。
- WPF 不路由水平触控板滚轮，Carousel 无法响应。
