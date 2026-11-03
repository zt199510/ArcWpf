# Carousel

> Browse a row of slides by dragging, flicking, or arrowing through them.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/carousel
- Markdown: https://uiarc.dev/components/carousel/markdown
- Registry item: https://uiarc.dev/r/carousel.json
- Source file: `registry/components/carousel/carousel.tsx`
- Dependencies: motion, lucide-react
- Keywords: carousel, slider, gallery, react carousel, image slider, swipe carousel, autoplay carousel, accessible carousel, slideshow component

## When to use

- A short set of peer items where neighbouring slides hint at more.
- Galleries browsed by drag, flick, trackpad swipe, or keys.
- Rotating highlights with a visible pause control, via interval.

## When not to use

- Use cover-flow for a showpiece gallery and photo-grid to show everything at once.
- Use card-stack for deciding on items one at a time.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/carousel
pnpm dlx shadcn@latest add @uiarc/carousel
yarn dlx shadcn@latest add @uiarc/carousel
bunx --bun shadcn@latest add @uiarc/carousel
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/carousel.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/carousel/carousel.tsx`

   The source is in the registry item: https://uiarc.dev/r/carousel.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Carousel } from "@/components/arc/carousel/carousel";

export function Suites() {
  return (
    <Carousel label="Harbour suites" interval={5000}>
      <img src="/suite-1.jpg" alt="Sea view suite" />
      <img src="/suite-2.jpg" alt="Garden suite" />
      <img src="/suite-3.jpg" alt="Loft suite" />
    </Carousel>
  );
}
```

## API reference

### Carousel

A centered slide row browsed by drag, flick, trackpad swipe, arrow keys, or pill indicators, with optional rotation.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` (required) | `string` | – | Names the carousel for assistive technology. |
| `children` (required) | `ReactNode` | – | One child per slide. |
| `index` | `number` | – | Controlled active slide. Pair with onIndexChange. |
| `defaultIndex` | `number` | `0` | Initial slide when uncontrolled. |
| `onIndexChange` | `(index: number) => void` | – | Called when the active slide changes. |
| `slideSize` | `string` | `"min(80cqw, 340px)"` | Width of one slide as a CSS length. |
| `slideLabel` | `(index: number, count: number) => string` | `"2 of 5"` | Names each slide and its indicator. |
| `interval` | `number` | – | Milliseconds per slide. Adds a play control for rotation. |
| `autoplay` | `boolean` | `false` | Starts rotating on mount. |
| `className` | `string` | – | Class for the root section. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ArrowLeft / ArrowRight | Moves to the previous or next slide. |
| Home / End | Jumps to the first or last slide. |
| Tab | Moves between the slide area, play control, active indicator, and arrows. |

## Accessibility

- The root is a section with aria-roledescription="carousel"; slides are labelled tabpanels and the indicators a role="tablist".
- Inactive slides are inert, so their links are not focusable.
- Slide changes are announced in a polite live region.
- Rotation pauses on hover, keyboard focus, and drag, and has a visible pause control.

## Motion

- Slides follow the finger and settle with momentum; neighbors shrink and dim with distance.
- The indicator stretches into a pill that tracks the slides, and drains to show rotation time.
- Reduced motion jumps between slides and disables rotation.

## Responsive behavior

- Slides default to min(80cqw, 340px), so they size to the carousel's container rather than the viewport.
- Controls tighten when the container is under 340px.

## Performance

- All slides stay mounted; inactive ones are inert, so keep the set short.
- Rotation pauses offscreen via IntersectionObserver and in hidden tabs via visibilitychange.

## Notes for AI

- Use for a short set of peer items where neighbors should hint at more. Use cover-flow for a showpiece gallery and photo-grid to show everything at once.
- Give each slide a stable key if the list can change, and write a specific label.
- Keep autoplay off unless rotation is essential.

## Related

- [Cover flow](https://uiarc.dev/components/cover-flow/markdown): A depth rail of images you can throw, with soft grounded shadows and a quiet reflection.
- [Photo grid](https://uiarc.dev/components/photo-grid/markdown): Pinch through zoom levels and open any photo straight from its cell.
- [Card stack](https://uiarc.dev/components/card-stack/markdown): Review a deck one card at a time, with a throw and an undo.
- [Image compare](https://uiarc.dev/components/image-compare/markdown): Drag a divider across two images to see what changed.

## Guidance for AI tools

Carousel: Browse a row of slides by dragging, flicking, or arrowing through them. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
