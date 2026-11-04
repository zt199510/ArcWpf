# Text reveal

> Reveal a short piece of content with restrained motion.

- Type: Component (text)
- Access: Free, open source
- Page: https://uiarc.dev/components/text-reveal
- Markdown: https://uiarc.dev/components/text-reveal/markdown
- Registry item: https://uiarc.dev/r/text-reveal.json
- Source file: `registry/components/text-reveal/text-reveal.tsx`
- Keywords: text, motion, content, react text reveal, headline animation, hero text animation, word by word reveal, blur text reveal, css text animation

## When to use

- Above-the-fold hero headlines that animate on page load.
- Landing page intros where text must appear on first paint even if scripts are slow.

## When not to use

- Use in-view-title for section titles further down the page.
- Use scroll-highlight for a key paragraph that reveals with scroll.
- Use word-rotate when one word in the headline should cycle.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/text-reveal
pnpm dlx shadcn@latest add @uiarc/text-reveal
yarn dlx shadcn@latest add @uiarc/text-reveal
bunx --bun shadcn@latest add @uiarc/text-reveal
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/text-reveal.json
```

### Manual

1. No packages are needed beyond React and Next.js.

2. Copy the source into your project. Main file: `registry/components/text-reveal/text-reveal.tsx`

   The source is in the registry item: https://uiarc.dev/r/text-reveal.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { TextReveal } from "@/components/arc/text-reveal/text-reveal";

export function Hero() {
  return <TextReveal as="h1" text={"Ship interfaces\nthat feel precise"} delay={0.1} />;
}
```

## API reference

### TextReveal

Reveals a headline once on mount: each word rises out of its own clip while it sharpens from a soft blur. Runs in CSS, so it starts on first paint.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `text` (required) | `string` | – | The copy to reveal. Use \n for a deliberate line break. |
| `as` | `"h1" \| "h2" \| "h3" \| "p"` | `"h2"` | Rendered element. p uses a softer blur. |
| `delay` | `number` | `0` | Seconds before the first word rises. |
| `className` | `string` | – | Merged onto the rendered element. |
| `id` | `string` | – | Forwarded to the rendered element, e.g. for aria-labelledby. |

## Accessibility

- A heading carries the full text as its aria-label and the animated words are aria-hidden, so screen readers and crawlers read one clean sentence. A p keeps a visually hidden copy instead, since a paragraph cannot take a label.
- Line breaks are read as spaces.
- Pick the heading level with as so the page outline stays correct.

## Motion

- Words rise out of a clip and sharpen from blur on a per-word stagger; total stagger is capped so long text never drags.
- The entrance is pure CSS, so text is never left hidden when scripts load slowly.
- Reduced motion drops the clip and rise and fades the words in quickly.

## Responsive behavior

- The element caps at 24ch with balanced wrapping, so lines stay even on any width.
- Use \n only for breaks that work on every width, since forced breaks apply on mobile too.

## Performance

- Pure CSS keyframes, no JavaScript animation or observers.
- Total stagger is capped, so long text does not lengthen the entrance; each word still gets its own blur filter.

## Notes for AI

- Use for above-the-fold headlines that should animate on page load. For titles further down the page use in-view-title; for body copy worth slowing down on use scroll-highlight.
- It plays once per mount. Change the element's key to replay it.

## Related

- [In-view title](https://uiarc.dev/components/in-view-title/markdown): Bring a section title in as it scrolls into view.
- [Text morph](https://uiarc.dev/components/text-morph/markdown): Morph a label into its next state, letter by letter.

## Also in text effects

- [Text shimmer](https://uiarc.dev/components/text-shimmer/markdown): Show ongoing work with a calm light across the words.

## Guidance for AI tools

Text reveal: Reveal a short piece of content with restrained motion. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
