# Elastic slider

> A slider that stretches like rubber past its ends, with a swaying value tag and detents the thumb clicks into.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/elastic-slider
- Markdown: https://uiarc.dev/components/elastic-slider/markdown
- Registry item: https://uiarc.dev/r/elastic-slider.json
- Source file: `registry/components/elastic-slider/elastic-slider.tsx`
- Dependencies: motion
- Keywords: inputs, new, elastic slider, rubber band slider, react slider, volume slider, slider with detents, stretchy slider, ios slider, animated range input

## When to use

- A single setting people drag to taste, such as volume, brightness, or opacity.
- Media and creative tools where a physical, tactile control fits the product.
- Values with a few common stops that should feel like detents.

## When not to use

- Use slider for dense forms and settings pages where a quiet control fits better.
- Use range-histogram-slider to pick a min and max over data.
- Use number-field or ruler-slider when the exact number matters more than the feel.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/elastic-slider
pnpm dlx shadcn@latest add @uiarc/elastic-slider
yarn dlx shadcn@latest add @uiarc/elastic-slider
bunx --bun shadcn@latest add @uiarc/elastic-slider
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/elastic-slider.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/elastic-slider/elastic-slider.tsx`

   The source is in the registry item: https://uiarc.dev/r/elastic-slider.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ElasticSlider } from "@/components/arc/elastic-slider/elastic-slider";
import { Volume1, Volume2 } from "lucide-react";

export function VolumeControl() {
  const [volume, setVolume] = useState(60);
  return (
    <ElasticSlider
      label="Volume"
      value={volume}
      onValueChange={setVolume}
      marks={[0, { value: 50, label: "50%" }, 100]}
      format={value => `${value}%`}
      startIcon={<Volume1 size={16} />}
      endIcon={<Volume2 size={16} />}
    />
  );
}
```

## API reference

### ElasticSlider

A single value slider that stretches like rubber past either end, swells the thumb under the finger, hangs a value tag that sways with speed, and treats marks as detents the thumb clicks into.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label. It also names the thumb. |
| `value` | `number` | – | Controlled value. Leave it out for uncontrolled use. |
| `defaultValue` | `number` | `min` | Initial value when uncontrolled. |
| `onValueChange` | `(value: number) => void` | – | Called on every change while dragging or pressing keys. |
| `onValueCommit` | `(value: number) => void` | – | Called once a drag is released, a key changes the value, or a mark label is clicked. |
| `min` | `number` | `0` | Lowest value. |
| `max` | `number` | `100` | Highest value. |
| `step` | `number` | `1` | Value increment. Values snap to it. |
| `largeStep` | `number` | – | Distance for PageUp, PageDown, and Shift with an arrow. Defaults to a tenth of the range. |
| `marks` | `(number \| { value: number; label?: string })[]` | – | Detents. A dragged thumb clicks into each one and holds for about 7px. Labelled marks print under the track and can be clicked. |
| `format` | `(value: number) => string` | – | Formats the readout, the value tag, and aria-valuetext. |
| `showValue` | `boolean` | `true` | Shows the value beside the label. The tag over the thumb still appears while dragging or using keys. |
| `startIcon` | `ReactNode` | – | Icon before the track. It is pushed outward when the track is pulled past the start. |
| `endIcon` | `ReactNode` | – | Icon after the track. It is pushed outward when the track is pulled past the end. |
| `name` | `string` | – | Submits the value with a form through a hidden input. |
| `disabled` | `boolean` | `false` | Blocks pointer and keyboard input and removes the thumb from the tab order. |
| `className` | `string` | – | Extra class on the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowRight / ArrowUp | Increases by one step. |
| ArrowLeft / ArrowDown | Decreases by one step. |
| Shift + Arrow / PageUp / PageDown | Moves by largeStep. |
| Home / End | Goes to min or max. At a limit, the track stretches toward the press and snaps back. |

## Accessibility

- The thumb is role="slider" with aria-valuemin, aria-valuemax, aria-valuenow, and aria-valuetext from format.
- The thumb is labelled by the visible label through aria-labelledby.
- The readout, value tag, icons, and mark labels are aria-hidden; the slider carries the value for assistive tech.
- Clicking a mark label is a pointer shortcut; the same values are reachable with keys.

## Motion

- The track rubber-bands past either end with iOS-style resistance and springs back with the release velocity.
- The thumb swells to 1.28x while dragged; the value tag trails it on a soft spring and tilts with its speed.
- Detents pop their tick and, on touch devices that support it, give a short vibration.
- Reduced motion removes the stretch, sway, and tick pop; the thumb jumps to its value and the tag fades.

## Responsive behavior

- The track fills its container; icons and the header stay on one row at any width.
- Touch drags use pointer capture, and detents add a short vibration where supported.
- Mark labels at the ends align to the track edges so they stay inside narrow containers.

## Performance

- Thumb, fill, stretch, and tag are driven by motion values, so dragging does not re-render React per frame.
- Track width is measured with one ResizeObserver.

## Notes for AI

- Choose it for one continuous value where the feel matters: volume, brightness, zoom, playback speed.
- Pass marks for values people aim for (0, 50, 100); they act as soft snaps without blocking values in between.
- Use onValueChange for live previews and onValueCommit for saves or network requests.
- For two thumbs or a range, use slider or range-histogram-slider instead.

## Related

- [Slider](https://uiarc.dev/components/slider/markdown): Pick a value or a range on a track that follows your finger.
- [Range histogram slider](https://uiarc.dev/components/range-histogram-slider/markdown): A price range over a live histogram of the data, lighting the bars inside the range and settling on bar edges.
- [Ruler slider](https://uiarc.dev/components/ruler-slider/markdown): A tape of ticks that slides under a fixed needle, with momentum, snapping, and a rolling readout.
- [Scrub input](https://uiarc.dev/components/scrub-input/markdown): A Figma-style number field: drag the label to scrub, or type values, units, and arithmetic.
- [Number field](https://uiarc.dev/components/number-field/markdown): Enter a bounded number with clear increment controls.

## Guidance for AI tools

Elastic slider: A slider that stretches like rubber past its ends, with a swaying value tag and detents the thumb clicks into. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
