# Slider

> Pick a value or a range on a track that follows your finger.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/slider
- Markdown: https://uiarc.dev/components/slider/markdown
- Registry item: https://uiarc.dev/r/slider.json
- Source file: `registry/components/slider/slider.tsx`
- Dependencies: motion
- Keywords: slider, range, input, react slider, range slider, dual thumb slider, price range slider, slider with marks, animated slider

## When to use

- Approximate values like volume, opacity, or a price range.
- Ranges with two thumbs, labelled marks, and a formatted readout.

## When not to use

- Use number-field when the exact number matters.
- Use progress to show a value people cannot change.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/slider
pnpm dlx shadcn@latest add @uiarc/slider
yarn dlx shadcn@latest add @uiarc/slider
bunx --bun shadcn@latest add @uiarc/slider
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/slider.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/slider/slider.tsx`

   The source is in the registry item: https://uiarc.dev/r/slider.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Slider } from "@/components/arc/slider/slider";

export function PriceFilter() {
  const [range, setRange] = useState<[number, number]>([20, 80]);
  return (
    <Slider
      label="Price"
      value={range}
      onValueChange={setRange}
      step={5}
      format={(v) => `$${v}`}
      thumbLabels={["Minimum price", "Maximum price"]}
    />
  );
}
```

## API reference

### Slider

A single or range slider with momentum dragging, marks, a value bubble, and a rolling readout.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label; names the thumbs unless thumbLabels is set. |
| `value` | `number \| [number, number]` | – | Controlled value. A pair renders a range with two thumbs. |
| `defaultValue` | `number \| [number, number]` | – | Initial value when uncontrolled. |
| `onValueChange` | `(value: T) => void` | – | Called on every step while dragging or pressing keys. |
| `onValueCommit` | `(value: T) => void` | – | Called once when a drag is released or a key changes the value. |
| `min` | `number` | `0` | Lower bound. |
| `max` | `number` | `100` | Upper bound. |
| `step` | `number` | `1` | Increment. |
| `largeStep` | `number` | `a tenth of the range` | Distance for PageUp, PageDown, and Shift with an arrow. |
| `marks` | `(number \| { value: number; label?: string })[]` | – | Ticks on the track; labelled marks print under it and are clickable. |
| `minStepsBetweenThumbs` | `number` | `0` | Closest two range thumbs may sit, in steps. |
| `format` | `(value: number) => string` | – | Formats the readout, bubble, and spoken value. |
| `showValue` | `boolean` | `true` | Show the readout beside the label. |
| `thumbLabels` | `[string, string]` | – | Accessible names for the two range thumbs. |
| `start` | `ReactNode` | – | Content before the track, such as an icon. |
| `end` | `ReactNode` | – | Content after the track. |
| `name` | `string` | – | Submits the value with a form, one hidden input per thumb. |
| `disabled` | `boolean` | – | Disables dragging and keys. |
| `className` | `string` | – | Added to the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowRight / ArrowUp | Increases by step. |
| ArrowLeft / ArrowDown | Decreases by step. |
| Shift + Arrow / PageUp / PageDown | Moves by largeStep. |
| Home / End | Moves to the lowest or highest allowed value for that thumb. |

## Accessibility

- Each thumb is role="slider" with aria-valuemin, aria-valuemax, aria-valuenow, aria-valuetext from format, and aria-orientation.
- Thumbs are named by label, or by thumbLabels for ranges.
- The readout, bubble, and marks are aria-hidden because the thumbs carry the value.

## Motion

- Released drags carry a little momentum before snapping; the bubble springs up over an active thumb and digits roll in the readout. At a limit the thumb strains toward the key press and springs back.
- Reduced motion removes momentum, the strain, and digit rolling, and turns off CSS transitions.

## Responsive behavior

- It fills its column; the track uses touch-action pan-y, so horizontal drags move the thumb while vertical swipes scroll the page.
- On coarse pointers the value bubble sits higher so the finger does not cover it.
- The bubble and end mark labels clamp inside the track so they never overflow the edges.

## Performance

- Drags write motion values with pointer capture, so the thumb follows 1:1 without React re-renders per frame.
- onValueChange fires on every step; put fetching or heavy work in onValueCommit.

## Notes for AI

- Use for approximate values and ranges like volume, price filters, or opacity. Use number-field when the exact number matters.
- Controlled or uncontrolled; pass a tuple for a range. Put heavy work such as fetching in onValueCommit, not onValueChange.
- Pass name to submit through hidden inputs. Exported as both named and default.

## Related

- [Number field](https://uiarc.dev/components/number-field/markdown): Enter a bounded number with clear increment controls.
- [Progress](https://uiarc.dev/components/progress/markdown): Show how much of a known task is complete.
- [Filter toolbar](https://uiarc.dev/components/filter-toolbar/markdown): Keep collection filters close and easy to reset.

## Also in sliders

- [Elastic slider](https://uiarc.dev/components/elastic-slider/markdown): A slider that stretches like rubber past its ends, with a swaying value tag and detents the thumb clicks into.
- [Range histogram slider](https://uiarc.dev/components/range-histogram-slider/markdown): A price range over a live histogram of the data, lighting the bars inside the range and settling on bar edges.
- [Ruler slider](https://uiarc.dev/components/ruler-slider/markdown): A tape of ticks that slides under a fixed needle, with momentum, snapping, and a rolling readout.

## Guidance for AI tools

Slider: Pick a value or a range on a track that follows your finger. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
