# Text morph

> Morph a label into its next state, letter by letter.

- Type: Component (text)
- Access: Free, open source
- Page: https://uiarc.dev/components/text-morph
- Markdown: https://uiarc.dev/components/text-morph/markdown
- Registry item: https://uiarc.dev/r/text-morph.json
- Source file: `registry/components/text-morph/text-morph.tsx`
- Dependencies: motion
- Keywords: text, motion, status, react text morph, morphing text, animated label, text transition, letter morph animation, shared letter animation

## When to use

- Status words that change a few letters at a time, such as Follow and Following.
- Custom buttons or badges that need a morphing label without the rest of the button.
- Inline counters or short state labels that should glide instead of jump.

## When not to use

- Use word-rotate for a sentence with one cycling word.
- Use button or action-swap when you want the morph plus button behaviour.
- Avoid it for text that wraps, since it always renders a single line.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/text-morph
pnpm dlx shadcn@latest add @uiarc/text-morph
yarn dlx shadcn@latest add @uiarc/text-morph
bunx --bun shadcn@latest add @uiarc/text-morph
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/text-morph.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/text-morph/text-morph.tsx`

   The source is in the registry item: https://uiarc.dev/r/text-morph.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { TextMorph } from "@/components/arc/text-morph/text-morph";

export function PublishButton({ state }: { state: "idle" | "busy" | "done" }) {
  const label = state === "idle" ? "Publish" : state === "busy" ? "Publishing" : "Published";
  return (
    <button type="button">
      <TextMorph>{label}</TextMorph>
    </button>
  );
}
```

## API reference

### TextMorph

Morphs one short label into the next in place. Shared letters glide, new ones sharpen in, removed ones blur away, and the width springs to fit.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `children` (required) | `string` | – | The current label. Change it to morph. |
| `as` | `"span" \| "div" \| "p" \| "strong" \| "h1" \| "h2" \| "h3"` | `"span"` | Rendered element. |
| `className` | `string` | – | Applied to the rendered element. |
| `id` | `string` | – | Forwarded to the rendered element. |

## Accessibility

- The plain label is in a visually hidden span; the animated glyphs are aria-hidden.
- It does not announce changes. Wrap it in your own aria-live region if a status change must be announced.

## Motion

- Letters are matched by character and occurrence, so shared letters glide to their new positions on a spring while new letters stagger in with a blur.
- The frame width springs to the new label; font loading or resizes follow immediately without animating.
- Reduced motion swaps the label and width instantly.

## Responsive behavior

- The frame width springs to each label, and a ResizeObserver follows font loading and container resizes without animating.
- The label never wraps, so keep it short in narrow layouts.

## Performance

- Every character is a motion span with layout animation; keep labels to a few words.

## Notes for AI

- Use for status words that change a few letters at a time (Follow / Following, Save / Saved). For a sentence with a cycling word use word-rotate.
- Keep labels to one short line; it never wraps.

## Related

- [Button](https://uiarc.dev/components/button/markdown): A clear, responsive action with quiet secondary states.
- [Action swap](https://uiarc.dev/components/action-swap/markdown): Swap an action label while preserving its place and intent.
- [Text shimmer](https://uiarc.dev/components/text-shimmer/markdown): Show ongoing work with a calm light across the words.

## Also in text effects

- [Text reveal](https://uiarc.dev/components/text-reveal/markdown): Reveal a short piece of content with restrained motion.
- [In-view title](https://uiarc.dev/components/in-view-title/markdown): Bring a section title in as it scrolls into view.

## Guidance for AI tools

Text morph: Morph a label into its next state, letter by letter. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
