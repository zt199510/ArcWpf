# Metric card

> A compact summary for a number that needs context.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/metric-card
- Markdown: https://uiarc.dev/components/metric-card/markdown
- Registry item: https://uiarc.dev/r/metric-card.json
- Source file: `registry/components/metric-card/metric-card.tsx`
- Dependencies: motion
- Keywords: data, summary, react metric card, kpi card, odometer number, rolling number card, dashboard metric, stat tile

## When to use

- A single plain number with odometer style rolling digits, such as uptime or orders.
- Numbers that update live and should roll in the direction they moved.

## When not to use

- Use stat-card for preformatted strings, trend arrows, tones, and icons.
- Use sparkline when the trend over time matters more than the latest value.
- Use animated-counter for a number without a card around it.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/metric-card
pnpm dlx shadcn@latest add @uiarc/metric-card
yarn dlx shadcn@latest add @uiarc/metric-card
bunx --bun shadcn@latest add @uiarc/metric-card
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/metric-card.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/metric-card/metric-card.tsx`

   The source is in the registry item: https://uiarc.dev/r/metric-card.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { MetricCard } from "@/components/arc/metric-card/metric-card";

export function Uptime() {
  return <MetricCard label="Uptime" value={99.9} suffix="%" context="Last 30 days" change="+0.2%" />;
}
```

## API reference

### MetricCard

A compact card with a label, an odometer style number, context line, and optional change chip.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | What is measured. |
| `value` (required) | `number` | – | The number. Rolls up from zero on first view, then between values. |
| `suffix` | `string` | – | Text after the number, such as "%" or " ms". |
| `context` (required) | `string` | – | Line under the number, such as "vs last week". |
| `change` | `string` | – | Chip in the top corner, such as "+12%". Omit to hide it. |

## Accessibility

- Renders an article; the number is read as plain text through a visually hidden copy.
- The change chip is plain text, so write the direction into it (+ or -) rather than relying on color.
- It does not announce updates; wrap it in a live region if values change while people watch.

## Motion

- The value uses animated-counter: digits roll on view and turn the way the number moved.
- Label, context, and change copy roll in from the direction the number moved, and the chip width springs.
- Reduced motion jumps the digits and swaps copy with a quick fade.

## Responsive behavior

- Below 380px the padding tightens and the change chip wraps under the label instead of crowding it.
- Copy lines stay on one line and clip, so keep label and context short.

## Performance

- Digits roll with animated-counter and a ResizeObserver drives the chip width spring; a dashboard row of cards is fine.

## Notes for AI

- Use when the value is a plain number and you want the rolling digits. Use stat-card for preformatted strings, trend arrows, tones, and icons.
- value must be a number; put units in suffix and formatting-free context in context.

## Related

- [Animated counter](https://uiarc.dev/components/animated-counter/markdown): Give changing totals a clear sense of movement.
- [Sparkline](https://uiarc.dev/components/sparkline/markdown): Show a compact trend beside a value.
- [Gauge](https://uiarc.dev/components/gauge/markdown): Show a value against a known range.

## Also in cards

- [Card](https://uiarc.dev/components/card/markdown): A contained group of related content and actions.
- [Empty state](https://uiarc.dev/components/empty-state/markdown): A useful next step when there is nothing to show yet.
- [Metric ticker](https://uiarc.dev/components/metric-ticker/markdown): A strip of live KPIs that roll to new values digit by digit and flash their change; rest on one to pause the strip and open its recent trend as a sparkline.

## Guidance for AI tools

Metric card: A compact summary for a number that needs context. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
