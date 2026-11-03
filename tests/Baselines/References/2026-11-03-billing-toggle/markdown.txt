# Billing toggle

> A monthly and yearly switch with a savings badge and prices that roll to the new amount.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/billing-toggle
- Markdown: https://uiarc.dev/components/billing-toggle/markdown
- Registry item: https://uiarc.dev/r/billing-toggle.json
- Source file: `registry/components/billing-toggle/billing-toggle.tsx`
- Dependencies: motion
- Keywords: inputs, new, react billing toggle, monthly yearly switch, pricing period toggle, annual billing discount, save 20 percent badge, animated price, pricing toggle

## When to use

- The billing period switch at the top of a pricing page.
- Any price display that should roll to a new amount when a period or plan changes, using BillingPrice.
- A small plan picker with a savings note, such as yearly versus lifetime.

## When not to use

- Use segmented-control for switching views or filters with no pricing meaning.
- Use radio-group when options need descriptions or a vertical layout.
- Use switch for a single on or off setting.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/billing-toggle
pnpm dlx shadcn@latest add @uiarc/billing-toggle
yarn dlx shadcn@latest add @uiarc/billing-toggle
bunx --bun shadcn@latest add @uiarc/billing-toggle
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/billing-toggle.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/billing-toggle/billing-toggle.tsx`

   The source is in the registry item: https://uiarc.dev/r/billing-toggle.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { BillingPrice, BillingToggle } from "@/components/arc/billing-toggle/billing-toggle";

export function ProPrice() {
  const [period, setPeriod] = useState("monthly");
  const yearly = period === "yearly";
  return (
    <div>
      <BillingToggle
        value={period}
        onValueChange={setPeriod}
        options={[
          { value: "monthly", label: "Monthly" },
          { value: "yearly", label: "Yearly", badge: "Save 20%", activeBadge: "You save $48" },
        ]}
      />
      <BillingPrice amount={yearly ? 16 : 20} was={yearly ? 20 : undefined} period={yearly ? "per month, billed yearly" : "per month"} />
    </div>
  );
}
```

## API reference

### BillingToggle

A billing period switch. One thumb glides between the options, and the savings note on the cheaper period tints and rewrites itself in place once it is chosen. Set --billing-accent on any ancestor to recolor the note (defaults to --success).

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` (required) | `string` | – | Value of the selected option. Controlled only. |
| `onValueChange` (required) | `(value: string) => void` | – | Called with the chosen option's value on click or arrow key. |
| `options` | `BillingToggleOption[]` | `Monthly and Yearly with a Save 20% badge` | Options in order. Each has value, label, and optional badge and activeBadge. |
| `label` | `string` | `"Billing period"` | Accessible name of the radio group. |
| `size` | `"md" \| "lg"` | `"md"` | md is 36px tall per option, lg is 44px with larger text. |
| `className` | `string` | – | Extra class on the radio group. |

### BillingToggleOption

One option of the toggle.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` (required) | `string` | – | Value passed to onValueChange. Also the React key. |
| `label` (required) | `string` | – | Visible option text. |
| `badge` | `string` | – | Short savings note, such as Save 20%. |
| `activeBadge` | `string` | – | Badge text once this option is selected, such as You save $48. Defaults to badge. |

### BillingPrice

A price that rolls to its new amount when the billing period changes, with an optional struck-through old price and a period that swaps in place.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `amount` (required) | `number` | – | The price to show. |
| `currency` | `string` | `"$"` | Symbol before the amount and the old price. |
| `period` | `string` | – | Text after the price, such as per month. Swaps with a short fade when it changes. |
| `was` | `number` | – | Previous price. Shown struck through only when it is higher than amount. |
| `decimals` | `number` | `0` | Decimal places for amount and was. |
| `className` | `string` | – | Extra class on the price. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Moves focus to the selected option only (roving tabindex). |
| Arrow Right / Arrow Down | Selects and focuses the next option, wrapping at the end. |
| Arrow Left / Arrow Up | Selects and focuses the previous option, wrapping at the start. |
| Home / End | Selects the first or last option. |

## Accessibility

- The toggle is role="radiogroup" labelled by label. Each option is a button with role="radio" and aria-checked.
- Badges are part of the option text, so the saving is read with the option name.
- The sliding thumb is aria-hidden.
- BillingPrice uses del for the old price. The rolling digits come from AnimatedCounter.

## Motion

- One thumb is measured from the selected option and glides to its x and width on a critically damped spring (0.34s, no bounce).
- When an option with a badge is selected, the badge takes a faint --billing-accent tint and its text crossfades with a short rise. Every candidate text reserves space, so the badge never changes width.
- BillingPrice rolls digits to the new amount, fades the old price in from the left, and swaps the period text in place.
- Reduced motion jumps the thumb and swaps badge text without travel.

## Responsive behavior

- The group is inline-flex and capped at 100% width. Options do not wrap, so keep labels and badges short on phones.
- Hover color only applies on devices with a fine pointer. Tap highlight is removed on touch.

## Performance

- Only the thumb and badge text animate. One ResizeObserver re-measures the thumb when the group resizes.
- BillingPrice renders one AnimatedCounter; keep it to a few per page section.

## Notes for AI

- Choose it for the monthly or yearly switch above pricing cards. Use segmented-control for general view switching that has nothing to do with price.
- It is controlled only. Keep the period in state and derive every price from it so all cards update together.
- Put the concrete saving in activeBadge, and a percentage in badge, so the confirmed number appears once chosen.
- Recolor the savings note with --billing-accent on any ancestor, for example --billing-accent: var(--accent) in a wrapper's CSS.
- Use BillingPrice for each card's amount so the digits roll together when the period flips.
- More than two options work, such as monthly, yearly, and lifetime.

## Related

- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.
- [Animated counter](https://uiarc.dev/components/animated-counter/markdown): Give changing totals a clear sense of movement.
- [Radio group](https://uiarc.dev/components/radio-group/markdown): Choose one option from a visible set.

## Also in toggles

- [Radio cards](https://uiarc.dev/components/radio-cards/markdown): Selectable option cards with a sliding selection ring, price and description slots, and radio keyboard behavior.
- [Checkbox](https://uiarc.dev/components/checkbox/markdown): A binary choice with a precise, legible state.
- [Switch](https://uiarc.dev/components/switch/markdown): A tactile toggle for settings that take effect immediately.
- [Rating](https://uiarc.dev/components/rating/markdown): A simple, accessible way to capture a numeric opinion.
- [Toggle group](https://uiarc.dev/components/toggle-group/markdown): A row of icon or text toggles, single or multiple: one pressed fill glides between options, or each fades in on its own and neighbours join, as in a formatting toolbar.

## Guidance for AI tools

Billing toggle: A monthly and yearly switch with a savings badge and prices that roll to the new amount. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
