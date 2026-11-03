# Comment thread

> Threaded comments with replies, reactions, mentions, inline edit, and resolve.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/comment-thread
- Markdown: https://uiarc.dev/components/comment-thread/markdown
- Registry item: https://uiarc.dev/r/comment-thread.json
- Source file: `registry/components/comment-thread/comment-thread.tsx`
- Dependencies: motion, lucide-react
- Keywords: data, new, react comment thread, threaded comments, comment replies, resolve thread, mentions in comments, comment reactions, review comments component

## When to use

- Review threads on documents, designs, tickets, or pull requests.
- Discussions that need replies, reactions, mentions, and a resolved state.
- Sidebars of feedback attached to one object.

## When not to use

- Use chat-thread for real-time back-and-forth conversation.
- Use ai-chat for assistant conversations with streaming replies.
- Use mention-input alone when you only need a composer.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/comment-thread
pnpm dlx shadcn@latest add @uiarc/comment-thread
yarn dlx shadcn@latest add @uiarc/comment-thread
bunx --bun shadcn@latest add @uiarc/comment-thread
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/comment-thread.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/comment-thread/comment-thread.tsx`

   The source is in the registry item: https://uiarc.dev/r/comment-thread.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { CommentThread, type ThreadComment } from "@/components/arc/comment-thread/comment-thread";

export function HeadlineDiscussion({ initial, me }: { initial: ThreadComment[]; me: { id: string; name: string; avatar?: string } }) {
  return (
    <CommentThread
      title="Hero headline"
      currentUser={me}
      defaultComments={initial}
      onCommentsChange={(_, event) => syncComment(event)}
      onResolvedChange={resolved => setThreadResolved("hero", resolved)}
    />
  );
}
```

## API reference

### CommentThread

A threaded discussion: nested replies that collapse, reactions with rolling counts, in-place edit and delete, @mention autocomplete, and resolving that folds the thread into a chip.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `comments` | `ThreadComment[]` | – | The comment tree (controlled). |
| `defaultComments` | `ThreadComment[]` | `[]` | Initial tree when uncontrolled. |
| `onCommentsChange` | `(comments: ThreadComment[], event: CommentThreadEvent) => void` | – | Called with the full next tree and what changed: reply, edit, delete, or react. |
| `currentUser` (required) | `CommentAuthor` | – | The person writing. Their comments can be edited and deleted. |
| `people` | `CommentAuthor[]` | – | People who can be mentioned. Defaults to everyone in the thread. |
| `resolved` | `boolean` | – | Resolved state (controlled). |
| `defaultResolved` | `boolean` | `false` | Initial resolved state. |
| `onResolvedChange` | `(resolved: boolean) => void` | – | Called on Resolve and Reopen. |
| `title` | `ReactNode` | – | What the thread is about, shown in the header, such as "Hero headline". |
| `reactions` | `string[]` | `["👍", "❤️", "🎉", "👀", "🚀", "✅"]` | Emoji offered by the reaction picker. |
| `placeholder` | `string` | – | Composer placeholder. |
| `maxDepth` | `number` | `2` | Replies deeper than this attach to the deepest allowed parent. |
| `nowLabel` | `string` | – | createdAt label for comments written now. |
| `labels` | `CommentThreadLabels` | – | Words for localization, every visible word and accessible name: reply, edit, delete, cancel, send, save, resolve, resolved, reopen, empty, edited, deleteConfirm, deletedText, deletedComment, editComment, hideReplies, cancelReply, addReaction, closeReactions, reactions, mentions and thread. Words with a count or a name are functions: commentCount, peopleCount, showReplies, replyingTo, reactWith and reactionCount. Any key left out keeps its English default. |
| `className` | `string` | – | Extra class on the section. |
| `ref` | `Ref<HTMLElement>` | – | The section element. |

### ThreadComment

One comment in the tree.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `id` (required) | `string` | – | Stable id. |
| `author` (required) | `{ id: string; name: string; avatar?: string }` | – | Author. Initials show without an avatar. |
| `body` (required) | `string` | – | Plain text. "@Full Name" mentions of known people are highlighted. |
| `createdAt` (required) | `string` | – | Display label such as "2h" or "Sep 18". |
| `edited` | `boolean` | – | Shows an edited mark. |
| `deleted` | `boolean` | – | A deleted comment with replies stays as a quiet placeholder. |
| `reactions` | `{ emoji: string; users: string[] }[]` | – | Reactions and who gave them. |
| `replies` | `ThreadComment[]` | – | Nested replies. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| ⌘ + Enter / Ctrl + Enter | Sends the composer. |
| @ | Opens mention suggestions in the composer. |
| ArrowUp / ArrowDown | Move through mention suggestions. |
| Enter / Tab | Insert the active mention. |
| Escape | Closes suggestions, then cancels an edit or reply; also closes the reaction picker or delete confirmation. |

## Accessibility

- The composer is a combobox with a listbox of people and aria-activedescendant.
- Each comment is an article labelled with author and time; deleted ones read "Deleted comment".
- Reaction chips are toggle buttons with aria-pressed and a label like "👍 3, including you".
- Reply toggles expose aria-expanded; icon-only actions have specific labels.

## Motion

- The thread height springs when replies expand, comments arrive, or the thread folds into the resolved chip.
- Reaction counts roll to the new value; chips move with layout animation as they are added or removed.
- Edit, confirm, and picker rows slide in from the side.
- Reduced motion swaps content without travel and turns off transitions.

## Responsive behavior

- Below 420px the composer hides its avatar and nested replies indent less (12px).
- Hover styles only apply on fine pointers; every action is a visible button on touch.

## Performance

- Each change rebuilds the tree immutably and renders every comment; paginate or collapse very long threads.
- Reply nesting is capped by maxDepth, which keeps indentation and render depth bounded.

## Notes for AI

- Choose it for comments attached to a document, design, or record. Persist changes with the event passed to onCommentsChange rather than diffing trees.
- currentUser decides which comments show Edit and Delete; enforce the same rule on your server.
- Deleting a comment with replies keeps a placeholder so the thread structure survives.

## Related

- [Chat thread](https://uiarc.dev/components/chat-thread/markdown): A chat thread with grouped messages, reactions, read receipts, typing, and a composer with attachments.
- [Mention input](https://uiarc.dev/components/mention-input/markdown): A textarea with @people and #channel mentions that act as single tokens, with suggestions at the caret.
- [Reactions](https://uiarc.dev/components/reactions/markdown): Acknowledge a post in one tap, with counts that roll.
- [Avatar](https://uiarc.dev/components/avatar/markdown): A compact identity marker for people and accounts.
- [Inline edit](https://uiarc.dev/components/inline-edit/markdown): Rename in place: the text becomes a field without moving.

## Also in activity

- [Timeline](https://uiarc.dev/components/timeline/markdown): Follow what happened, newest first, grouped by day.

## Guidance for AI tools

Comment thread: Threaded comments with replies, reactions, mentions, inline edit, and resolve. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
