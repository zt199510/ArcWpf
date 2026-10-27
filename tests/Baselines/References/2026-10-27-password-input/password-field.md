# Password field

> Capture sensitive text with a visible reveal control.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/password-field
- Markdown: https://uiarc.dev/components/password-field/markdown
- Registry item: https://uiarc.dev/r/password-field.json
- Source file: `registry/components/password-field/password-field.tsx`
- Dependencies: motion
- Keywords: field, security, react password input, show hide password, password toggle, password field with eye icon, login password field

## When to use

- Sign in forms and password confirmation fields.
- Any secret the person may want to check by revealing it.

## When not to use

- Use password-strength when creating or changing a password.
- Use input for non-secret values.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/password-field
pnpm dlx shadcn@latest add @uiarc/password-field
yarn dlx shadcn@latest add @uiarc/password-field
bunx --bun shadcn@latest add @uiarc/password-field
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/password-field.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/password-field/password-field.tsx`

   The source is in the registry item: https://uiarc.dev/r/password-field.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { PasswordField } from "@/components/arc/password-field/password-field";

export function SignInPassword() {
  return (
    <PasswordField
      label="Password"
      name="password"
      autoComplete="current-password"
      required
    />
  );
}
```

## Examples

### Translated

```tsx
<PasswordField label="Passwort" messages={{ showPassword: "Passwort anzeigen", hidePassword: "Passwort verbergen" }} />
```

## API reference

### PasswordField

A password input with a show and hide toggle whose eye icon is slashed by a drawn line.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label tied to the input. |
| `description` | `string` | – | Helper copy under the field, linked through aria-describedby. |
| `messages` | `Partial<{ showPassword: string; hidePassword: string }>` | `{ showPassword: "Show password", hidePassword: "Hide password" }` | Names of the reveal toggle, for translation. Pass only the strings you change. |
| `...props` | `Omit<InputHTMLAttributes<HTMLInputElement>, "type">` | – | Forwarded to the input, including ref, value, onChange, name, and autoComplete. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter / Space | On the toggle button, shows or hides the password. |

## Accessibility

- Toggle is a native button with aria-pressed and a label that switches between "Show password" and "Hide password" (messages.showPassword and messages.hidePassword).
- Description is linked through aria-describedby.
- Set autoComplete to current-password or new-password so password managers work.

## Motion

- A slash draws across the eye and masks the outline beneath it instead of swapping icons.
- After the first toggle, the value resolves through a short CSS reveal on each change; reduced motion draws the slash instantly.

## Responsive behavior

- The field fills its column; the reveal button sits inside the shell, so it never wraps below the input.
- Hover styles apply only on fine pointers.

## Performance

- One ResizeObserver per field measures the helper row for its height spring; cheap for a form, but avoid hundreds in a table.
- The reveal effect is a short CSS animation, and the slash is one drawn path.

## Notes for AI

- Use for sign in and password confirmation. Use password-strength when the user is creating a new password.
- Uncontrolled by default; pass value and onChange to control it like a native input.
- Every built-in string can be translated through messages; the label and description are your own props.

## Related

- [Password strength](https://uiarc.dev/components/password-strength/markdown): Show how strong a new password is while it is typed.
- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.

## Also in text fields

- [Textarea](https://uiarc.dev/components/textarea/markdown): A multiline field for notes, descriptions, and longer text.
- [Search field](https://uiarc.dev/components/search-field/markdown): A recognizable search entry point with clear affordances.
- [Expanding search](https://uiarc.dev/components/expanding-search/markdown): An icon that morphs into a search field with results beneath it.
- [Inline edit](https://uiarc.dev/components/inline-edit/markdown): Rename in place: the text becomes a field without moving.

## Guidance for AI tools

Password field: Capture sensitive text with a visible reveal control. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
