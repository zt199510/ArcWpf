# Color picker

> A swatch that grows into a picker with format morphing, eyedropper, saved swatches, and contrast readout.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/color-picker
- Markdown: https://uiarc.dev/components/color-picker/markdown
- Registry item: https://uiarc.dev/r/color-picker.json
- Source file: `registry/components/color-picker/color-picker.tsx`
- Dependencies: motion, lucide-react
- Keywords: inputs, new, react color picker, hex color input, oklch color picker, hsl picker, eyedropper, color contrast checker, color swatches, alpha color picker

## When to use

- Theme editors and brand settings where users choose an exact color.
- Design tools that need hex, RGB, HSL, and OKLCH input plus opacity.
- Any color choice where contrast against a background should be visible while picking.

## When not to use

- Use segmented-control or radio-group when the choice is between a few fixed colors.
- Use input for a plain hex field with no visual picking.
- Use chip-group for tagging items with a small preset palette.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/color-picker
pnpm dlx shadcn@latest add @uiarc/color-picker
yarn dlx shadcn@latest add @uiarc/color-picker
bunx --bun shadcn@latest add @uiarc/color-picker
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/color-picker.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/color-picker/color-picker.tsx`

   The source is in the registry item: https://uiarc.dev/r/color-picker.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { ColorPicker } from "@/components/arc/color-picker/color-picker";
import type { ColorSwatch } from "@/components/arc/color-picker/color-picker";

export function BrandColor() {
  const [color, setColor] = useState("#2F6BFF");
  const [swatches, setSwatches] = useState<ColorSwatch[]>([
    { id: "ink", color: "#17171A" },
    { id: "sky", color: "#3A8DFF" },
  ]);

  return (
    <ColorPicker
      label="Accent"
      value={color}
      onValueChange={setColor}
      background="#FFFFFF"
      swatches={swatches}
      onSwatchesChange={setSwatches}
    />
  );
}
```

## API reference

### ColorPicker

A swatch trigger that grows into a picker with a saturation area, hue and opacity sliders, a typed value, contrast readout, and saved swatches.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `value` | `string` | – | Controlled color in hex, rgb(), hsl(), or oklch(). |
| `defaultValue` | `string` | `"#2F6BFF"` | Uncontrolled starting color. |
| `onValueChange` | `(hex: string) => void` | – | Receives the color as uppercase hex, with two alpha digits when it is not opaque. |
| `background` | `string` | `"#FFFFFF"` | The background the color will sit on, for the WCAG contrast readout. |
| `label` | `string` | `"Color"` | Name shown on the swatch, such as "Accent". |
| `swatches` | `ColorSwatch[]` | – | Controlled saved swatches: { id, color }. |
| `defaultSwatches` | `ColorSwatch[]` | – | Uncontrolled starting swatches. Defaults to none. |
| `onSwatchesChange` | `(swatches: ColorSwatch[]) => void` | – | Called when swatches are added, reordered, or removed. |
| `maxSwatches` | `number` | `7` | Most saved swatches. Saving past it drops the oldest. |
| `defaultFormat` | `"hex" \| "rgb" \| "hsl" \| "oklch"` | `"hex"` | Format shown in the text field first. |
| `className` | `string` | – | Extra class on the root. |

### parseColor

Reads hex, rgb(), hsl(), and oklch() in modern or comma syntax into { h, s, v, a }. Returns null for anything else.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `input` (required) | `string` | – | Color text. |
| `hue` | `number` | `0` | Hue to keep for grays, where hue is undefined. |

### toHex

Formats { h, s, v, a } as uppercase hex, adding alpha digits below full opacity.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `hsva` (required) | `Hsva` | – | Hue in degrees, saturation, value, and alpha from 0 to 1. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Arrow keys | Move the saturation and brightness thumb in 2D, or the focused hue or opacity slider; Shift takes steps of ten. |
| PageUp / PageDown | Step a slider by ten. |
| Home / End | Move a slider to its minimum or maximum; in the swatch row, jump to the first or last swatch. |
| Enter | Applies the typed value in the text field; an invalid value shakes the field and shows an error. |
| Alt + ArrowLeft / ArrowRight | Moves the focused swatch. |
| Delete / Backspace | Removes the focused swatch. |
| Escape | Discards a typed draft first, then closes the panel and returns focus to the trigger. |

## Accessibility

- The trigger has aria-haspopup="dialog" and aria-expanded; the panel is a labelled dialog that turns inert while closing.
- The area thumb is a role="slider" with aria-roledescription="2D slider" and a value text naming saturation and brightness; hue and opacity are sliders with degree and percent value texts.
- The text field sets aria-invalid and points to a polite error message; the contrast ratio and its WCAG grade are also written out for screen readers.
- Saved swatches are a horizontal listbox with a roving tabindex, aria-selected on the current color, and labels that explain move and delete.

## Motion

- The panel grows out of the swatch with an animated clip-path inset on a spring, and its drop shadow follows the clipped shape.
- Thumbs chase the pointer on a quick spring with slight bounce, and the contrast ratio counts to its new value.
- The format button morphs the text between hex, RGB, HSL, and OKLCH with text-morph; swatches pop in and reorder with layout springs.
- Reduced motion jumps the thumbs, fades the panel instead of clipping it, and skips the format morph and shake.

## Responsive behavior

- The panel is min(19rem, 100vw - 32px) wide and opens down and to the right of the swatch, so leave room there.
- Below 360px viewport width the "against background" caption hides to keep the contrast row on one line.
- The area and sliders use touch-action: none with pointer capture, so dragging on touch never scrolls the page.

## Performance

- Colors are converted in plain math (HSV, HSL, OKLCH) with no color library.
- The panel mounts only while open, and a ResizeObserver measures it for the clip-path animation.
- Thumbs animate motion values, so dragging does not re-render the slider tracks.

## Notes for AI

- Use for picking a single brand or theme color with an alpha channel. onValueChange always returns hex; parse it elsewhere if you need another format.
- Pass the real surface color as background so the contrast readout reflects where the color will be used.
- The eyedropper button appears only where window.EyeDropper exists (Chromium desktop).
- Imports TextMorph from the Arc text-morph component, so install that alongside it.

## Related

- [Text morph](https://uiarc.dev/components/text-morph/markdown): Morph a label into its next state, letter by letter.
- [Slider](https://uiarc.dev/components/slider/markdown): Pick a value or a range on a track that follows your finger.
- [Popover](https://uiarc.dev/components/popover/markdown): A small anchored surface for contextual information.
- [Segmented control](https://uiarc.dev/components/segmented-control/markdown): Switch between a small set of related views.

## Also in pickers

- [Calendar](https://uiarc.dev/components/calendar/markdown): Browse dates in a clear, compact month view.
- [Date picker](https://uiarc.dev/components/date-picker/markdown): Choose a date without losing context.
- [Date range picker](https://uiarc.dev/components/date-range-picker/markdown): A range picker that grows from its trigger into two months with presets and a stretching range highlight.
- [Time picker](https://uiarc.dev/components/time-picker/markdown): Choose a time with sensible keyboard behavior.
- [Emoji picker](https://uiarc.dev/components/emoji-picker/markdown): A full emoji picker with instant search, scroll-synced categories, skin tones, recently used emoji, and a virtualized, keyboard-navigable grid.

## Guidance for AI tools

Color picker: A swatch that grows into a picker with format morphing, eyedropper, saved swatches, and contrast readout. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
