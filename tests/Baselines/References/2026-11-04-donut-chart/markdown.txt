# Donut chart

> A donut whose arcs morph between datasets, with the active value rolling into the center.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/donut-chart
- Markdown: https://uiarc.dev/components/donut-chart/markdown
- Registry item: https://uiarc.dev/r/donut-chart.json
- Source file: `registry/components/donut-chart/donut-chart.tsx`
- Dependencies: motion
- Keywords: data, new, react donut chart, pie chart, ring chart, share of total chart, animated donut, chart with legend, category breakdown, accessible pie chart

## When to use

- Traffic by source, spend by category, or storage by file type.
- A dashboard card where the total and one highlighted share matter most.
- Switching between datasets, such as this month and last month, with the same categories.

## When not to use

- Use bar-chart when precise comparison between parts matters or there are many parts.
- Use gauge or usage-meter for a single value against a limit.
- Use line-chart for change over time.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/donut-chart
pnpm dlx shadcn@latest add @uiarc/donut-chart
yarn dlx shadcn@latest add @uiarc/donut-chart
bunx --bun shadcn@latest add @uiarc/donut-chart
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/donut-chart.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/donut-chart/donut-chart.tsx`

   The source is in the registry item: https://uiarc.dev/r/donut-chart.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { DonutChart } from "@/components/arc/donut-chart/donut-chart";

export function TrafficSources() {
  return (
    <DonutChart
      label="Visits by source"
      unit="visits"
      data={[
        { key: "search", label: "Search", value: 4210 },
        { key: "direct", label: "Direct", value: 2380 },
        { key: "social", label: "Social", value: 1190 },
        { key: "email", label: "Email", value: 640 },
        { key: "ads", label: "Ads", value: 120 },
        { key: "other", label: "Referral", value: 90 },
      ]}
    />
  );
}
```

## API reference

### DonutChart

A donut chart with a synced legend that shows and hides segments, a rolling center readout, automatic Other grouping, and arcs that morph between datasets.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `data` (required) | `DonutChartDatum[]` | – | Parts: { key, label, value, color? }. Values of zero or less are left out. |
| `label` (required) | `string` | – | What the whole is, such as "Visits by source". Names the chart for assistive technology. |
| `unit` | `string` | `""` | Unit after values, such as "visits". Shown under the total at rest. |
| `formatValue` | `(value: number) => string` | `grouped number` | Formats values in the center, legend, and table. |
| `totalLabel` | `string` | `"Total"` | Center label at rest, above the total. |
| `size` | `number` | `208` | Diameter in pixels. The chart scales down to fit narrower containers. |
| `thickness` | `number` | `24` | Ring thickness in pixels. |
| `groupBelow` | `number` | `0.04` | Parts below this share of the total join Other, when at least two would. |
| `maxSegments` | `number` | `6` | The most segments drawn, counting Other. The smallest parts beyond it are grouped. |
| `otherLabel` | `string` | `"Other"` | Label of the grouped segment. |
| `activeKey` | `string \| null` | – | Controlled selected segment key. Hover and focus preview other segments without changing it. |
| `defaultActiveKey` | `string \| null` | `null` | Selected segment on first render when uncontrolled. |
| `onActiveChange` | `(key: string \| null) => void` | – | Called when a segment is pinned or unpinned by clicking the ring, or a legend row when legendAction is select. |
| `hiddenKeys` | `string[]` | – | Controlled hidden segment keys. A hidden segment closes and the rest of the ring redistributes. |
| `defaultHiddenKeys` | `string[]` | `[]` | Hidden segments on first render when uncontrolled. |
| `onHiddenKeysChange` | `(keys: string[]) => void` | – | Called when a legend row shows or hides its segment. |
| `legendAction` | `"toggle" \| "select"` | `"toggle"` | What clicking a legend row does: show or hide its segment, or pin it as the selected segment. |
| `legend` | `boolean` | `true` | The synced legend beside or below the ring. |
| `emptyLabel` | `string` | `"No data yet"` | Center text when the total is zero. |
| `ref` | `Ref<HTMLElement>` | – | The figure element. |
| `className` | `string` | – | Class on the figure. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowDown / ArrowRight | Moves to the next legend row, looping at the end. |
| ArrowUp / ArrowLeft | Moves to the previous legend row. |
| Home / End | Jumps to the first or last row. |
| Enter / Space | Shows or hides the focused segment; with legendAction select, pins or unpins it. |
| Escape | Clears the pinned segment. |
| Arrow keys on the ring | Without a legend the ring takes focus: arrows walk the segments, Enter pins one, and each is announced. |

## Accessibility

- The legend is the keyboard path: each row is a button with aria-pressed (shown, or pinned with legendAction select) and a label giving value, share, and grouped members.
- Showing or hiding a segment is announced in a polite live region with the new total. Without a legend the ring is focusable and announces each segment as the arrows reach it.
- The ring and center readout are aria-hidden; a hidden summary and table list every part, including the members of Other.
- Keyboard focus previews a segment in the ring; hover previews with a mouse only. Pointer focus draws no ring; keyboard focus shows Arc's shared focus ring.

## Motion

- One sweep the first time the chart scrolls into view: every segment leaves the top together and each trailing edge follows a beat behind the one before.
- New data and hidden segments morph start and end angles, never paths, from wherever each arc is on screen. Springs keep their velocity, so an interrupted change continues smoothly. Segments keep their order and never remount mid-morph.
- The active segment slides out along its middle and thickens slightly while the others dim. One pointer handler hit-tests the angle, so crossing a gap never drops the hover.
- The center readout rolls like a drum toward the active segment with tabular numbers in a fixed cell; totals and shares count to new values.
- Reduced motion jumps arcs, lifts, and text to their final state.

## Responsive behavior

- The figure is a container: at 460px and wider the legend sits beside the ring, below that it stacks under it.
- The ring scales down from size to fit narrower containers while keeping its aspect ratio.
- Legend hover styles only apply on hover-capable fine pointers; taps pin a segment.

## Performance

- Arc angles and lifts are motion values; one batched paint per frame writes every path d and transform straight to the DOM, with no React render per frame.
- Counting numbers write their text directly; center readouts stay mounted, so hovering never mounts or unmounts nodes.

## Notes for AI

- Use for two to six parts of one whole. With more parts, lean on groupBelow and maxSegments or use bar-chart.
- Keep keys stable between datasets so arcs morph in place.
- Leave color out to get the shared --series-1 to --series-4 palette in data order, then neutral steps; a key keeps its color across datasets. Pass colors only when they carry meaning.
- Legend rows show and hide segments by default; set legendAction to select for the older pin behavior.
- Drive activeKey from a table or filter to highlight the same category elsewhere.

## Related

- [Bar chart](https://uiarc.dev/components/bar-chart/markdown): Compare one measure across days and scrub any bar for its value.
- [Line chart](https://uiarc.dev/components/line-chart/markdown): A multi-series line chart with a gliding crosshair, legend toggles, and paths that morph between ranges.
- [Gauge](https://uiarc.dev/components/gauge/markdown): Show a value against a known range.
- [Usage meter](https://uiarc.dev/components/usage-meter/markdown): Show what fills an allowance and how close it is to the limit.

## Also in charts

- [Streamgraph](https://uiarc.dev/components/streamgraph/markdown): Layered streams on a wiggle baseline that morph between ranges, with a layer you can isolate and read week by week.
- [Brush chart](https://uiarc.dev/components/brush-chart/markdown): A dense time series with an overview strip: drag a window to zoom, resize it by its handles, and read events in place.
- [Waffle chart](https://uiarc.dev/components/waffle-chart/markdown): A ten by ten unit chart where every cell is one percent, and cells fly to their new group when the data changes.
- [Slope chart](https://uiarc.dev/components/slope-chart/markdown): Before and after on two axes: lines draw in, rank moves sit beside each value, and switching datasets slides every line to its new slope.
- [Sparkline](https://uiarc.dev/components/sparkline/markdown): Show a compact trend beside a value.
- [Activity heatmap](https://uiarc.dev/components/activity-heatmap/markdown): See a year of activity at a glance, one square per day.
- [Animated counter](https://uiarc.dev/components/animated-counter/markdown): Give changing totals a clear sense of movement.
- [Ridgeline](https://uiarc.dev/components/ridgeline/markdown): Overlapping distributions, one ridge per group: hover to lift a ridge and read its quartiles, switch datasets and every curve morphs.
- [Treemap](https://uiarc.dev/components/treemap/markdown): A squarified treemap: click to drill and the tiles grow to fill the view, with a breadcrumb back and metrics that morph every tile.
- [Split bar](https://uiarc.dev/components/split-bar/markdown): A single 100% bar split into shares: rest on a segment to widen it and read its exact figures, toggle segments from the legend and the rest reflow, with labels inside when they fit and on leader lines when they don't.

## Guidance for AI tools

Donut chart: A donut whose arcs morph between datasets, with the active value rolling into the center. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
