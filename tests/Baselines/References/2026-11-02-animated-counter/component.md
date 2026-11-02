# Animated counter

> Give changing totals a clear sense of movement.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/animated-counter
- Markdown: https://uiarc.dev/components/animated-counter/markdown
- Registry item: https://uiarc.dev/r/animated-counter.json
- Source file: `registry/components/animated-counter/animated-counter.tsx`
- Dependencies: motion
- Keywords: data, motion, react animated counter, number ticker, odometer counter, rolling digits, count up animation, animated number

## When to use

- Standalone numbers that change, such as totals, prices, or live counts.
- Hero stats that roll up from zero when they scroll into view, via animateOnView.

## When not to use

- Use metric-card or stat-card when the number needs a card around it.
- Use text-morph for words rather than numbers.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/animated-counter
pnpm dlx shadcn@latest add @uiarc/animated-counter
yarn dlx shadcn@latest add @uiarc/animated-counter
bunx --bun shadcn@latest add @uiarc/animated-counter
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/animated-counter.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/animated-counter/animated-counter.tsx`

   The source is in the registry item: https://uiarc.dev/r/animated-counter.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { AnimatedCounter } from "@/components/arc/animated-counter/animated-counter";

export function Raised({ amount }: { amount: number }) {
  return <AnimatedCounter label="Raised" value={amount} prefix="$" animateOnView />;
}
```

## API reference

### AnimatedCounter

An odometer style number whose digit columns roll to each new value.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` (required) | `number` | – | The number to show. |
| `label` | `string` | – | Small label above the number. |
| `prefix` | `string` | `""` | Text before the number, such as "$". |
| `suffix` | `string` | `""` | Text after the number, such as "%". |
| `decimals` | `number` | `0` | Fixed fraction digits. |
| `animateOnView` | `boolean` | `false` | Rolls every digit up from zero the first time it scrolls into view. |
| `locale` | `string` | `"en-US"` | Formatting locale. Fixed by default so server and client match. |

## Accessibility

- A visually hidden copy holds the full formatted text; the rolling digits are aria-hidden.
- It does not announce changes; wrap it in a live region if updates must be spoken.

## Motion

- Each digit column turns in the direction the whole number moved, wrapping 9 to 0, with a slight stagger on first reveal.
- Columns and separators slide in or out when the digit count changes.
- Reduced motion jumps digits into place.

## Responsive behavior

- It sizes to its digits, and columns slide in or out when the digit count changes, so neighbours shift slightly.

## Performance

- Each digit is its own motion column; fine for a few counters, not for every cell of a table.
- The first reveal waits for 60% visibility through useInView.

## Notes for AI

- Use for standalone numbers that change: totals, prices, counts. Use metric-card or stat-card when the number needs a card around it.
- Pass a raw number and let decimals, prefix, and suffix format it; do not pass preformatted strings.

## Related

- [Metric card](https://uiarc.dev/components/metric-card/markdown): A compact summary for a number that needs context.
- [Text morph](https://uiarc.dev/components/text-morph/markdown): Morph a label into its next state, letter by letter.

## Also in charts

- [Line chart](https://uiarc.dev/components/line-chart/markdown): A multi-series line chart with a gliding crosshair, legend toggles, and paths that morph between ranges.
- [Bar chart](https://uiarc.dev/components/bar-chart/markdown): Compare one measure across days and scrub any bar for its value.
- [Donut chart](https://uiarc.dev/components/donut-chart/markdown): A donut whose arcs morph between datasets, with the active value rolling into the center.
- [Streamgraph](https://uiarc.dev/components/streamgraph/markdown): Layered streams on a wiggle baseline that morph between ranges, with a layer you can isolate and read week by week.
- [Brush chart](https://uiarc.dev/components/brush-chart/markdown): A dense time series with an overview strip: drag a window to zoom, resize it by its handles, and read events in place.
- [Waffle chart](https://uiarc.dev/components/waffle-chart/markdown): A ten by ten unit chart where every cell is one percent, and cells fly to their new group when the data changes.
- [Slope chart](https://uiarc.dev/components/slope-chart/markdown): Before and after on two axes: lines draw in, rank moves sit beside each value, and switching datasets slides every line to its new slope.
- [Sparkline](https://uiarc.dev/components/sparkline/markdown): Show a compact trend beside a value.
- [Gauge](https://uiarc.dev/components/gauge/markdown): Show a value against a known range.
- [Activity heatmap](https://uiarc.dev/components/activity-heatmap/markdown): See a year of activity at a glance, one square per day.
- [Ridgeline](https://uiarc.dev/components/ridgeline/markdown): Overlapping distributions, one ridge per group: hover to lift a ridge and read its quartiles, switch datasets and every curve morphs.
- [Treemap](https://uiarc.dev/components/treemap/markdown): A squarified treemap: click to drill and the tiles grow to fill the view, with a breadcrumb back and metrics that morph every tile.
- [Split bar](https://uiarc.dev/components/split-bar/markdown): A single 100% bar split into shares: rest on a segment to widen it and read its exact figures, toggle segments from the legend and the rest reflow, with labels inside when they fit and on leader lines when they don't.

## Guidance for AI tools

Animated counter: Give changing totals a clear sense of movement. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
