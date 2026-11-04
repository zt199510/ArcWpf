# Bar chart

> Compare one measure across days and scrub any bar for its value.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/bar-chart
- Markdown: https://uiarc.dev/components/bar-chart/markdown
- Registry item: https://uiarc.dev/r/bar-chart.json
- Source file: `registry/components/bar-chart/bar-chart.tsx`
- Dependencies: motion
- Keywords: chart, bar, data, react bar chart, column chart, animated bar chart, svg bar chart, scrubbable chart, daily activity chart

## When to use

- One measure across days, weeks, or months, with an average line.
- Charts with a range switch, where stable keys let bars morph between ranges.

## When not to use

- Use sparkline for a compact trend.
- Use activity-heatmap for daily rhythm over a year.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/bar-chart
pnpm dlx shadcn@latest add @uiarc/bar-chart
yarn dlx shadcn@latest add @uiarc/bar-chart
bunx --bun shadcn@latest add @uiarc/bar-chart
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/bar-chart.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/bar-chart/bar-chart.tsx`

   The source is in the registry item: https://uiarc.dev/r/bar-chart.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { BarChart } from "@/components/arc/bar-chart/bar-chart";

const week = [
  { key: "2026-09-14", label: "Mon, Sep 14", axisLabel: "M", value: 32 },
  { key: "2026-09-15", label: "Tue, Sep 15", axisLabel: "T", value: 48 },
  { key: "2026-09-16", label: "Wed, Sep 16", axisLabel: "W", value: 27 },
];

export function ActiveMinutes() {
  return <BarChart data={week} label="Active minutes" period="Sep 14–16, 2026" unit="min" />;
}
```

## API reference

### BarChart

A single series column chart with an average line and a scrubbable headline value.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `data` (required) | `{ key: string; label: string; axisLabel?: string; value: number }[]` | – | Bars in order. A surviving key keeps its bar, so range switches morph. |
| `label` (required) | `string` | – | What is measured, such as "Active minutes". |
| `period` (required) | `string` | – | Range on show, under the headline. |
| `unit` | `string` | `""` | Unit after each value, such as "min". |
| `averageLabel` | `string` | `"Daily average"` | Headline label at rest. |
| `valueLabel` | `string` | `"Total"` | Headline label while a bar is scrubbed. |
| `categoryLabel` | `string` | `"Day"` | First column header in the screen reader table. |
| `showAverage` | `boolean` | `true` | Draws the average as a reference line. |
| `height` | `number` | `176` | Plot height in pixels. |
| `formatValue` | `(value: number) => string` | – | Formats values and ticks. Defaults to a grouped number. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowLeft / ArrowRight | Moves between bars (ArrowDown and ArrowUp too). |
| PageDown / PageUp | Moves up to seven bars. |
| Home / End | Jumps to the first or last bar. |
| Escape | Stops scrubbing and returns to the average. |

## Accessibility

- The SVG is role="img" with a summary of average, highest, and lowest.
- A focusable role="slider" scrubber reads each bar as its label and value.
- A visually hidden table lists every value, and range changes are announced politely.

## Motion

- Bars grow in with a stagger on first view; new ranges morph bars, ticks, and the average line.
- Scrubbing moves a cursor and rolls the headline value.
- Reduced motion shows bars at once and swaps values without movement.

## Responsive behavior

- The plot measures its width with a ResizeObserver and bars cap at 28px, so wide containers get spacing rather than fat bars.
- Scrubbing uses touch-action pan-y, so a horizontal drag scrubs while vertical swipes scroll the page.

## Performance

- Bars, ticks, and the average line animate with motion values; one series and many bars per view is the intended load.
- A visually hidden table renders every value, so very long ranges double the DOM.

## Notes for AI

- Use for one measure across days, weeks, or months. Use sparkline for a compact trend and activity-heatmap for daily rhythm over a year.
- Use stable keys such as ISO dates so switching ranges morphs instead of redrawing.

## Related

- [Sparkline](https://uiarc.dev/components/sparkline/markdown): Show a compact trend beside a value.
- [Activity heatmap](https://uiarc.dev/components/activity-heatmap/markdown): See a year of activity at a glance, one square per day.
- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.

## Also in charts

- [Line chart](https://uiarc.dev/components/line-chart/markdown): A multi-series line chart with a gliding crosshair, legend toggles, and paths that morph between ranges.
- [Donut chart](https://uiarc.dev/components/donut-chart/markdown): A donut whose arcs morph between datasets, with the active value rolling into the center.
- [Streamgraph](https://uiarc.dev/components/streamgraph/markdown): Layered streams on a wiggle baseline that morph between ranges, with a layer you can isolate and read week by week.
- [Brush chart](https://uiarc.dev/components/brush-chart/markdown): A dense time series with an overview strip: drag a window to zoom, resize it by its handles, and read events in place.
- [Waffle chart](https://uiarc.dev/components/waffle-chart/markdown): A ten by ten unit chart where every cell is one percent, and cells fly to their new group when the data changes.
- [Slope chart](https://uiarc.dev/components/slope-chart/markdown): Before and after on two axes: lines draw in, rank moves sit beside each value, and switching datasets slides every line to its new slope.
- [Gauge](https://uiarc.dev/components/gauge/markdown): Show a value against a known range.
- [Animated counter](https://uiarc.dev/components/animated-counter/markdown): Give changing totals a clear sense of movement.
- [Ridgeline](https://uiarc.dev/components/ridgeline/markdown): Overlapping distributions, one ridge per group: hover to lift a ridge and read its quartiles, switch datasets and every curve morphs.
- [Treemap](https://uiarc.dev/components/treemap/markdown): A squarified treemap: click to drill and the tiles grow to fill the view, with a breadcrumb back and metrics that morph every tile.
- [Split bar](https://uiarc.dev/components/split-bar/markdown): A single 100% bar split into shares: rest on a segment to widen it and read its exact figures, toggle segments from the legend and the rest reflow, with labels inside when they fit and on leader lines when they don't.

## Guidance for AI tools

Bar chart: Compare one measure across days and scrub any bar for its value. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
