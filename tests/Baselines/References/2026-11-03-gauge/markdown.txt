# Gauge

> Show a value against a known range.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/gauge
- Markdown: https://uiarc.dev/components/gauge/markdown
- Registry item: https://uiarc.dev/r/gauge.json
- Source file: `registry/components/gauge/gauge.tsx`
- Dependencies: motion
- Keywords: data, chart, react gauge, gauge chart, radial meter, progress ring, threshold gauge, dashboard gauge

## When to use

- One value against a known range, such as disk quota or health score.
- Values with labelled bands, like Healthy, Filling up, and Critical.

## When not to use

- Use progress for task completion.
- Use usage-meter for plan limits and activity-rings for several goals at once.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/gauge
pnpm dlx shadcn@latest add @uiarc/gauge
yarn dlx shadcn@latest add @uiarc/gauge
bunx --bun shadcn@latest add @uiarc/gauge
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/gauge.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/gauge/gauge.tsx`

   The source is in the registry item: https://uiarc.dev/r/gauge.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Gauge } from "@/components/arc/gauge/gauge";

export function DiskUsage() {
  return (
    <Gauge
      label="Disk usage"
      value={72}
      detail="360 of 500 GB"
      thresholds={[
        { from: 0, tone: "success", label: "Healthy" },
        { from: 70, tone: "warning", label: "Filling up" },
        { from: 90, tone: "danger", label: "Critical" },
      ]}
    />
  );
}
```

## API reference

### Gauge

A 270 degree ring meter with a percent readout and optional labelled threshold bands.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` (required) | `number` | – | Current value within min and max. |
| `min` | `number` | `0` | Lower bound. |
| `max` | `number` | `100` | Upper bound. |
| `label` (required) | `string` | – | What is measured. |
| `detail` | `string` | – | Secondary caption line. |
| `tone` | `"accent" \| "success" \| "warning" \| "danger"` | `"accent"` | Ring color when no threshold applies. |
| `thresholds` | `{ from: number; tone: "accent" \| "success" \| "warning" \| "danger"; label: string }[]` | – | Bands by starting value. The highest band reached sets the tone and shows its label. |

## Accessibility

- The ring is role="meter" with aria-valuemin, aria-valuemax, aria-valuenow, and an aria-valuetext including the band label.
- Threshold labels name the state in text, so it never rests on color alone.
- The SVG and animated readout are aria-hidden.

## Motion

- The ring fills once when half in view without overshoot, then springs to new values while the percent counts.
- The band color and label change exactly as the count crosses a threshold.
- Reduced motion lands on the value at once.

## Responsive behavior

- The ring is min(100%, 176px) wide and keeps its aspect ratio, so it shrinks in narrow columns.

## Performance

- It fills once when half in view, then springs to new values; it is one SVG arc with no running loop.

## Notes for AI

- Use for one value against a known range, such as quota or health. Use progress for task completion and usage-meter for plan limits.
- Always add thresholds with labels when tone carries meaning.

## Related

- [Progress](https://uiarc.dev/components/progress/markdown): Show how much of a known task is complete.
- [Usage meter](https://uiarc.dev/components/usage-meter/markdown): Show what fills an allowance and how close it is to the limit.
- [Activity rings](https://uiarc.dev/components/activity-rings/markdown): Daily goals as tick rings that sweep, count up, and trace a second lap past 100%.

## Also in charts

- [Line chart](https://uiarc.dev/components/line-chart/markdown): A multi-series line chart with a gliding crosshair, legend toggles, and paths that morph between ranges.
- [Bar chart](https://uiarc.dev/components/bar-chart/markdown): Compare one measure across days and scrub any bar for its value.
- [Donut chart](https://uiarc.dev/components/donut-chart/markdown): A donut whose arcs morph between datasets, with the active value rolling into the center.
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

Gauge: Show a value against a known range. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
