# Radio cards

> Selectable option cards with a sliding selection ring, price and description slots, and radio keyboard behavior.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/radio-cards
- Markdown: https://uiarc.dev/components/radio-cards/markdown
- Registry item: https://uiarc.dev/r/radio-cards.json
- Source file: `registry/components/radio-cards/radio-cards.tsx`
- Dependencies: motion
- Keywords: inputs, new, react radio cards, selectable cards, radio card group, plan selector, shipping options, option cards, card radio buttons

## When to use

- Choices where each option needs a description, price, or estimate to decide.
- Plan, shipping, region, or size pickers inside checkout and setup flows.
- Options that may be unavailable and need a visible reason.

## When not to use

- Use radio-group for short text-only options.
- Use segmented-control for two to four view modes that switch content instantly.
- Use select or combobox for long lists.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/radio-cards
pnpm dlx shadcn@latest add @uiarc/radio-cards
yarn dlx shadcn@latest add @uiarc/radio-cards
bunx --bun shadcn@latest add @uiarc/radio-cards
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/radio-cards.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/radio-cards/radio-cards.tsx`

   The source is in the registry item: https://uiarc.dev/r/radio-cards.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { RadioCards } from "@/components/arc/radio-cards/radio-cards";

export function ShippingSpeed() {
  return (
    <RadioCards
      aria-label="Shipping speed"
      name="shipping"
      layout="list"
      defaultValue="standard"
      options={[
        { value: "standard", label: "Standard", description: "4 to 6 business days", meta: "Free" },
        { value: "express", label: "Express", description: "2 business days", meta: "$12" },
        { value: "overnight", label: "Overnight", meta: "$29", disabled: true, disabledReason: "Not available for this address" },
      ]}
    />
  );
}
```

## API reference

### RadioCards

Selectable option cards for choices that need more than a label. One selection ring glides from card to card, and the group behaves as a native radio group.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `options` (required) | `RadioCardOption[]` | – | Choices: { value, label, description?, meta?, icon?, disabled?, disabledReason? }. |
| `value` | `string \| null` | – | Selected value (controlled). |
| `defaultValue` | `string \| null` | `null` | Initial value when uncontrolled. |
| `onValueChange` | `(value: string) => void` | – | Called when a new card is selected. |
| `layout` | `"grid" \| "list"` | `"grid"` | "grid" places cards in responsive columns; "list" stacks full-width rows with meta at the end. |
| `minColumnWidth` | `number` | `180` | Narrowest a grid column may get before the grid drops a column, in px. |
| `name` | `string` | – | Form field name. Renders a hidden input with the selected value. |
| `required` | `boolean` | – | Marks the group and hidden input as required. |
| `disabled` | `boolean` | `false` | Disables every card. |
| `...props` | `HTMLAttributes<HTMLDivElement>` | – | Forwarded to the radiogroup, including ref, aria-label, and className. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Focuses the selected card, or the first enabled one. One tab stop for the group. |
| Arrow keys | Move to the next or previous enabled card and select it, wrapping. Left and right flip in RTL. |
| Home / End | Select the first or last enabled card. |
| Enter / Space | Selects the focused card. |

## Accessibility

- role="radiogroup" with role="radio" cards, aria-checked, and aria-disabled; give the group an aria-label or aria-labelledby.
- Each card is labelled by its label and described by its description, or by disabledReason when disabled.
- Selection is shown by a ring and a filled indicator dot, not color alone.

## Motion

- One ring springs to the selected card's box and its size; resizes snap it without replaying the glide.
- The indicator dot scales in on the snappy spring.
- Reduced motion jumps the ring and dot to their final state.

## Responsive behavior

- The grid uses auto-fill columns with a minimum of minColumnWidth, so it drops to one column in narrow containers.
- List layout keeps the indicator, text, and meta on one row and truncates nothing; long descriptions wrap.

## Performance

- One ResizeObserver on the group keeps the ring aligned; the ring moves with motion values without re-rendering cards.

## Notes for AI

- Choose it for three to six options that each need a description or price: plans, shipping speeds, regions, instance sizes.
- Pass name to submit with a native form; the value is carried by a hidden input.
- Use disabledReason to explain why an option is unavailable instead of hiding it.

## Related

- [Radio group](https://uiarc.dev/components/radio-group/markdown): Choose one option from a visible set.
- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.
- [Billing toggle](https://uiarc.dev/components/billing-toggle/markdown): A monthly and yearly switch with a savings badge and prices that roll to the new amount.

## Also in toggles

- [Checkbox](https://uiarc.dev/components/checkbox/markdown): A binary choice with a precise, legible state.
- [Switch](https://uiarc.dev/components/switch/markdown): A tactile toggle for settings that take effect immediately.
- [Rating](https://uiarc.dev/components/rating/markdown): A simple, accessible way to capture a numeric opinion.
- [Toggle group](https://uiarc.dev/components/toggle-group/markdown): A row of icon or text toggles, single or multiple: one pressed fill glides between options, or each fades in on its own and neighbours join, as in a formatting toolbar.

## Guidance for AI tools

Radio cards: Selectable option cards with a sliding selection ring, price and description slots, and radio keyboard behavior. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
