# Signature pad

> Smooth ink that thins with speed, with undo, replay, and PNG or SVG export.

- Type: Component (inputs)
- Access: Free, open source
- Page: https://uiarc.dev/components/signature-pad
- Markdown: https://uiarc.dev/components/signature-pad/markdown
- Registry item: https://uiarc.dev/r/signature-pad.json
- Source file: `registry/components/signature-pad/signature-pad.tsx`
- Dependencies: motion, lucide-react
- Keywords: inputs, new, react signature pad, signature capture, draw signature, e-signature field, pressure sensitive ink, signature to png, sign here component

## When to use

- Contracts, delivery confirmations, and consent forms that need a drawn signature.
- Initials or quick sketches where pen pressure and natural stroke width matter.
- Flows that need a clean, cropped PNG or SVG of the signature.

## When not to use

- Use input for a typed name when a legal drawn signature is not required.
- Use file-upload when the user already has a scanned signature image.
- Use hold-to-confirm for a simple deliberate confirmation without a signature artifact.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/signature-pad
pnpm dlx shadcn@latest add @uiarc/signature-pad
yarn dlx shadcn@latest add @uiarc/signature-pad
bunx --bun shadcn@latest add @uiarc/signature-pad
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/signature-pad.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/signature-pad/signature-pad.tsx`

   The source is in the registry item: https://uiarc.dev/r/signature-pad.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { useState } from "react";
import { SignaturePad, signatureToSvg } from "@/components/arc/signature-pad/signature-pad";
import type { InkStroke } from "@/components/arc/signature-pad/signature-pad";

export function SignContract() {
  const [strokes, setStrokes] = useState<InkStroke[]>([]);

  return (
    <form onSubmit={event => { event.preventDefault(); submit(signatureToSvg(strokes)); }}>
      <SignaturePad signer="Ada Lovelace" fileName="ada-signature" onChange={setStrokes} />
      <button type="submit" disabled={!strokes.length}>Sign</button>
    </form>
  );
}
```

## API reference

### SignaturePad

A signature field with pressure-sensitive ink, undo and redo, a wipe to clear, replay, and PNG or SVG export.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `signer` | `string` | – | Printed under the baseline, such as the signer's name. |
| `hint` | `string` | `"Sign here"` | Resting hint on the baseline before the first stroke. |
| `defaultColor` | `"black" \| "blue" \| "violet"` | `"black"` | Starting ink color. |
| `defaultWidth` | `"fine" \| "medium" \| "bold"` | `"medium"` | Starting nib width. |
| `onChange` | `(strokes: InkStroke[]) => void` | – | Receives the strokes after every change, including undo, redo, and clear. |
| `fileName` | `string` | `"signature"` | File name for exports, without extension. |
| `label` | `string` | `"Signature pad"` | Accessible name of the drawing surface. |
| `className` | `string` | – | Extra class on the root. |

### signatureToSvg

Returns a standalone SVG string of the strokes, cropped to the ink with print ink colors. Empty string for no strokes.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `strokes` (required) | `InkStroke[]` | – | Strokes as received from onChange. |

### signatureToPng

Resolves to a transparent PNG Blob cropped to the ink. Rejects when there are no strokes.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `strokes` (required) | `InkStroke[]` | – | Strokes as received from onChange. |
| `scale` | `number` | `3` | Multiple of the 600 by 260 pad resolution. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Mod + Z | Undoes the last change while the pad is focused. |
| Shift + Mod + Z / Mod + Y | Redoes. |
| Delete / Backspace | Clears the signature with the wipe; undo brings it back. |
| Escape | Stops a replay in progress. |
| Arrow keys | Move between ink colors or nib widths inside each radio group. |

## Accessibility

- The pad is focusable with role="img", aria-roledescription="signature pad", and a label that states the stroke count.
- Keyboard shortcuts are described through aria-describedby on a visually hidden paragraph.
- Ink color and width are radio groups with roving tabindex; toolbar icon buttons carry aria-label and title.
- A polite status region announces strokes added, undo, redo, clear, replay, and export results.

## Motion

- Clear sweeps a squeegee edge across the pad while a clip-path wipe removes the ink over 0.56s.
- Replay redraws each stroke in its original rhythm with a nib dot, capping gaps between strokes and fitting the whole replay into about 3.2s.
- The hint fades up from a soft blur; the selected color and width highlight glides with a shared layout spring.
- Reduced motion skips the wipe and blur and uses short opacity fades.

## Responsive behavior

- The pad keeps a 600 / 260 aspect ratio at every width, and strokes scale with it.
- touch-action: none on the pad keeps finger strokes from scrolling the page; coalesced pointer events capture fast strokes on touch and pen.
- The toolbar wraps on narrow widths; hover styles apply only on hover-capable fine pointers.

## Performance

- The live stroke writes its SVG path directly to the DOM on each pointer move, without a React render.
- History keeps the last 60 states; replay runs a rAF loop that re-renders the visible strokes each frame, so very long signatures cost more to replay.
- PNG export draws to an offscreen canvas at 3x by default.

## Notes for AI

- Use for capturing a handwritten signature or initials. Store the InkStroke[] from onChange, or convert it with signatureToSvg or signatureToPng on submit.
- Strokes are stored in a 600 by 260 coordinate space (PAD_WIDTH, PAD_HEIGHT), so they scale with the pad and can be replayed later.
- getStrokeOutline, outlineToPath, and strokePath are also exported if you need the raw outline math.
- The pad is uncontrolled; to reset it, remount it with a new key.

## Related

- [File upload](https://uiarc.dev/components/blocks/file-upload/markdown): A complete file selection flow with constraints, progress, and error feedback.
- [Multi-step form](https://uiarc.dev/components/blocks/multi-step-form/markdown): A guided form that presents one decision at a time and preserves progress.
- [Hold to confirm](https://uiarc.dev/components/hold-to-confirm/markdown): Confirm a destructive action by holding, not tapping.
- [Input](https://uiarc.dev/components/input/markdown): A single line field with clear labels and useful states.

## Also in editors

- [Rich text editor](https://uiarc.dev/components/rich-text-editor/markdown): A lightweight editor with markdown shortcuts, a floating toolbar, a slash menu, and HTML and markdown output.
- [File dropzone](https://uiarc.dev/components/file-dropzone/markdown): A generous target for dropping one or more files.
- [Avatar upload](https://uiarc.dev/components/avatar-upload/markdown): Click or drop a photo onto the avatar to crop it in place with drag, pinch and zoom. It uploads through a progress ring and settles, with clear errors and retry.

## Guidance for AI tools

Signature pad: Smooth ink that thins with speed, with undo, replay, and PNG or SVG export. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
