# Ruler slider

> A tape of ticks that slides under a fixed needle, with momentum, snapping, and a rolling readout.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/ruler-slider
- Markdown: https://uiarc.dev/components/ruler-slider/markdown
- Registry item: https://uiarc.dev/r/ruler-slider.json
- Source file: `registry/components/ruler-slider/ruler-slider.tsx`
- Dependencies: motion
- Keywords: inputs, new, ruler slider, tape measure input, height picker, weight picker, scrollable ruler, momentum slider, measurement input, react ruler picker

## When to use

- Onboarding and profile flows that ask for body measurements or goals.
- Mobile-first inputs where dragging a scale is easier than typing.
- Wide ranges with fine steps that a short slider track cannot resolve.

## When not to use

- Use number-field when people know the exact number and prefer to type it.
- Use slider or elastic-slider when the value is relative, like volume.
- Use range-histogram-slider to pick a range.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/ruler-slider
pnpm dlx shadcn@latest add @uiarc/ruler-slider
yarn dlx shadcn@latest add @uiarc/ruler-slider
bunx --bun shadcn@latest add @uiarc/ruler-slider
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/ruler-slider.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/ruler-slider/ruler-slider.tsx`

   The source is in the registry item: https://uiarc.dev/r/ruler-slider.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { RulerSlider } from "@/components/arc/ruler-slider/ruler-slider";

export function HeightField() {
  const [height, setHeight] = useState(172);
  return (
    <RulerSlider
      label="Height"
      unit="cm"
      min={120}
      max={220}
      value={height}
      onValueChange={setHeight}
      onValueCommit={saveHeight}
    />
  );
}
```

## API reference

### RulerSlider

A precise value input shaped like a tape measure. A ruler of ticks slides under a fixed needle; drag it, flick it to coast onto a tick, tap to glide there, or scroll horizontally. The readout rolls each digit.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Visible label. It also names the control. |
| `value` | `number` | – | Controlled value. Leave it out for uncontrolled use. |
| `defaultValue` | `number` | `min` | Initial value when uncontrolled. |
| `onValueChange` | `(value: number) => void` | – | Called whenever a tick passes the needle, including while coasting. |
| `onValueCommit` | `(value: number) => void` | – | Called once the tape comes to rest after a drag, flick, tap, scroll, or key. |
| `min` | `number` | `0` | Lowest value. |
| `max` | `number` | `100` | Highest value. |
| `step` | `number` | `1` | Value of one small tick. The tape always rests on a tick. |
| `largeStep` | `number` | – | Distance for PageUp, PageDown, and Shift with an arrow. Defaults to one labelled tick (step × majorEvery). |
| `majorEvery` | `number` | `10` | Small ticks between labelled ticks. |
| `spacing` | `number` | `10` | Pixels between small ticks. Wider spacing gives finer control. |
| `format` | `(value: number) => string` | – | Formats the readout, the tick labels, and aria-valuetext. |
| `unit` | `string` | – | Unit shown after the readout and spoken with the value, such as "cm". |
| `name` | `string` | – | Submits the value with a form through a hidden input. |
| `disabled` | `boolean` | `false` | Blocks input and removes the control from the tab order. |
| `className` | `string` | – | Extra class on the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowRight / ArrowUp | Moves one tick up. |
| ArrowLeft / ArrowDown | Moves one tick down. |
| Shift + Arrow / PageUp / PageDown | Moves one labelled tick (largeStep). |
| Home / End | Goes to min or max. |

## Accessibility

- The tape viewport is role="slider" with aria-valuemin, aria-valuemax, aria-valuenow, and aria-valuetext that includes the unit.
- It is labelled by the visible label through aria-labelledby.
- The rolling readout and needle are aria-hidden; the slider announces the value.
- Horizontal trackpad scrolling moves the tape; vertical scrolling is left to the page.

## Motion

- Dragging moves the tape 1:1; a flick coasts with scroll-like deceleration and lands on a tick.
- Pulling past either end rubber-bands, and releasing pulls the tape home with the throw's speed.
- Taps and keys glide on a spring that keeps any speed already in the tape; grabbing a coasting tape catches it in place.
- Each digit of the readout rolls on its own column as ticks pass the needle.
- Reduced motion removes coasting, rubber-banding, and glides; the tape jumps to the nearest tick.

## Responsive behavior

- The viewport fills its container and fades the tape at both edges; the needle stays centered at any width.
- Touch drags, flicks, and taps work the same as mouse; small moves under 4px count as a tap.
- Horizontal trackpad scrolling moves the tape without capturing vertical page scroll.

## Performance

- Ticks are drawn with CSS gradients, not one element per tick, so long ranges stay cheap.
- Only labelled ticks are elements; keep (max - min) / (step × majorEvery) to a few hundred.
- Tape position is a motion value, so dragging and coasting do not re-render per frame.

## Notes for AI

- Choose it for measurements and precise values on touch: height, weight, temperature, tip amount, zoom level.
- Set step to the precision you store and majorEvery so labelled ticks land on round numbers (10 for cm, 5 or 10 for kg).
- Increase spacing when people need finer control on small screens.
- Use onValueCommit for saves: onValueChange fires on every tick while the tape coasts.

## Related

- [Elastic slider](https://uiarc.dev/components/elastic-slider/markdown): A slider that stretches like rubber past its ends, with a swaying value tag and detents the thumb clicks into.
- [Slider](https://uiarc.dev/components/slider/markdown): Pick a value or a range on a track that follows your finger.
- [Scrub input](https://uiarc.dev/components/scrub-input/markdown): A Figma-style number field: drag the label to scrub, or type values, units, and arithmetic.
- [Number field](https://uiarc.dev/components/number-field/markdown): Enter a bounded number with clear increment controls.
- [Time dial](https://uiarc.dev/components/time-dial/markdown): Spin a dial to pick an hour and see which team cities are at work.

## Also in sliders

- [Range histogram slider](https://uiarc.dev/components/range-histogram-slider/markdown): A price range over a live histogram of the data, lighting the bars inside the range and settling on bar edges.

## Guidance for AI tools

Ruler slider: A tape of ticks that slides under a fixed needle, with momentum, snapping, and a rolling readout. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
