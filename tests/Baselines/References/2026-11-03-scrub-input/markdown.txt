# Scrub input

> A Figma-style number field: drag the label to scrub, or type values, units, and arithmetic.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/scrub-input
- Markdown: https://uiarc.dev/components/scrub-input/markdown
- Registry item: https://uiarc.dev/r/scrub-input.json
- Source file: `registry/components/scrub-input/scrub-input.tsx`
- Dependencies: motion
- Keywords: inputs, new, react scrub input, drag to change number, scrubbable number field, inspector input, figma number input, math expression input, unit input

## When to use

- Position, size, rotation, and opacity fields in an inspector panel.
- Dense settings where typing exact values and dragging both matter.
- Values with units such as px, %, or deg that people type mixed.

## When not to use

- Use number-field for a normal form with stepper buttons and a visible label.
- Use slider when the range and position matter more than the exact number.
- Use money-input for currency amounts.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/scrub-input
pnpm dlx shadcn@latest add @uiarc/scrub-input
yarn dlx shadcn@latest add @uiarc/scrub-input
bunx --bun shadcn@latest add @uiarc/scrub-input
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/scrub-input.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/scrub-input/scrub-input.tsx`

   The source is in the registry item: https://uiarc.dev/r/scrub-input.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { ScrubInput } from "@/components/arc/scrub-input/scrub-input";

export function LayerInspector() {
  const [width, setWidth] = useState(240);
  const [opacity, setOpacity] = useState(100);
  return (
    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 8 }}>
      <ScrubInput label="W" value={width} onValueChange={setWidth} min={0} units={["px", "%"]} resetValue={240} />
      <ScrubInput label="Opacity" value={opacity} onValueChange={setOpacity} min={0} max={100} unit="%" />
    </div>
  );
}
```

## Examples

### Rotation that wraps

```tsx
<ScrubInput label="R" min={0} max={360} wrap unit="°" units={["°"]} fineStep={0.1} onValueCommit={value => setRotation(value)} />
```

## API reference

### ScrubInput

A compact number field for inspectors: drag the label to scrub, or type values with arithmetic and units, with rubber-band limits.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `ReactNode` | – | Text or a 16px icon. It is the scrub handle. Icon labels need aria-label. |
| `value` | `number` | – | Controlled value. |
| `defaultValue` | `number` | `0` | Starting value when uncontrolled. |
| `onValueChange` | `(value: number, details: ScrubInputChangeDetails) => void` | – | Fires for every change, including each scrub step. details is { source: "scrub" \| "type" \| "key" \| "reset", unit }. |
| `onValueCommit` | `(value: number, details: ScrubInputChangeDetails) => void` | – | Fires once when a change settles: a scrub ends, Enter or blur commits typing, or a key step lands. |
| `min` | `number` | `-Infinity` | Lower limit. |
| `max` | `number` | `Infinity` | Upper limit. |
| `step` | `number` | `1` | One step for arrows and scrubbing. |
| `fineStep` | `number` | `step / 10` | Step with Alt held. |
| `coarseStep` | `number` | `step * 10` | Step with Shift held. Coarse steps snap to the coarse grid. |
| `precision` | `number` | – | Decimal places kept. Defaults to the precision of step or fineStep. |
| `pixelsPerStep` | `number` | `2` | Pointer travel in px for one step while scrubbing. |
| `wrap` | `boolean` | `false` | Wraps from max to min, as rotation does. Needs both limits. |
| `unit` | `string` | – | Controlled unit shown after the number. With more than one entry in units it becomes a button that cycles them. |
| `defaultUnit` | `string` | – | Starting unit when uncontrolled. Defaults to unit or the first of units. |
| `units` | `readonly string[]` | – | Units a typed value may carry, such as ["px", "%"]. Defaults to the current unit. |
| `onUnitChange` | `(unit: string) => void` | – | Called when a typed unit or the unit button changes the unit. |
| `resetValue` | `number` | – | Double click the label to return here. |
| `track` | `boolean` | `true when min and max are set` | Shows where the value sits between min and max as a quiet fill. |
| `size` | `"sm" \| "md"` | `"md"` | Field height: 30px, or the small control height. |
| `pointerLock` | `boolean` | `true` | Locks the mouse pointer while scrubbing when the browser allows it. |
| `className` | `string` | – | Class on the root. |
| `...props` | `InputHTMLAttributes<HTMLInputElement>` | – | Forwarded to the input, except value, defaultValue, onChange, type, size, min, max, step, and prefix. |
| `ref` | `Ref<HTMLInputElement>` | – | The input element. |

### evaluateScrubExpression

Evaluates + - * / and parentheses over numbers that may carry a unit. Returns { value, unit? }, or null for anything else or an unknown unit.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `text` (required) | `string` | – | The typed expression, such as "(100-24)/2" or "50%". Commas read as decimal points. |
| `units` (required) | `readonly string[]` | – | Allowed units. deg and ° are aliases. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowUp / ArrowDown | Steps the value; Shift steps ten times coarser, Alt ten times finer. |
| Enter | Commits typed text, including arithmetic such as 12*2. |
| Escape | Restores the last value while typing, or cancels a scrub back to where it started. |
| Shift / Alt (while scrubbing) | Switches to coarse or fine steps mid-drag and shows the multiplier. |

## Accessibility

- The input is role="spinbutton" with aria-valuenow, finite aria-valuemin and aria-valuemax, and aria-valuetext that spells out percent and degrees.
- The label is a real label for the input; clicking it focuses and selects the value.
- Invalid typed text sets aria-invalid for 900ms and keeps the previous value.
- The unit cycle button is labelled Unit: <unit>. Change unit.

## Motion

- Past min or max the field strains with rubber-band resistance, up to 12px, and springs back on release; the number leans with it.
- The track fill follows a drag one to one and glides to typed or stepped values.
- Invalid input and steps against a limit kick the field with a short shake; unit changes rise from a small blur.
- Reduced motion removes the strain, shake, and glide, and swaps the unit with a fade.

## Responsive behavior

- The field fills its column; set --scrub-input-width to size it.
- Touch scrubs through pointer capture on the label, with touch-action: pan-y so vertical page scroll still works; pointer lock is mouse only.
- With the pointer locked, a stand-in cursor portaled to the body wraps at the window edges so a long drag never runs out of screen.

## Performance

- Scrubbing writes motion values directly and emits only when the rounded value changes.
- One ResizeObserver per field tracks its width for the strain scale.

## Notes for AI

- Use in design tool inspectors, property panels, and editors where people tune values by feel.
- Save on onValueCommit, not onValueChange, which fires on every scrub step.
- Set min and max for bounded values to get the track fill; set wrap for angles.
- For a labelled form field with plus and minus buttons use number-field; for a range with a thumb use slider.

## Related

- [Number field](https://uiarc.dev/components/number-field/markdown): Enter a bounded number with clear increment controls.
- [Slider](https://uiarc.dev/components/slider/markdown): Pick a value or a range on a track that follows your finger.
- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.
- [Money input](https://uiarc.dev/components/money-input/markdown): A currency field with live grouping, stable width, rolling digits, and minor-unit output.
- [Color picker](https://uiarc.dev/components/color-picker/markdown): A swatch that grows into a picker with format morphing, eyedropper, saved swatches, and contrast readout.

## Also in special inputs

- [Phone input](https://uiarc.dev/components/phone-input/markdown): A phone field with a country picker, formatting as you type, and E.164 output.
- [Tag input](https://uiarc.dev/components/tag-input/markdown): Turn short text values into removable tags.
- [Mention input](https://uiarc.dev/components/mention-input/markdown): A textarea with @people and #channel mentions that act as single tokens, with suggestions at the caret.
- [Shortcut recorder](https://uiarc.dev/components/shortcut-recorder/markdown): Record key combinations into key caps, with conflict warnings, Kbd, and a searchable cheatsheet.

## Guidance for AI tools

Scrub input: A Figma-style number field: drag the label to scrub, or type values, units, and arithmetic. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
