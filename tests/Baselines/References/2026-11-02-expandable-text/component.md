# Expandable text

> Long text clamped to a few lines with a soft fade that glides open with Read more and closes with Show less, shown only when the text overflows.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/expandable-text
- Markdown: https://uiarc.dev/components/expandable-text/markdown
- Registry item: https://uiarc.dev/r/expandable-text.json
- Source file: `registry/components/expandable-text/expandable-text.tsx`
- Dependencies: motion, lucide-react
- Keywords: disclosure, text, motion, new, read more, show more, show less, truncate, line clamp, clamp, collapse text, expand text, see more, review, description, fade out text

## When to use

- A list of user reviews or comments where some entries are long.
- A product, listing, or profile description that should not push the rest of the page down.
- Any card where long text would make the cards uneven.

## When not to use

- Use accordion for a set of titled sections or an FAQ.
- Use expandable-card when the hidden part is structured content rather than running text.
- Use scroll-area when the full text must stay in a fixed height box.
- Do not clamp content people must read, such as legal terms or error details.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/expandable-text
pnpm dlx shadcn@latest add @uiarc/expandable-text
yarn dlx shadcn@latest add @uiarc/expandable-text
bunx --bun shadcn@latest add @uiarc/expandable-text
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/expandable-text.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/expandable-text/expandable-text.tsx`

   The source is in the registry item: https://uiarc.dev/r/expandable-text.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ExpandableText } from "@/components/arc/expandable-text/expandable-text";

export function ReviewBody({ review }: { review: Review }) {
  return (
    <ExpandableText lines={2}>
      {review.paragraphs.map(paragraph => <p key={paragraph}>{paragraph}</p>)}
    </ExpandableText>
  );
}
```

## API reference

### ExpandableText

Long text clamped to a few lines with a soft fade over the last one. Read more opens it smoothly to its full height and Show less closes it again. The toggle only appears when the text really overflows.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `children` (required) | `ReactNode` | – | The text. Strings, paragraphs, and inline links all work. The whole text stays in the accessibility tree while clamped. |
| `lines` | `number` | `3` | Lines shown while collapsed, measured in the text's own line height. |
| `expanded` | `boolean` | – | Controlled open state. |
| `defaultExpanded` | `boolean` | `false` | Starts open when uncontrolled. |
| `onExpandedChange` | `(expanded: boolean) => void` | – | Called when the toggle, or focus moving to a hidden link, opens or closes the text. |
| `moreLabel` | `string` | `"Read more"` | Toggle label while collapsed. |
| `lessLabel` | `string` | `"Show less"` | Toggle label while open. |
| `...props` | `HTMLAttributes<HTMLDivElement>` | – | Passed to the root element, including className and style. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Moves to links inside the text, then to the toggle. Focusing a link hidden below the clamp opens the text. |
| Enter / Space | Opens or closes the text from the toggle. |

## Accessibility

- The clamp is visual only: the full text stays in the accessibility tree, so screen readers read all of it without pressing the toggle.
- The toggle is a native button with aria-expanded and aria-controls pointing at the text.
- The two labels crossfade in one grid cell; the hidden one is aria-hidden, so the button's name is always just the current label.
- Links or buttons inside the clamped part open the text when they receive keyboard focus, so focus is never hidden.

## Motion

- Height, the fade mask, and the chevron all follow one progress value, so they always move in step and a click mid-way reverses from the current height.
- Opening takes 500 to 600ms and closing 400 to 450ms, growing gently with the distance, on a soft ease out (0.32, 0.5, 0.2, 1) that answers at once and spends most of its time settling. It never overshoots.
- The fade over the last line dissolves as the text opens and only returns as it nears the clamp. While moving, newly revealed lines rise through the soft edge instead of being uncovered by a hard line.
- Read more and Show less crossfade with a short drift and a brief blur, without changing the button's width.
- Closing from far down the page scrolls the start of the text back into view together with the collapse.
- At rest the clamp is plain CSS (max-height in lh units), so width and font changes need no JavaScript. With reduced motion the text opens and closes at once.

## Responsive behavior

- The clamp is measured in lines, so narrower screens show the same number of lines with fewer words; the toggle appears or disappears as wrapping changes.
- The toggle sits under the text, left aligned, with a 28px tall hit area that works on touch.

## Performance

- One ResizeObserver per instance, watching the text and a zero width probe that is exactly the clamp height.
- Only height and a mask gradient change while moving; at rest there is no inline height and, once open, no mask.
- The server renders the toggle, assuming long text; short text drops it on the first measurement.

## Notes for AI

- Use it for reviews, product descriptions, bios, comments, and release notes where the first lines are enough for most readers.
- Pick lines by context: 2 in dense lists such as reviews, 3 to 4 for a product or profile description.
- Keep one line height inside the text. Headings or large images inside break the line based clamp; use expandable-card or accordion for structured content.
- Nothing needs measuring by the caller: a ResizeObserver decides whether the toggle is shown and keeps that correct as the layout changes.

## Related

- [Expandable card](https://uiarc.dev/components/expandable-card/markdown): Give a dense card more room when requested.
- [Accordion](https://uiarc.dev/components/accordion/markdown): Progressively reveal supporting information in place.
- [Scroll area](https://uiarc.dev/components/scroll-area/markdown): A native scroll container with thin overlay scrollbars and edge fades that appear only when content overflows.

## Also in expand

- [Resizable panels](https://uiarc.dev/components/resizable-panels/markdown): Trade space between panes by dragging the divider between them.

## Guidance for AI tools

Expandable text: Long text clamped to a few lines with a soft fade that glides open with Read more and closes with Show less, shown only when the text overflows. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
