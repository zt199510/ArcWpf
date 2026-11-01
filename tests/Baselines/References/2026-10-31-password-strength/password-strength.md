# Password strength

> Show how strong a new password is while it is typed.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/password-strength
- Markdown: https://uiarc.dev/components/password-strength/markdown
- Registry item: https://uiarc.dev/r/password-strength.json
- Source file: `registry/components/password-strength/password-strength.tsx`
- Dependencies: motion
- Keywords: password, strength meter, validation, react password strength, password strength meter, password requirements checklist, new password field, password validation, signup password

## When to use

- Sign up and change password forms.
- Password rules that should check off live as the person types.

## When not to use

- Use password-field for sign in.
- Use progress for measurable progress unrelated to passwords.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/password-strength
pnpm dlx shadcn@latest add @uiarc/password-strength
yarn dlx shadcn@latest add @uiarc/password-strength
bunx --bun shadcn@latest add @uiarc/password-strength
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/password-strength.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/password-strength/password-strength.tsx`

   The source is in the registry item: https://uiarc.dev/r/password-strength.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { PasswordStrength } from "@/components/arc/password-strength/password-strength";

export function NewPasswordField() {
  const [strong, setStrong] = useState(false);
  return (
    <PasswordStrength
      label="New password"
      name="password"
      autoComplete="new-password"
      onValueChange={(_, strength) => setStrong(strength.level >= 3)}
    />
  );
}
```

## Examples

### Gate submit on strength

```tsx
const [level, setLevel] = useState(0);

<PasswordStrength
  label="New password"
  name="password"
  autoComplete="new-password"
  onValueChange={(_, strength) => setLevel(strength.level)}
/>
<Button type="submit" disabled={level < 3}>Create account</Button>
```

### Translated, with your own length policy

```tsx
import { PasswordStrength, createPasswordRules } from "@/components/arc/password-strength/password-strength";

const rules = createPasswordRules({
  minLength: 10,
  labels: {
    length: min => `Mindestens ${min} Zeichen`,
    case: "Gross- und Kleinbuchstaben",
    number: "Mindestens eine Zahl",
    symbol: "Mindestens ein Sonderzeichen",
  },
});

export function NewPassword() {
  return (
    <PasswordStrength
      label="Neues Passwort"
      rules={rules}
      shortLength={10}
      messages={{
        levels: { tooShort: "Zu kurz", weak: "Schwach", fair: "Mittel", good: "Gut", strong: "Stark" },
        showPassword: "Passwort anzeigen",
        meterLabel: "Passwortstärke",
        noPassword: "Noch kein Passwort",
        rulesLabel: "Passwortanforderungen",
        ruleMet: ", erfüllt",
        ruleNotMet: ", nicht erfüllt",
        remainingPrefix: "noch",
        remainingSuffix: "",
        summary: ({ label, met, total }) => `Stärke: ${label}. ${met} von ${total} Anforderungen erfüllt.`,
      }}
    />
  );
}
```

## API reference

### PasswordStrength

A new-password field with a four-segment meter, rule checklist, and a morphing strength word.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label tied to the input. |
| `value` | `string` | – | Controlled password. |
| `defaultValue` | `string` | `""` | Initial value when uncontrolled. |
| `onValueChange` | `(value: string, strength: PasswordStrengthResult) => void` | – | Called on every change with the value and its { level, label, met } score. |
| `rules` | `PasswordRule[]` | `defaultPasswordRules` | { id, label, test, remaining? } rules. Strength is the share met across four steps. |
| `error` | `string` | – | Error copy; sets aria-invalid and shakes the field once per new error. |
| `revealed` | `boolean` | – | Controlled show or hide state. |
| `onRevealedChange` | `(revealed: boolean) => void` | – | Called when the reveal toggle is pressed. |
| `messages` | `PasswordStrengthMessagesInput` | `defaultPasswordStrengthMessages` | Every built-in string, for translation: levels { tooShort, weak, fair, good, strong }, showPassword, meterLabel, noPassword, rulesLabel, ruleMet, ruleNotMet, remainingPrefix, remainingSuffix, and summary({ label, met, total }). Pass only what you change; the rest stay English. |
| `shortLength` | `number` | `8` | Below this many characters the meter holds at its first step and reads the tooShort word. |
| `countCharacters` | `(password: string) => number` | `countCodePoints` | How shortLength counts characters. Code points by default (an emoji counts once); pass password => password.length to match a policy that counts UTF-16 units. |
| `...props` | `Omit<InputHTMLAttributes<HTMLInputElement>, "type" \| "value" \| "defaultValue" \| "children">` | – | Forwarded to the input, including ref, name, autoComplete, and onChange. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | On the reveal button, shows or hides the password. |

## Accessibility

- The meter is role="meter" with aria-valuenow 0 to 4 and aria-valuetext set to the strength word.
- The rules list is linked to the input through aria-describedby along with any error; errors set aria-invalid.
- A role="status" region summarizes strength and rules met; the reveal button uses aria-pressed and aria-controls.

## Motion

- Segments fill and change tone in a staggered wave, rules check off with drawn ticks, remaining counts roll, and the word morphs up or down with strength.
- Reduced motion, applied after hydration, drops the shake, wave, and roll in favor of instant changes.

## Responsive behavior

- On coarse pointers the input grows to the base font size, so iOS does not zoom on focus.
- The four-segment meter shares the row width evenly and shrinks with the column.

## Performance

- Scoring runs on the device on every keystroke with simple rule tests; there is no dictionary check.
- One ResizeObserver per field measures the helper row for its height spring; cheap for a form, but avoid hundreds in a table.

## Notes for AI

- Use when creating or changing a password. Use password-field for sign in.
- Controlled or uncontrolled; read strength from onValueChange to gate submit. Scoring runs on the device.
- Also exports estimateStrength(password, rules, { shortLength, levels, countCharacters }), defaultPasswordRules, and createPasswordRules({ minLength, labels, countCharacters }) for server-side checks, translated rule labels, or your own length policy.
- To translate, pass messages for the component's own copy and build rules with createPasswordRules({ labels }) for the rule labels. Keep shortLength and the rules' minLength in line with your server's policy.

## Related

- [Password field](https://uiarc.dev/components/password-field/markdown): Capture sensitive text with a visible reveal control.
- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.
- [Progress](https://uiarc.dev/components/progress/markdown): Show how much of a known task is complete.

## Also in text fields

- [Textarea](https://uiarc.dev/components/textarea/markdown): A multiline field for notes, descriptions, and longer text.
- [Search field](https://uiarc.dev/components/search-field/markdown): A recognizable search entry point with clear affordances.
- [Expanding search](https://uiarc.dev/components/expanding-search/markdown): An icon that morphs into a search field with results beneath it.
- [Inline edit](https://uiarc.dev/components/inline-edit/markdown): Rename in place: the text becomes a field without moving.

## Guidance for AI tools

Password strength: Show how strong a new password is while it is typed. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
