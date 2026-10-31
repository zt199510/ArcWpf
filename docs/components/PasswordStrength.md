# PasswordStrength 组件规格

状态：WPF fallback 实现，Arc Registry/CSS 当前网络不可访问，未宣称网页完全对齐。

`PasswordStrength` 根据长度、大小写、数字和符号计算 0–4 分，提供 `Password`、`Label`、`ShowRules`、`Score`、`StrengthLabel`，并使用 Progress/Badge 语义令牌。
