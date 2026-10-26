# Input

> A single line field with clear labels and useful states.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/input
- Markdown: https://uiarc.dev/components/input/markdown
- Registry item: https://uiarc.dev/r/input.json
- Source file: `registry/components/input/input.tsx`
- Dependencies: motion
- Keywords: field, form, react input, text field, animated form input, input with error message, form field validation, labelled input, input helper text

## When to use

- Any single-line text value in a form, such as name, email, or URL.
- Fields whose helper or error copy changes as the person types, where the message should reword in place.
- Plain form posts, since it forwards name and every native input attribute.

## When not to use

- Use textarea for multi-line text.
- Use password-field, search-field, or number-field when the value has that shape.
- Use inline-edit for a value shown as page text and edited in place.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/input
pnpm dlx shadcn@latest add @uiarc/input
yarn dlx shadcn@latest add @uiarc/input
bunx --bun shadcn@latest add @uiarc/input
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/input.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/input/input.tsx`

   The source is in the registry item: https://uiarc.dev/r/input.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Input } from "@/components/arc/input/input";

export function EmailField() {
  const [email, setEmail] = useState("");
  return (
    <Input
      label="Email"
      type="email"
      value={email}
      onChange={(event) => setEmail(event.target.value)}
      description="We only use this for receipts."
      error={email && !email.includes("@") ? "Enter a valid email" : undefined}
    />
  );
}
```

## API reference

### Input

A labelled text input with helper and error copy that open on a spring and reword in place.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label, tied to the input with htmlFor. |
| `description` | `string` | – | Helper copy under the field, linked through aria-describedby. |
| `error` | `string` | – | Error copy. Sets aria-invalid and renders in a role="alert" row. |
| `...props` | `InputHTMLAttributes<HTMLInputElement>` | – | Forwarded to the native input, including ref, type, value, onChange, and name. |

## Accessibility

- Renders a native input with a real label, so focus and form behavior are native.
- Description and error ids are merged into aria-describedby alongside any caller value.
- Errors set aria-invalid and announce through role="alert"; animated words are aria-hidden with a plain screen reader copy.

## Motion

- Helper and error rows open their height on a smooth spring, then changed words rise in and unblur while numbers roll digit by digit.
- Reduced motion mounts rows at full height and swaps words with an instant fade.

## Responsive behavior

- The field fills its grid column with min-width 0, so it shrinks inside narrow layouts without overflowing.
- Text stays at the small size on touch, unlike password-strength, so iOS may zoom on focus if your --text-sm is below 16px.
- Hover border styles apply only on hover-capable fine pointers.

## Performance

- One ResizeObserver per field measures the helper row for its height spring; cheap for a form, but avoid hundreds in a table.
- Only changed words animate, and numbers roll only the digits that changed.

## Notes for AI

- Default single-line text field. Use password-field, search-field, or number-field when the value has that shape.
- Works controlled or uncontrolled like a native input; pass name for plain form submission.
- Changing the error string rewords it in place, so derive it from state instead of toggling separate messages.

## Related

- [Textarea](https://uiarc.dev/components/textarea/markdown): A multiline field for notes, descriptions, and longer text.
- [Password field](https://uiarc.dev/components/password-field/markdown): Capture sensitive text with a visible reveal control.
- [Search field](https://uiarc.dev/components/search-field/markdown): A recognizable search entry point with clear affordances.
- [Number field](https://uiarc.dev/components/number-field/markdown): Enter a bounded number with clear increment controls.

## Also in text fields

- [Password strength](https://uiarc.dev/components/password-strength/markdown): Show how strong a new password is while it is typed.
- [Expanding search](https://uiarc.dev/components/expanding-search/markdown): An icon that morphs into a search field with results beneath it.
- [Inline edit](https://uiarc.dev/components/inline-edit/markdown): Rename in place: the text becomes a field without moving.

## Guidance for AI tools

Input: A single line field with clear labels and useful states. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
