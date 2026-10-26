# Radio group

> Choose one option from a visible set.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/radio-group
- Markdown: https://uiarc.dev/components/radio-group/markdown
- Registry item: https://uiarc.dev/r/radio-group.json
- Source file: `registry/components/radio-group/radio-group.tsx`
- Dependencies: motion
- Keywords: field, choice, react radio group, radio buttons, radio cards, plan picker, animated radio, radio with description

## When to use

- Two to six mutually exclusive options that need descriptions, such as plans.
- Form choices that should submit natively through name.

## When not to use

- Use segmented-control for short inline view options.
- Use select for longer lists.
- Use checkbox when several options can be on at once.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/radio-group
pnpm dlx shadcn@latest add @uiarc/radio-group
yarn dlx shadcn@latest add @uiarc/radio-group
bunx --bun shadcn@latest add @uiarc/radio-group
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/radio-group.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/radio-group/radio-group.tsx`

   The source is in the registry item: https://uiarc.dev/r/radio-group.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { RadioGroup } from "@/components/arc/radio-group/radio-group";

export function PlanPicker() {
  const [plan, setPlan] = useState("team");
  return (
    <RadioGroup
      label="Plan"
      name="plan"
      value={plan}
      onValueChange={setPlan}
      options={[
        { value: "solo", label: "Solo", description: "One seat" },
        { value: "team", label: "Team", description: "Up to 20 seats" },
      ]}
    />
  );
}
```

## API reference

### RadioGroup

A fieldset of native radios where one highlight glides to the chosen row.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Rendered as the fieldset legend. |
| `options` (required) | `{ value: string; label: string; description?: string }[]` | – | Rows in order, each with optional secondary copy. |
| `value` (required) | `string` | – | Selected value. |
| `onValueChange` (required) | `(value: string) => void` | – | Called with the chosen value. |
| `name` | `string` | – | Native radio name for form submission. Generated when omitted. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Arrow keys | Move the selection between options (native radio behavior). |
| Tab | Enters and leaves the group at the selected option. |

## Accessibility

- Uses a fieldset and legend with native radio inputs wrapped in labels.
- The highlight and dot are aria-hidden decoration.

## Motion

- One highlight glides to the chosen row on the morph spring while the new dot springs in and the old one shrinks away.
- Reduced motion places the highlight and dot instantly; resizes never animate.

## Responsive behavior

- Rows stack vertically and stretch to the column, so long descriptions wrap cleanly on narrow screens.
- A ResizeObserver re-places the highlight on resize without animating it.

## Performance

- One shared highlight moves between rows instead of a layout animation per row.

## Notes for AI

- Use for two to six mutually exclusive options that need descriptions. Use segmented-control for short inline choices and select for longer lists.
- Always controlled: value and onValueChange are required. Pass name to submit with a form.

## Related

- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.
- [Select](https://uiarc.dev/components/select/markdown): A compact choice field with a keyboard friendly menu.
- [Checkbox](https://uiarc.dev/components/checkbox/markdown): A binary choice with a precise, legible state.

## Also in toggles

- [Radio cards](https://uiarc.dev/components/radio-cards/markdown): Selectable option cards with a sliding selection ring, price and description slots, and radio keyboard behavior.
- [Billing toggle](https://uiarc.dev/components/billing-toggle/markdown): A monthly and yearly switch with a savings badge and prices that roll to the new amount.
- [Switch](https://uiarc.dev/components/switch/markdown): A tactile toggle for settings that take effect immediately.
- [Rating](https://uiarc.dev/components/rating/markdown): A simple, accessible way to capture a numeric opinion.

## Guidance for AI tools

Radio group: Choose one option from a visible set. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
