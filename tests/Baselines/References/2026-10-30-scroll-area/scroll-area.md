# Scroll area

> A native scroll container with thin overlay scrollbars and edge fades that appear only when content overflows.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/scroll-area
- Markdown: https://uiarc.dev/components/scroll-area/markdown
- Registry item: https://uiarc.dev/r/scroll-area.json
- Source file: `registry/components/scroll-area/scroll-area.tsx`
- Keywords: disclosure, new, react scroll area, custom scrollbar, overlay scrollbar, scroll fade edges, horizontal scroll container, scroll snap container, scrollable panel

## When to use

- Panels, sidebars, and popovers whose content can outgrow their box.
- Horizontal strips of cards or chips that need mouse-wheel scrolling and snap points.
- Places where platform scrollbars look heavy but native scrolling must be kept.

## When not to use

- Use carousel when items should page one at a time with controls.
- Do not wrap the whole page; let the document scroll natively.
- Use data-grid for large tabular data; it virtualizes its own scroll.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/scroll-area
pnpm dlx shadcn@latest add @uiarc/scroll-area
yarn dlx shadcn@latest add @uiarc/scroll-area
bunx --bun shadcn@latest add @uiarc/scroll-area
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/scroll-area.json
```

### Manual

1. No packages are needed beyond React and Next.js.

2. Copy the source into your project. Main file: `registry/components/scroll-area/scroll-area.tsx`

   The source is in the registry item: https://uiarc.dev/r/scroll-area.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ScrollArea } from "@/components/arc/scroll-area/scroll-area";

export function ActivityPanel({ events }: { events: { id: string; text: string }[] }) {
  return (
    <ScrollArea maxHeight={320} label="Recent activity">
      <ul>{events.map(event => <li key={event.id}>{event.text}</li>)}</ul>
    </ScrollArea>
  );
}
```

## Examples

### Horizontal strip with snap

```tsx
<ScrollArea orientation="horizontal" snap="x mandatory" label="Templates">
  <div style={{ display: "flex", gap: 12 }}>
    {templates.map(item => <TemplateCard key={item.id} {...item} style={{ scrollSnapAlign: "start" }} />)}
  </div>
</ScrollArea>
```

## API reference

### ScrollArea

A native scroll container with thin overlay scrollbars that appear while scrolling or on hover, and edge fades that grow with the content left beyond each edge.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `orientation` | `"vertical" \| "horizontal" \| "both"` | `"vertical"` | Axes that scroll. |
| `fade` | `number` | `28` | Length of the edge fade in px. 0 turns the fades off. |
| `scrollbars` | `"auto" \| "always"` | `"auto"` | "auto" shows scrollbars while scrolling or hovering; "always" keeps them visible when content overflows. |
| `hideDelay` | `number` | `900` | How long scrollbars linger after scrolling stops, in ms. |
| `maxHeight` | `CSSProperties["maxHeight"]` | – | Maximum viewport height, for vertical areas that grow with their content. |
| `snap` | `CSSProperties["scrollSnapType"]` | – | Passed to the viewport's scroll-snap-type, for example "x mandatory". Children set their own scroll-snap-align. |
| `label` | `string` | – | Accessible name. The viewport becomes a labelled region. |
| `wheelToHorizontal` | `boolean` | `true` | Turns vertical wheel movement into horizontal scrolling for horizontal areas. |
| `viewportClassName` | `string` | – | Extra class on the scrolling viewport. |
| `viewportStyle` | `CSSProperties` | – | Inline style on the viewport. |
| `viewportRef` | `Ref<HTMLDivElement>` | – | The scrolling element, for programmatic scrolling. |
| `onScroll` | `(event: UIEvent<HTMLDivElement>) => void` | – | Viewport scroll handler. |
| `onEdgeChange` | `(edges: ScrollAreaEdges) => void` | – | Called when content starts or stops extending past an edge: { top, bottom, left, right }. |
| `...props` | `HTMLAttributes<HTMLDivElement>` | – | Forwarded to the root, including ref and className. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | The viewport is focusable (tabIndex 0). |
| Arrow keys / Page Up / Page Down / Home / End / Space | Scroll natively once the viewport has focus. |

## Accessibility

- Scrolling stays native, so keyboard, screen reader, and touch behavior match the platform.
- With label, the viewport is a named region; without one, give it context another way.
- The overlay tracks and thumbs are aria-hidden; the hidden native scrollbar remains the real control.

## Motion

- Scrollbars fade in while scrolling or on hover and fade out after hideDelay; the thumb thickens under the pointer.
- Pressing the track pages 90% of the viewport toward the pointer with smooth scrolling, or instantly under reduced motion.
- Reduced motion removes scrollbar transitions.

## Responsive behavior

- Touch keeps native momentum scrolling; the overlay bars only appear while scrolling.
- Thumbs only thicken on hover-capable fine pointers.
- Content changes and resizes are tracked with ResizeObserver and MutationObserver, so fades and thumbs stay correct as layouts reflow.

## Performance

- Fades and thumb positions are written straight to the DOM on each scroll event; scrolling never re-renders React.
- Horizontal wheel conversion only captures the wheel while there is room to scroll, so the page still scrolls at the ends.
- Edge fades use CSS mask-image, which is cheap but still composited; set fade={0} in very long lists if you see cost.

## Notes for AI

- Use it wherever content scrolls inside a fixed box: sidebars, panels, menus, horizontal card strips.
- Give vertical areas a height or maxHeight; the viewport scrolls only when it has a constrained size.
- Use onEdgeChange to show a "more below" affordance or to load more when bottom becomes false.

## Related

- [Resizable panels](https://uiarc.dev/components/resizable-panels/markdown): Trade space between panes by dragging the divider between them.
- [Carousel](https://uiarc.dev/components/carousel/markdown): Browse a row of slides by dragging, flicking, or arrowing through them.
- [Data grid](https://uiarc.dev/components/data-grid/markdown): A spreadsheet grid with range selection, inline editing, a fill handle, and animated sorting.
- [Tree view](https://uiarc.dev/components/tree-view/markdown): Navigate nested folders and structured content.

## Also in expand

- [Accordion](https://uiarc.dev/components/accordion/markdown): Progressively reveal supporting information in place.
- [Expandable card](https://uiarc.dev/components/expandable-card/markdown): Give a dense card more room when requested.
- [Expandable text](https://uiarc.dev/components/expandable-text/markdown): Long text clamped to a few lines with a soft fade that glides open with Read more and closes with Show less, shown only when the text overflows.

## Guidance for AI tools

Scroll area: A native scroll container with thin overlay scrollbars and edge fades that appear only when content overflows. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
