# Activity heatmap

> See a year of activity at a glance, one square per day.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/activity-heatmap
- Markdown: https://uiarc.dev/components/activity-heatmap/markdown
- Registry item: https://uiarc.dev/r/activity-heatmap.json
- Source file: `registry/components/activity-heatmap/activity-heatmap.tsx`
- Dependencies: motion
- Keywords: data, chart, calendar, react activity heatmap, contribution graph, github calendar heatmap, calendar heatmap, streak chart, activity calendar

## When to use

- Daily rhythm over a long range, like contributions or workouts.
- Views where streaks and quiet weeks matter more than exact comparison.

## When not to use

- Use bar-chart when the exact comparison is the point.
- Use calendar to pick dates.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/activity-heatmap
pnpm dlx shadcn@latest add @uiarc/activity-heatmap
yarn dlx shadcn@latest add @uiarc/activity-heatmap
bunx --bun shadcn@latest add @uiarc/activity-heatmap
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/activity-heatmap.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/activity-heatmap/activity-heatmap.tsx`

   The source is in the registry item: https://uiarc.dev/r/activity-heatmap.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { ActivityHeatmap } from "@/components/arc/activity-heatmap/activity-heatmap";

export function Contributions({ days }: { days: { date: string; count: number }[] }) {
  const [selected, setSelected] = useState<string | null>(null);
  return (
    <ActivityHeatmap days={days} label="Contributions in 2026" period="2026" selectedDate={selected} onSelectDate={setSelected} />
  );
}
```

## API reference

### ActivityHeatmap

A contribution style calendar with one tinted square per day and a hover or focus tooltip.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `days` (required) | `{ date: string; count: number }[]` | – | One entry per day as YYYY-MM-DD, oldest first. Missing days count as zero. |
| `label` (required) | `string` | – | Accessible name for the grid, such as "Contributions in 2025". |
| `period` (required) | `string` | – | Finishes the summary: "1,284 contributions in {period}". |
| `unit` | `{ one: string; other: string }` | `{ one: "contribution", other: "contributions" }` | Nouns for the count. |
| `thresholds` | `[number, number, number]` | – | Upper bounds for levels one to three. Defaults to quarters of the busiest day. |
| `weekStartsOn` | `0 \| 1` | `0` | Sunday or Monday as the first row. |
| `selectedDate` | `string \| null` | `null` | Day drawn with a selection ring. |
| `onSelectDate` | `(date: string) => void` | – | Called on click, Enter, or Space. |
| `actions` | `ReactNode` | – | Controls beside the summary, such as a range switch. |
| `locale` | `string` | `"en-US"` | Formatting locale. |
| `className` | `string` | – | Class for the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowUp / ArrowDown | Moves one day back or forward. |
| ArrowLeft / ArrowRight | Moves one week back or forward. |
| Home / End | Jumps to the first or last day. |
| Enter / Space | Selects the focused day. |
| Escape | Hides the tooltip. |

## Accessibility

- The grid is role="grid" with labelled gridcells and a hidden legend explaining the levels.
- Legend swatches are toggle buttons with aria-pressed that highlight days of one level.
- The total and hovered day are announced through status and polite live regions; the tooltip itself is aria-hidden.

## Motion

- Cells wave in once on view and a new range recolors the grid in a sweep.
- The tooltip glides between cells and its text rolls; the total counts to new values.
- Reduced motion drops the wave, sweep, and glide in favor of short fades.

## Responsive behavior

- Cells scale between 9px and 15px with the container, then the grid scrolls horizontally from the newest week.
- Below a 440px container the caption switches to a short form.
- On touch the tooltip lingers after a tap instead of hiding at once.

## Performance

- Every day is its own cell, so a year is about 370 elements; the reveal wave is capped in total time.
- One shared tooltip serves the whole grid.

## Notes for AI

- Use when rhythm, streaks, and quiet weeks matter more than exact comparison. Use bar-chart when the exact comparison is the point.
- Pass a range switch through actions and swap days to change the period.

## Related

- [Bar chart](https://uiarc.dev/components/bar-chart/markdown): Compare one measure across days and scrub any bar for its value.
- [Calendar](https://uiarc.dev/components/calendar/markdown): Browse dates in a clear, compact month view.
- [Activity rings](https://uiarc.dev/components/activity-rings/markdown): Daily goals as tick rings that sweep, count up, and trace a second lap past 100%.
- [Sparkline](https://uiarc.dev/components/sparkline/markdown): Show a compact trend beside a value.

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

Activity heatmap: See a year of activity at a glance, one square per day. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
