# Checkbox

> A binary choice with a precise, legible state.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/checkbox
- Markdown: https://uiarc.dev/components/checkbox/markdown
- Registry item: https://uiarc.dev/r/checkbox.json
- Source file: `registry/components/checkbox/checkbox.tsx`
- Dependencies: @radix-ui/react-checkbox, motion
- Keywords: field, form, react checkbox, animated checkbox, indeterminate checkbox, radix checkbox, checkbox with description, terms checkbox

## When to use

- Independent on and off choices confirmed by a submit, such as accepting terms.
- Parent rows that show a partial selection through the indeterminate state.

## When not to use

- Use switch for settings that apply immediately.
- Use radio-group when only one option can be chosen.
- Use chip-group for filter facets people toggle often.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/checkbox
pnpm dlx shadcn@latest add @uiarc/checkbox
yarn dlx shadcn@latest add @uiarc/checkbox
bunx --bun shadcn@latest add @uiarc/checkbox
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/checkbox.json
```

### Manual

1. Install the dependencies:

```bash
npm install @radix-ui/react-checkbox motion
```

2. Copy the source into your project. Main file: `registry/components/checkbox/checkbox.tsx`

   The source is in the registry item: https://uiarc.dev/r/checkbox.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Checkbox } from "@/components/arc/checkbox/checkbox";

export function TermsCheckbox() {
  const [accepted, setAccepted] = useState(false);
  return (
    <Checkbox
      label="I agree to the terms"
      description="You can export your data at any time."
      checked={accepted}
      onCheckedChange={(next) => setAccepted(next === true)}
    />
  );
}
```

## API reference

### Checkbox

A Radix checkbox whose check morphs into the indeterminate dash and back.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` | `string` | – | Visible label. Without it, pass aria-label; the fallback name is "Checkbox". |
| `description` | `string` | – | Secondary copy under the label, linked through aria-describedby. |
| `checked` | `boolean \| "indeterminate"` | – | Controlled state. |
| `defaultChecked` | `boolean \| "indeterminate"` | `false` | Initial state when uncontrolled. |
| `onCheckedChange` | `(checked: boolean \| "indeterminate") => void` | – | Called on every toggle. |
| `...props` | `ComponentPropsWithoutRef<typeof CheckboxPrimitive.Root>` | – | Radix Checkbox root props, including ref, name, value, required, and disabled. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Space | Toggles the checkbox. |

## Accessibility

- Radix renders a button with role="checkbox" and aria-checked, including "mixed" for indeterminate.
- The label is a real label element tied by id; description is linked through aria-describedby.
- The drawn mark is aria-hidden.

## Motion

- The fill scales in on a snappy spring while the check path draws; switching to indeterminate morphs the same path into a dash.
- Reduced motion applies every state change instantly.

## Responsive behavior

- The hit box is a full control-height square, so it stays easy to tap even though the drawn box is smaller.
- The label and description wrap beside the box; the box stays top-aligned with the first line.

## Performance

- A single spring on the fill and a path draw per toggle; long lists of checkboxes are fine.

## Notes for AI

- Use for independent on/off choices in forms. Use switch for settings that apply immediately and chip-group for filter facets.
- Set checked="indeterminate" on a parent checkbox when only some children are selected.
- Radix renders a hidden native input when name is set, so it submits with forms.

## Related

- [Switch](https://uiarc.dev/components/switch/markdown): A tactile toggle for settings that take effect immediately.
- [Radio group](https://uiarc.dev/components/radio-group/markdown): Choose one option from a visible set.
- [Chip group](https://uiarc.dev/components/chip-group/markdown): Filter by a few facets with chips that morph as you pick them.

## Also in toggles

- [Radio cards](https://uiarc.dev/components/radio-cards/markdown): Selectable option cards with a sliding selection ring, price and description slots, and radio keyboard behavior.
- [Billing toggle](https://uiarc.dev/components/billing-toggle/markdown): A monthly and yearly switch with a savings badge and prices that roll to the new amount.
- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.
- [Rating](https://uiarc.dev/components/rating/markdown): A simple, accessible way to capture a numeric opinion.

## Guidance for AI tools

Checkbox: A binary choice with a precise, legible state. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
