# Breadcrumb

> Show where a page sits in a hierarchy.

- Type: Component (disclosure)
- Access: Free, open source
- Page: https://uiarc.dev/components/breadcrumb
- Markdown: https://uiarc.dev/components/breadcrumb/markdown
- Registry item: https://uiarc.dev/r/breadcrumb.json
- Source file: `registry/components/breadcrumb/breadcrumb.tsx`
- Dependencies: motion, lucide-react
- Keywords: navigation, wayfinding, react breadcrumb, breadcrumb navigation, animated breadcrumb, page path, nextjs breadcrumb, hierarchy navigation

## When to use

- Showing where a page sits in a hierarchy, such as Workspace, Settings, Billing.
- Client-side paths like a file browser, where crumbs call onClick instead of navigating.
- Paths that grow as people drill in, where new crumbs should slide in.

## When not to use

- Use tree-view when people need to browse the whole hierarchy.
- Use tabs for switching between sibling views.
- Use pagination for moving through pages of a list.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/breadcrumb
pnpm dlx shadcn@latest add @uiarc/breadcrumb
yarn dlx shadcn@latest add @uiarc/breadcrumb
bunx --bun shadcn@latest add @uiarc/breadcrumb
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/breadcrumb.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/breadcrumb/breadcrumb.tsx`

   The source is in the registry item: https://uiarc.dev/r/breadcrumb.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { Breadcrumb } from "@/components/arc/breadcrumb/breadcrumb";

export function SettingsPath() {
  return (
    <Breadcrumb
      items={[
        { label: "Workspace", href: "/" },
        { label: "Settings", href: "/settings" },
        { label: "Billing" },
      ]}
    />
  );
}
```

## API reference

### Breadcrumb

A path of links where the last item is the current page. Crumbs added later slide in.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `items` (required) | `{ label: string; href?: string; onClick?: (event: MouseEvent<HTMLElement>) => void }[]` | – | Path from root to current page. Items with href render a plain a, or the link component set with ArcProvider (such as next/link or a locale-aware Link); items with only onClick render buttons. |
| `ariaLabel` | `string` | `"Breadcrumb"` | Label for the nav landmark. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Tab | Moves between crumb links and buttons. |
| Enter | Follows the focused crumb. |

## Accessibility

- Renders a nav landmark with an ordered list.
- The last item is a span with aria-current="page" and is never a link.
- Chevron separators are aria-hidden.

## Motion

- Crumbs present on first render stay still; new crumbs slide in 8px from the left out of a blur while siblings shift on a smooth spring.
- Reduced motion adds and removes crumbs without movement.

## Responsive behavior

- The list wraps onto new lines on narrow screens; individual crumbs never wrap internally.
- It does not truncate or collapse long paths, so keep labels short or shorten the path on mobile yourself.

## Performance

- Crumbs use layout position animation only when the path changes; static paths do not animate.

## Notes for AI

- Use for hierarchical page location. For client-side paths such as a file browser, pass onClick without href.
- Links are plain anchors by default. For client-side navigation, wrap the app once in ArcProvider from @/components/arc/lib/arc-provider with your router's Link: <ArcProvider link={Link}>.
- Keep labels short; the component does not truncate or collapse long paths.

## Related

- [Tree view](https://uiarc.dev/components/tree-view/markdown): Navigate nested folders and structured content.
- [Pagination](https://uiarc.dev/components/pagination/markdown): Move through a long collection with clear bounds.
- [Tabs](https://uiarc.dev/components/tabs/markdown): Switch between related content in the same context.

## Also in navigation

- [Mega menu morph](https://uiarc.dev/components/mega-menu-morph/markdown): A site header whose menus share one floating panel. Moving between sections morphs its size, slides the next menu in from that side and glides the caret under the new trigger, with hover intent, a safe triangle and an accordion sheet on narrow screens.

## Guidance for AI tools

Breadcrumb: Show where a page sits in a hierarchy. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt

