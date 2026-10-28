# SplitButton 来源参考卡

- 来源页面：<https://uiarc.dev/components/split-button>
- 读取日期：2026-10-28
- 来源状态：Free
- 关键意图：默认动作与相邻菜单动作组合在一个按钮表面。
- WPF 映射：主动作 ICommand，菜单动作 MenuItem.Command，Popup 复用 Menu。
- 关键差异：主/副区域使用独立命中目标；不复制 Radix/React API。
