# Textarea

> A multiline field for notes, descriptions, and longer text.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/textarea
- Markdown: https://uiarc.dev/components/textarea/markdown
- Registry item: https://uiarc.dev/r/textarea.json
- Source file: `registry/components/textarea/textarea.tsx`
- Dependencies: motion
- Keywords: field, form, react textarea, multi-line input, textarea with character count, animated textarea, comment field, form textarea

## When to use

- Free-form multi-line text like bios, comments, or feedback.
- Fields with a live character count, which rolls its digits in the helper row.

## When not to use

- Use input for single-line values.
- Use inline-edit with multiline for a description edited in place on a page.
- Use tag-input when the text is really a list of short values.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/textarea
pnpm dlx shadcn@latest add @uiarc/textarea
yarn dlx shadcn@latest add @uiarc/textarea
bunx --bun shadcn@latest add @uiarc/textarea
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/textarea.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/textarea/textarea.tsx`

   The source is in the registry item: https://uiarc.dev/r/textarea.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Textarea } from "@/components/arc/textarea/textarea";

export function BioField() {
  const [bio, setBio] = useState("");
  return (
    <Textarea
      label="Bio"
      rows={4}
      value={bio}
      onChange={(event) => setBio(event.target.value)}
      description={`${280 - bio.length} characters left`}
    />
  );
}
```

## API reference

### Textarea

A labelled multi-line field with the same animated helper and error rows as Input.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label, tied to the textarea with htmlFor. |
| `description` | `string` | – | Helper copy under the field, linked through aria-describedby. Counts such as "120 characters" roll their digits. |
| `error` | `string` | – | Error copy. Sets aria-invalid and renders in a role="alert" row. |
| `...props` | `TextareaHTMLAttributes<HTMLTextAreaElement>` | – | Forwarded to the native textarea, including ref, rows, value, onChange, and maxLength. |

## Accessibility

- Native textarea with a real label.
- Description and error ids are merged into aria-describedby; errors set aria-invalid and use role="alert".
- Animated copy is aria-hidden and mirrored in a visually hidden plain-text span.

## Motion

- Message rows open their height on a smooth spring; changed words rise in with a soft blur and counts roll only the digits that changed.
- Reduced motion drops the roll, blur, and height spring for instant changes.

## Responsive behavior

- It fills its column with a 110px minimum height and a vertical resize handle; it does not auto-grow with content.
- Hover border styles apply only on hover-capable fine pointers.

## Performance

- One ResizeObserver per field measures the helper row for its height spring; cheap for a form, but avoid hundreds in a table.
- Recomputing a character count on each keystroke only re-renders the helper row words that changed.

## Notes for AI

- Use for free-form, multi-line text. Use input for single lines and inline-edit for text edited in place on a page.
- A live character count in description gets the rolling-digit treatment for free.

## Related

- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.
- [Inline edit](https://uiarc.dev/components/inline-edit/markdown): Rename in place: the text becomes a field without moving.
- [Tag input](https://uiarc.dev/components/tag-input/markdown): Turn short text values into removable tags.

## Also in text fields

- [Password field](https://uiarc.dev/components/password-field/markdown): Capture sensitive text with a visible reveal control.
- [Password strength](https://uiarc.dev/components/password-strength/markdown): Show how strong a new password is while it is typed.
- [Search field](https://uiarc.dev/components/search-field/markdown): A recognizable search entry point with clear affordances.
- [Expanding search](https://uiarc.dev/components/expanding-search/markdown): An icon that morphs into a search field with results beneath it.

## Guidance for AI tools

Textarea: A multiline field for notes, descriptions, and longer text. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
