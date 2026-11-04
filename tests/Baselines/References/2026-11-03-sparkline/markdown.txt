# Sparkline

> Show a compact trend beside a value.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/sparkline
- Markdown: https://uiarc.dev/components/sparkline/markdown
- Registry item: https://uiarc.dev/r/sparkline.json
- Source file: `registry/components/sparkline/sparkline.tsx`
- Dependencies: motion
- Keywords: data, chart, react sparkline, mini line chart, trend line, inline chart, scrubbable chart, svg sparkline

## When to use

- A compact trend inside a card, table row, or KPI tile.
- Small charts people can scrub by pointer or keyboard to read past values.

## When not to use

- Use bar-chart for comparing discrete periods with axes.
- Use stat-card or metric-card when only the latest value matters.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/sparkline
pnpm dlx shadcn@latest add @uiarc/sparkline
yarn dlx shadcn@latest add @uiarc/sparkline
bunx --bun shadcn@latest add @uiarc/sparkline
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/sparkline.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/sparkline/sparkline.tsx`

   The source is in the registry item: https://uiarc.dev/r/sparkline.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Sparkline } from "@/components/arc/sparkline/sparkline";

export function Signups() {
  return (
    <Sparkline
      label="Signups"
      data={[12, 18, 15, 22, 30, 27, 34]}
      labels={["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"]}
      value="34"
      change="+26%"
      tone="success"
    />
  );
}
```

## API reference

### Sparkline

A small line chart with a headline value that can be scrubbed by pointer or keyboard.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `data` (required) | `number[]` | – | Points in order, oldest first. |
| `label` (required) | `string` | – | What is measured. Names the chart. |
| `value` | `string` | – | Headline value at rest. While scrubbing it shows the point under the cursor. |
| `change` | `string` | – | Aside text at rest, such as "+4.1%". |
| `tone` | `"accent" \| "success" \| "warning" \| "danger"` | `"accent"` | Line and change color. |
| `width` | `number` | `160` | Drawing width in SVG units. |
| `height` | `number` | `52` | Drawing height in SVG units. |
| `labels` | `string[]` | – | One label per point, such as a date, shown while scrubbing. |
| `formatValue` | `(value: number, index: number) => string` | – | Formats a scrubbed point. Defaults to a grouped number. |
| `area` | `boolean` | `true` | Quiet fill under the line. |
| `interactive` | `boolean` | `true` | Enables pointer and keyboard scrubbing. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowLeft / ArrowRight | Moves the cursor one point back or forward (ArrowDown and ArrowUp too). |
| PageDown / PageUp | Moves about a sixth of the series. |
| Home / End | Jumps to the first or last point. |
| Escape | Stops scrubbing and returns to the latest value. |

## Accessibility

- The figure has an aria-label describing the series; the SVG is hidden.
- When interactive, the plot is a focusable role="slider" whose aria-valuetext reads the point label and value.
- Pass labels so scrubbed points read as dates rather than positions.

## Motion

- The line draws in on first view and the end dot appears as it lands.
- While scrubbing, the cursor springs between points, later points dim, and headline copy rolls.
- Reduced motion shows the full line at once and moves the cursor without springing.

## Responsive behavior

- The plot measures its width with a ResizeObserver and redraws to fill its container.
- Scrubbing uses touch-action pan-y, so a horizontal drag scrubs while vertical swipes still scroll the page.
- On touch the readout returns to the latest value when the finger lifts.

## Performance

- Each chart has its own ResizeObservers and an in-view draw; set interactive to false in dense tables to drop tab stops and handlers.

## Notes for AI

- Use for a compact trend inside a card or table row. Use bar-chart for comparing discrete periods with axes.
- Pass value as the formatted latest reading; formatValue should match it so scrubbed values look consistent.
- Set interactive to false in dense tables to avoid many tab stops.

## Related

- [Bar chart](https://uiarc.dev/components/bar-chart/markdown): Compare one measure across days and scrub any bar for its value.
- [Metric card](https://uiarc.dev/components/metric-card/markdown): A compact summary for a number that needs context.
- [Gauge](https://uiarc.dev/components/gauge/markdown): Show a value against a known range.

## Also in charts

- [Line chart](https://uiarc.dev/components/line-chart/markdown): A multi-series line chart with a gliding crosshair, legend toggles, and paths that morph between ranges.
- [Donut chart](https://uiarc.dev/components/donut-chart/markdown): A donut whose arcs morph between datasets, with the active value rolling into the center.
- [Streamgraph](https://uiarc.dev/components/streamgraph/markdown): Layered streams on a wiggle baseline that morph between ranges, with a layer you can isolate and read week by week.
- [Brush chart](https://uiarc.dev/components/brush-chart/markdown): A dense time series with an overview strip: drag a window to zoom, resize it by its handles, and read events in place.
- [Waffle chart](https://uiarc.dev/components/waffle-chart/markdown): A ten by ten unit chart where every cell is one percent, and cells fly to their new group when the data changes.
- [Slope chart](https://uiarc.dev/components/slope-chart/markdown): Before and after on two axes: lines draw in, rank moves sit beside each value, and switching datasets slides every line to its new slope.
- [Activity heatmap](https://uiarc.dev/components/activity-heatmap/markdown): See a year of activity at a glance, one square per day.
- [Animated counter](https://uiarc.dev/components/animated-counter/markdown): Give changing totals a clear sense of movement.
- [Ridgeline](https://uiarc.dev/components/ridgeline/markdown): Overlapping distributions, one ridge per group: hover to lift a ridge and read its quartiles, switch datasets and every curve morphs.
- [Treemap](https://uiarc.dev/components/treemap/markdown): A squarified treemap: click to drill and the tiles grow to fill the view, with a breadcrumb back and metrics that morph every tile.
- [Split bar](https://uiarc.dev/components/split-bar/markdown): A single 100% bar split into shares: rest on a segment to widen it and read its exact figures, toggle segments from the legend and the rest reflow, with labels inside when they fit and on leader lines when they don't.

## Guidance for AI tools

Sparkline: Show a compact trend beside a value. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
