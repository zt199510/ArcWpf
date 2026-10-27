# Segmented control

> Switch between a small set of related views.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/segmented-control
- Markdown: https://uiarc.dev/components/segmented-control/markdown
- Registry item: https://uiarc.dev/r/segmented-control.json
- Source file: `registry/components/segmented-control/segmented-control.tsx`
- Dependencies: motion
- Keywords: control, choice, react segmented control, segmented button, toggle group, ios segmented control, view switcher, sliding pill toggle

## When to use

- Two to five short view options like Day, Week, and Month.
- Toolbar toggles between layouts or modes that apply immediately.

## When not to use

- Use tabs when each option swaps a panel of content.
- Use radio-group in forms or when options need descriptions.
- Use switch for a single on and off setting.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/segmented-control
pnpm dlx shadcn@latest add @uiarc/segmented-control
yarn dlx shadcn@latest add @uiarc/segmented-control
bunx --bun shadcn@latest add @uiarc/segmented-control
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/segmented-control.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/segmented-control/segmented-control.tsx`

   The source is in the registry item: https://uiarc.dev/r/segmented-control.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import SegmentedControl from "@/components/arc/segmented-control/segmented-control";

export function RangeToggle() {
  const [range, setRange] = useState("week");
  return (
    <SegmentedControl
      label="Range"
      value={range}
      onValueChange={setRange}
      options={[
        { value: "day", label: "Day" },
        { value: "week", label: "Week" },
        { value: "month", label: "Month" },
      ]}
    />
  );
}
```

## API reference

### SegmentedControl

A row of toggle buttons with one selection pill that slides between them. Default export.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `options` (required) | `{ value: string; label: string; accessory?: ReactNode }[]` | – | Segments in order. An accessory, such as a badge, renders after the label. |
| `value` (required) | `string` | – | Selected value. |
| `onValueChange` (required) | `(value: string) => void` | – | Called with the chosen segment's value, from a click or the arrow, Home and End keys. |
| `label` | `string` | – | aria-label for the group. |
| `onOptionIntent` | `(value: string) => void` | – | Called when the pointer or focus reaches a segment before it is chosen, to start loading what it shows. |
| `className` | `string` | – | Extra class on the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Moves focus into the control, onto the selected segment, and out again. |
| Arrow keys | Select and focus the previous or next segment, wrapping at the ends. |
| Home / End | Select the first or last segment. |

## Accessibility

- Renders role="group" labelled by label, with native buttons using aria-pressed for the selected segment. Only the selected segment is a tab stop.
- The sliding pill is aria-hidden.

## Motion

- A shared layoutId pill slides to the selected segment on the morph spring, scoped per instance by a LayoutGroup.
- Reduced motion moves the pill instantly and disables button transitions.

## Responsive behavior

- The row is inline with max-width 100% and scrolls horizontally with a hidden scrollbar when segments do not fit.
- Segments never shrink or wrap, so keep labels short on mobile.

## Performance

- One shared layoutId pill per instance, scoped by a LayoutGroup; no observers.

## Notes for AI

- Use for two to five short, mutually exclusive view options like time ranges or layouts. Use tabs when each option swaps a panel, and radio-group in forms.
- Always controlled. Import it as a default export.

## Related

- [Tabs](https://uiarc.dev/components/tabs/markdown): Switch between related content in the same context.
- [Radio group](https://uiarc.dev/components/radio-group/markdown): Choose one option from a visible set.
- [Switch](https://uiarc.dev/components/switch/markdown): A tactile toggle for settings that take effect immediately.
- [Liquid tab bar](https://uiarc.dev/components/liquid-tab-bar/markdown): Tabs with a lens selection that slides between them, swaps labels cleanly, and fills in icons as it passes.

## Also in toggles

- [Radio cards](https://uiarc.dev/components/radio-cards/markdown): Selectable option cards with a sliding selection ring, price and description slots, and radio keyboard behavior.
- [Billing toggle](https://uiarc.dev/components/billing-toggle/markdown): A monthly and yearly switch with a savings badge and prices that roll to the new amount.
- [Checkbox](https://uiarc.dev/components/checkbox/markdown): A binary choice with a precise, legible state.
- [Rating](https://uiarc.dev/components/rating/markdown): A simple, accessible way to capture a numeric opinion.

## Guidance for AI tools

Segmented control: Switch between a small set of related views. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
