# Image compare

> Drag a divider across two images to see what changed.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/image-compare
- Markdown: https://uiarc.dev/components/image-compare/markdown
- Registry item: https://uiarc.dev/r/image-compare.json
- Source file: `registry/components/image-compare/image-compare.tsx`
- Dependencies: motion, lucide-react
- Keywords: before after, comparison, image, react image compare, before after slider, image comparison slider, compare images, photo diff slider

## When to use

- Before and after visuals such as photo edits, redesigns, or cleanup results.
- Top and bottom comparisons, via orientation vertical.

## When not to use

- Use carousel to browse many images.
- Use resizable-panels to split live content rather than images.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/image-compare
pnpm dlx shadcn@latest add @uiarc/image-compare
yarn dlx shadcn@latest add @uiarc/image-compare
bunx --bun shadcn@latest add @uiarc/image-compare
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/image-compare.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/image-compare/image-compare.tsx`

   The source is in the registry item: https://uiarc.dev/r/image-compare.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ImageCompare } from "@/components/arc/image-compare/image-compare";

export function Retouch() {
  return (
    <ImageCompare
      before={<img src="/photo-raw.jpg" alt="Unedited photo" />}
      after={<img src="/photo-edit.jpg" alt="Retouched photo" />}
      aspectRatio="4 / 3"
    />
  );
}
```

## API reference

### ImageCompare

A before and after slider with a draggable divider over two stacked images.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `before` (required) | `ReactNode` | – | Original, shown left or top. Pass an image with its own alt text. |
| `after` (required) | `ReactNode` | – | Result, shown right or bottom. |
| `position` | `number` | – | Controlled divider position in percent from the left or top. |
| `defaultPosition` | `number` | `50` | Initial position when uncontrolled. |
| `onPositionChange` | `(position: number) => void` | – | Called when the divider settles on a new position. |
| `orientation` | `"horizontal" \| "vertical"` | `"horizontal"` | Vertical stacks images top and bottom; changing it swings the divider. |
| `labels` | `[string, string] \| false` | `["Before", "After"]` | Captions over each side. False hides them. |
| `label` | `string` | `"Before and after"` | Accessible name of the divider. |
| `aspectRatio` | `string` | `"3 / 2"` | Frame proportions as a CSS aspect-ratio. |
| `className` | `string` | – | Class for the root. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Arrow keys | Moves the divider by 1 percent, or 10 with Shift. |
| PageUp / PageDown | Moves the divider by 10 percent. |
| Home / End | Shows all of the before or all of the after image. |
| Double click | On the handle, recenters the divider. |

## Accessibility

- The handle is a focusable role="slider" with aria-orientation and aria-valuetext like "40% after".
- Caption chips are aria-hidden; give both images meaningful alt text.
- Dragging works anywhere on the frame, not only on the handle.

## Motion

- The divider follows the pointer and settles with release velocity; the handle stretches into a capsule while dragging.
- Keyboard presses at an edge give a small bump; captions fade as the divider reaches them.
- Reduced motion jumps the divider and handle without springs.

## Responsive behavior

- The frame is full width with a fixed aspect ratio, so it scales to any column.
- On touch the photo waits for a sideways drag before moving the divider, so vertical swipes still scroll the page; a tap jumps the divider there.

## Performance

- The divider runs on motion values and one ResizeObserver, with no React re-render per drag frame.
- Both images stay mounted and stacked, so size them for the frame.

## Notes for AI

- Use for visual diffs: photo edits, redesigns, before and after results. Use carousel to browse many images.
- Both images should share the same crop and size so the reveal lines up.

## Related

- [Carousel](https://uiarc.dev/components/carousel/markdown): Browse a row of slides by dragging, flicking, or arrowing through them.
- [Photo grid](https://uiarc.dev/components/photo-grid/markdown): Pinch through zoom levels and open any photo straight from its cell.
- [Slider](https://uiarc.dev/components/slider/markdown): Pick a value or a range on a track that follows your finger.
- [Resizable panels](https://uiarc.dev/components/resizable-panels/markdown): Trade space between panes by dragging the divider between them.

## Also in media

- [Card stack](https://uiarc.dev/components/card-stack/markdown): Review a deck one card at a time, with a throw and an undo.

## Guidance for AI tools

Image compare: Drag a divider across two images to see what changed. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
