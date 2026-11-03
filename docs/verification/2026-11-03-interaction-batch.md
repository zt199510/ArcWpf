# Carousel / RadioCards / CookieConsent / Reactions / SwipeActions 验证记录

日期：2026-11-03  
目标：对照 Arc Free 页面冻结资料，验证本批次五个控件的代码、Gallery 接入和专项回归。

## 来源与实现

五项 Arc 来源快照分别位于：

- `tests/Baselines/References/2026-11-03-carousel`
- `tests/Baselines/References/2026-11-03-radio-cards`
- `tests/Baselines/References/2026-11-03-cookie-consent`
- `tests/Baselines/References/2026-11-03-reactions`
- `tests/Baselines/References/2026-11-03-swipe-actions`

每项均有 WPF 控件、主题模板、Gallery 页面、组件规格和参考卡；Gallery 导航已注册五个页面。

## 程序化结果

环境：.NET SDK 10.0.402，目标框架 `net10.0-windows`。

执行命令：

```text
dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --no-restore --filter "FullyQualifiedName~CarouselTests|FullyQualifiedName~RadioCardsTests|FullyQualifiedName~CookieConsentTests|FullyQualifiedName~ReactionsTests|FullyQualifiedName~SwipeActionsTests" --verbosity quiet
dotnet build samples/ArcWpf.Gallery/ArcWpf.Gallery.csproj --no-restore --verbosity quiet
git diff --check
```

结果：

- 五项专项测试：18/18 通过，0 失败，0 跳过。
- Gallery 构建：0 警告，0 错误。
- 差异检查：通过。

## 已覆盖的代码契约

- Carousel：选择、键盘、自动播放、拖拽/触摸捕获、flick、相邻卡片反馈和指示器。
- RadioCards：网格/列表、禁用原因、单一 Tab stop、Home/End 和选中环。
- CookieConsent：bar/panel/reopen、草稿偏好、持久化、位置刷新、滚动偏好列表和焦点归还。
- Reactions：pill、picker、碰撞定位、键盘、可观察计数、选中/悬停反馈和 Add/Close 状态。
- SwipeActions：leading/trailing、拖拽、full swipe、橡皮筋、速度采样、More 菜单、动作命中和关闭路径。

## 尚未宣称通过的项目

本记录没有替代真实窗口验收。明暗主题、真实鼠标命中、触摸设备、DPI、Popup 边界、读屏、逐帧动效和性能仍需在 Windows Gallery 窗口中检查。CookieConsent 的单一 surface 形变、Reactions 的完整 Popover/逐位计数动效、SwipeActions 的触摸菜单观感和 Carousel 的逐帧 spring 观感保留为待验收项。
