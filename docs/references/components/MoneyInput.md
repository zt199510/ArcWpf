# MoneyInput 参考卡

来源：Arc `https://uiarc.dev/components/money-input`（Registry `https://uiarc.dev/r/money-input.json`），2026-10-31 冻结于 `tests/Baselines/References/2026-10-31-money-input/`（manifest.json 记录 SHA-256 与许可，许可见 `license.html`）。证据等级：源码与 CSS 直接读取（A）；主示例内容取自页面懒加载 chunk（A）；真实浏览器状态截图未采集（用户已确认不作为前置条件）。

- `registry/components/money-input/money-input.tsx` SHA-256 `55411d79e0e1d0e5…`
- `registry/components/money-input/money-input.module.css` SHA-256 `88559c08f38ebd4f…`

## 还原要点

- 外形：外壳 76 高、圆角 26、内边距 0 8 0 18，边框 BorderStrong（悬停/聚焦 Foreground，错误 Danger）；金额 36/500、等宽数字，货币符号 TextSecondary。
- 数值：以最小货币单位输出（$12.50=1250）；草稿规范化（去前导零、小数位上限、整数 12 位）；分组随输入插入且光标按有效字符数还原；缺失的小数位以 MoneyGhost 淡色占位，失焦/Enter 补齐。
- 动效：字符列按位值键控（i0/g3/d/f1），新列宽度 morph 展开、离开 smooth 收起；数字按增减方向 .55em 滚动（snappy，模糊 4→0）；键入时直接切换不滚动；符号切换 .4em 滚动并 morph 宽度；超宽时按 smooth 缩放适配；超过上限时 physical(.42) 以 ±160 速度侧踢，范围文字闪 Warning 1.4 s。
- 货币菜单：36 高胶囊按钮（悬停/打开 SurfaceMuted），箭头 GLIDE(.28) 翻转；菜单 256 宽、圆角 26、内边距 6、SurfaceRaised + 浮层阴影，自按钮角 scale .96、y -4、模糊 2 snappy 弹出；高亮（FieldHighlight，圆角 14）在选项间滑动；键盘上下/Home/End/Enter/Space/Escape/Tab 与字母类型前导。
- 工具行：快捷加额 30 高胶囊（xs 500，按下 .96，到上限时 .45 透明）+ 右侧范围“$1.00 to $2,480.00”。
- 主示例（chunk 80715）：460 宽卡片，“Pay Jasmine Brooks / Studio lunch, Thursday”，Amount 6000，USD/EUR/GBP/JPY 余额，min 100、max=余额，JPY 快捷 1000/5000，描述“Arrow keys step by 1, Shift by 10”，支付 900 ms 后扣减余额并显示“Sent $X to Jasmine.”。
