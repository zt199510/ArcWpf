# Skeleton

> Reserve space while content is still loading.

- Type: Component (feedback)
- Access: Free, open source
- Page: https://uiarc.dev/components/skeleton
- Markdown: https://uiarc.dev/components/skeleton/markdown
- Registry item: https://uiarc.dev/r/skeleton.json
- Source file: `registry/components/skeleton/skeleton.tsx`
- Dependencies: motion
- Keywords: loading, status, react skeleton, skeleton loader, loading placeholder, content placeholder, shimmer loading, skeleton screen

## When to use

- Loading states for content whose shape is known, like a profile or comment.
- Swapping a placeholder into real content with a crossfade and height spring.

## When not to use

- Use progress when you can report a percentage.
- Use empty-state when loading finished and there is nothing to show.
- Use text-shimmer for an AI thinking or status line.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/skeleton
pnpm dlx shadcn@latest add @uiarc/skeleton
yarn dlx shadcn@latest add @uiarc/skeleton
bunx --bun shadcn@latest add @uiarc/skeleton
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/skeleton.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/skeleton/skeleton.tsx`

   The source is in the registry item: https://uiarc.dev/r/skeleton.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Skeleton } from "@/components/arc/skeleton/skeleton";

export function Profile({ user }: { user?: User }) {
  return (
    <Skeleton avatar lines={2} loading={!user}>
      {user && <ProfileCard user={user} />}
    </Skeleton>
  );
}
```

## API reference

### Skeleton

A pulsing placeholder of text lines and an optional avatar that can crossfade into the loaded content.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `label` | `string` | `"Loading content"` | aria-label of the loading status. |
| `lines` | `number` | `3` | Number of text lines, clamped to 1-6. |
| `avatar` | `boolean` | `false` | Adds a round avatar placeholder. |
| `children` | `ReactNode` | – | Content to reveal once loading finishes. With children the placeholder crossfades into them. |
| `loading` | `boolean` | `true` | Keeps the placeholder visible while true. Only used with children. |
| `className` | `string` | – | Class on the outer element. |

## Accessibility

- The placeholder is role="status" with aria-busy and an aria-label; its shapes are aria-hidden.
- With children, the wrapper sets aria-busy while loading.

## Motion

- Blocks pulse in a staggered wave, each a beat after the one above.
- On load the placeholder fades out, content rises 4px into place, and the height springs from placeholder to content.
- Reduced motion stops the pulse and swaps with short fades and no height animation.

## Responsive behavior

- Lines are percentages of the container width, so the placeholder scales with its slot.
- It only draws text lines and an avatar; build custom shapes for grids or media.

## Performance

- The pulse is a CSS animation that stops under reduced motion.
- A ResizeObserver springs the height from placeholder to content; avoid hundreds of skeletons at once.

## Notes for AI

- Use while fetching content whose shape is known. Use progress when you can report a percentage, and empty-state when there is nothing to show.
- Wrap the real content as children and drive loading to get the crossfade; without children it renders only the placeholder.

## Related

- [Progress](https://uiarc.dev/components/progress/markdown): Show how much of a known task is complete.
- [Empty state](https://uiarc.dev/components/empty-state/markdown): A useful next step when there is nothing to show yet.
- [Card](https://uiarc.dev/components/card/markdown): A contained group of related content and actions.

## Also in progress

- [Stepper](https://uiarc.dev/components/stepper/markdown): Show where a person is in a multi-step flow and what is done.
- [Countdown](https://uiarc.dev/components/countdown/markdown): A launch countdown with rolling digits that morphs into a live state at zero.
- [Usage meter](https://uiarc.dev/components/usage-meter/markdown): Show what fills an allowance and how close it is to the limit.
- [Onboarding checklist](https://uiarc.dev/components/onboarding-checklist/markdown): Guide a new workspace through setup, one step at a time.

## Guidance for AI tools

Skeleton: Reserve space while content is still loading. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
