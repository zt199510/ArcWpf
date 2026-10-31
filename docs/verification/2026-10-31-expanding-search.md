# ExpandingSearch 验证记录（2026-10-31）

## 来源限制

`https://uiarc.dev/r/expanding-search.json` 在本次环境返回套接字访问拒绝，未能完成 Registry 冻结；实现依据已冻结 Search 令牌和公开交互意图，待网络恢复后补齐来源快照。

## 待运行

- Controls/Gallery 构建。
- ExpandingSearch 专项测试。
- 实际窗口的 collapsed/expanded 比例、输入起点、焦点归还、Escape、清除、明暗主题和 Reduced/Off。

## 程序化结果

- Controls 构建：成功，0 警告/错误。
- Gallery 普通构建：被运行中的旧 Gallery 进程锁定 Controls DLL；未结束用户进程。
- ExpandingSearch 专项：3/3 通过，使用现有构建引用执行。
- 来源：Registry 网络访问被拒绝，未伪造来源快照。


## 截图对照修正

- 用户反馈：展开后缺少搜索信息面板。
- 修复：增加展开后的 Recent 面板，包含条目图标、标题和辅助元数据；面板与搜索框共享 320 DIP 宽度、8 DIP 间距、圆角和浮动阴影。
- ExpandingSearch 专项复验：4/4 通过。
- Arc Registry/CSS 来源仍受当前网络策略阻断，网页精确参数待来源恢复后补齐。


> 2026-10-30 更新：来源已冻结并按源码重做，见 [源码复刻验证](2026-10-30-arc-source-rebuild.md)。
