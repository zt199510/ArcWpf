# Avatar

> A compact identity marker for people and accounts.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/avatar
- Markdown: https://uiarc.dev/components/avatar/markdown
- Registry item: https://uiarc.dev/r/avatar.json
- Source file: `registry/components/avatar/avatar.tsx`
- Dependencies: motion
- Keywords: identity, people, react avatar, avatar component, user avatar with initials, profile picture, avatar with status, online indicator avatar

## When to use

- Showing one person next to their name, comment, or record.
- Presence in a header or list, via the online or offline status dot.
- Places where a photo may be missing and initials should stand in.

## When not to use

- Use avatar-group for several people with an overflow count.
- Use user-menu when the avatar is the trigger for account actions.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/avatar
pnpm dlx shadcn@latest add @uiarc/avatar
yarn dlx shadcn@latest add @uiarc/avatar
bunx --bun shadcn@latest add @uiarc/avatar
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/avatar.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion
```

2. Copy the source into your project. Main file: `registry/components/avatar/avatar.tsx`

   The source is in the registry item: https://uiarc.dev/r/avatar.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Avatar } from "@/components/arc/avatar/avatar";

export function Owner() {
  return <Avatar name="Maya Chen" src="/people/maya.jpg" size="lg" status="online" />;
}
```

## API reference

### Avatar

A round portrait that falls back to initials and can show an online or offline dot.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `name` (required) | `string` | – | Full name. Used for the accessible label and the first two initials. |
| `src` | `string` | – | Image URL. Renders a plain img, or the image component set with ArcProvider (such as next/image). Falls back to initials if it fails to load. |
| `size` | `"sm" \| "md" \| "lg" \| "xl"` | `"md"` | Diameter, from 28px to 88px. |
| `status` | `"online" \| "offline"` | – | Adds a presence dot and appends the status to the label. |
| `...props` | `HTMLAttributes<HTMLSpanElement>` | – | Forwarded to the root span, including className. |

## Accessibility

- The root is role="img" with an aria-label of the name plus status, such as "Maya Chen, online".
- The image and initials are decorative, so the name is read once.
- The status dot is hidden from assistive tech; the label carries the state.

## Motion

- A photo still loading fades in from a soft blur; a cached one shows at once.
- The status dot pops in and out on a snappy spring.
- Reduced motion swaps the dot instantly and drops the image fade.

## Responsive behavior

- Size is fixed by the size prop, from 28px to 88px, and never changes by breakpoint.
- It passes a sizes hint per size, so with next/image (through ArcProvider) phones never download a larger photo than the circle shows.

## Performance

- The photo is a plain lazy img by default. Pass next/image through ArcProvider to optimize it; remote hosts then need next.config images settings.
- A cached photo shows at once; only a still-loading photo runs the short blur fade.

## Notes for AI

- Use for a single person. For a row of people with an overflow count use avatar-group.
- Renders a plain img, so any host works in any React app. To use next/image or your own image component, wrap the app once in ArcProvider from @/components/arc/lib/arc-provider: <ArcProvider image={Image}>. With next/image, remote hosts must be allowed in next.config images.

## Related

- [Avatar group](https://uiarc.dev/components/avatar-group/markdown): Show a team or set of contributors in a small space.
- [User menu](https://uiarc.dev/components/user-menu/markdown): Your account, settings, theme, and sign out behind the avatar. Opens as a bottom sheet on phones.
- [Card](https://uiarc.dev/components/card/markdown): A contained group of related content and actions.
- [Timeline](https://uiarc.dev/components/timeline/markdown): Follow what happened, newest first, grouped by day.

## Also in avatars

- [Badge](https://uiarc.dev/components/badge/markdown): A small label for status, category, or metadata.
- [Presence stack](https://uiarc.dev/components/presence-stack/markdown): Avatars of the people here now: joins pop in as the stack reflows, a counter rolls past the limit, dots show typing and idle, and hover fans it out with a gliding name label.

## Guidance for AI tools

Avatar: A compact identity marker for people and accounts. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
