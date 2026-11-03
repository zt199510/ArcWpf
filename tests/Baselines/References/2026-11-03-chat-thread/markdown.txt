# Chat thread

> A chat thread with grouped messages, reactions, read receipts, typing, and a composer with attachments.

- Type: Component (data)
- Access: Free, open source
- Page: https://uiarc.dev/components/chat-thread
- Markdown: https://uiarc.dev/components/chat-thread/markdown
- Registry item: https://uiarc.dev/r/chat-thread.json
- Source file: `registry/components/chat-thread/chat-thread.tsx`
- Dependencies: motion, lucide-react
- Keywords: data, new, react chat, chat thread, messaging ui, support chat widget, chat bubbles, read receipts, typing indicator, message reactions, chat composer with attachments

## When to use

- Customer support or in-app messaging with read receipts and typing state.
- Team or DM threads that need reactions and image or file attachments.
- Comment threads on an object where replies arrive live.

## When not to use

- Use log-stream for streaming system or build output.
- Use timeline for an activity history rather than a two-way conversation.
- Use textarea or mention-input when you only need the input box inside your own layout.

## Installation

### CLI

Run one of these in a project set up with `shadcn init`:

```bash
npx shadcn@latest add @uiarc/chat-thread
pnpm dlx shadcn@latest add @uiarc/chat-thread
yarn dlx shadcn@latest add @uiarc/chat-thread
bunx --bun shadcn@latest add @uiarc/chat-thread
```

The `@uiarc` name needs `"registries": { "@uiarc": "https://uiarc.dev/r/{name}.json" }` in `components.json`. Without it, use the full URL:

```bash
npx shadcn@latest add https://uiarc.dev/r/chat-thread.json
```

### Manual

1. Install the dependencies:

```bash
npm install motion lucide-react
```

2. Copy the source into your project. Main file: `registry/components/chat-thread/chat-thread.tsx`

   The source is in the registry item: https://uiarc.dev/r/chat-thread.json

3. Arc imports use the `@/` alias for `registry/` and `lib/`. Keep the same folders or update the import paths.

## Usage

```tsx
import { ChatThread, type ChatMessage } from "@/components/arc/chat-thread/chat-thread";

const participants = [
  { id: "me", name: "Alex Rivera" },
  { id: "sam", name: "Sam Lee", avatar: "/avatars/sam.jpg" },
];

const messages: ChatMessage[] = [
  { id: "m1", authorId: "sam", text: "Is the build green?", createdAt: "2026-09-23T09:12:00Z" },
  { id: "m2", authorId: "me", text: "Yes, shipping now.", createdAt: "2026-09-23T09:13:00Z", status: "delivered" },
];

export function SupportChat() {
  return (
    <div style={{ height: 520 }}>
      <ChatThread
        participants={participants}
        currentUserId="me"
        defaultMessages={messages}
        readBy={{ sam: "m2" }}
        typing={["sam"]}
        onSend={draft => console.log(draft.text, draft.files)}
      />
    </div>
  );
}
```

## API reference

### ChatThread

A chat or support thread with grouped messages, day separators, reactions, read receipts, a typing indicator, and a composer. It stays pinned to the newest message and shows a pill when new messages land out of view.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `participants` (required) | `ChatParticipant[]` | – | Everyone in the thread: id, name, and optional avatar URL. Initials show without an avatar. |
| `currentUserId` (required) | `string` | – | Id of the viewer. Their messages sit on the right in the accent color. |
| `messages` | `ChatMessage[]` | – | Controlled messages, oldest first. Leave it out to let the thread keep its own list. |
| `defaultMessages` | `ChatMessage[]` | – | Initial messages when uncontrolled. |
| `typing` | `string[]` | `[]` | Participant ids currently typing. The current user is ignored. |
| `readBy` | `Record<string, string>` | – | The last message each participant has read, by participant id. Their avatar sits under that message and glides as it moves. |
| `onSend` | `(draft: ChatDraft) => void \| Promise<void>` | – | Called with the composer's text and files. Uncontrolled threads also append the message themselves with status sent. |
| `onReact` | `(messageId: string, emoji: string) => void` | – | Called when a reaction is picked or toggled. Uncontrolled threads update counts themselves. |
| `onRetry` | `(messageId: string) => void` | – | Shows a Retry button under the last own message when its status is failed. |
| `reactions` | `string[]` | `["👍", "❤️", "😂", "🎉", "👀", "🙏"]` | Emoji offered by the reaction picker. |
| `placeholder` | `string` | `"Message"` | Composer placeholder, also its accessible name. |
| `composer` | `boolean` | `true` | Show the composer. Turn it off for a read-only transcript. |
| `allowAttachments` | `boolean` | `true` | Allow files by button, paste, or drop in the composer. |
| `accept` | `string` | – | File types the attach button offers, as for <input accept>. |
| `groupWindow` | `number` | `300000` | Messages from one person closer together than this, in ms, share a group. Defaults to five minutes. |
| `locale` | `string` | `"en-US"` | Locale for times and day labels. |
| `label` | `string` | `"Conversation"` | Accessible name of the message log. |
| `labels` | `ChatThreadLabels` | – | Words for localization: sending, sent, delivered, read, failed, retry, today, yesterday, jumpToLatest, unknownAuthor, addReaction, reactions, and the composer's send, attach and attachments. Words with a count or names are functions: readBy, typing, newMessages, reactWith, reactionCount and removeFile. Any key left out keeps its English default; today and yesterday default to the locale's own words. |
| `className` | `string` | – | Extra class on the root. |

### ChatThreadHandle

Methods on the ref.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `scrollToBottom` | `(smooth?: boolean) => void` | – | Scrolls to the newest message and pins the thread there. Smooth by default, instant with reduced motion. |
| `focusComposer` | `() => void` | – | Focuses the composer textarea. |

### ChatComposer

The message box on its own: autosizing text up to six lines, attachments by button, paste, or drop, and a send button that lifts its arrow on send.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `onSend` (required) | `(draft: ChatDraft) => void` | – | Called with trimmed text and the attached files. The composer clears itself after. |
| `placeholder` | `string` | `"Message"` | Placeholder and accessible name of the textarea. |
| `allowAttachments` | `boolean` | `true` | Show the attach button and accept pasted or dropped files, up to ten at a time. |
| `accept` | `string` | – | File types the attach button offers. |
| `disabled` | `boolean` | – | Disables the textarea and attach button. |
| `labels` | `ChatComposerLabels` | – | Words for localization: send, attach, attachments, and removeFile(name). Any key left out keeps its English default. |
| `className` | `string` | – | Extra class on the composer. |

### ChatMessage

Shape of one message.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `id` (required) | `string` | – | Stable id. Also the key used by readBy and onReact. |
| `authorId` (required) | `string` | – | Participant id of the author. |
| `text` | `string` | – | Message body, rendered as plain text with line breaks kept. |
| `createdAt` (required) | `Date \| string \| number` | – | Send time. Drives grouping, day separators, and the time label. |
| `attachments` | `ChatAttachment[]` | – | Images (kind image with url) show in a gallery of up to four with a +N tile; everything else shows as a download row. Pass width and height to reserve space for a single image. |
| `reactions` | `ChatReaction[]` | – | Emoji, count, and mine for reactions the viewer added. |
| `status` | `"sending" \| "sent" \| "delivered" \| "read" \| "failed"` | – | Delivery state of the viewer's own messages. Shown under their latest message only. |

### formatBytes

Formats a byte count as B, KB, or MB, as used on file rows.

| Prop | Type | Default | Description |
| --- | --- | --- | --- |
| `bytes` | `number` | `0` | Size in bytes. |

## Keyboard interactions

| Keys | Action |
| --- | --- |
| Enter | Sends the message from the composer. Ignored during IME composition. |
| Shift+Enter | Adds a line in the composer. |
| Tab | Reaches the add reaction button of each message; it shows on focus within the row. |
| Arrow Left / Arrow Right | Moves between emoji in an open reaction picker, wrapping at the ends. |
| Escape | Closes the reaction picker and returns focus to its button. |

## Accessibility

- Messages are an ordered list with role log, aria-live polite, and aria-relevant additions, so new messages are read out.
- Typing is announced in a separate status region, such as Sam is typing; the visual dots are aria-hidden.
- Reaction chips are toggle buttons with aria-pressed and labels like 👍 3, including you.
- Read receipts carry an aria-label listing who read the message; avatars are decorative.
- The picker focuses its first emoji on open and uses a roving tabindex.

## Motion

- New messages rise in from their own side with a small scale on a smooth spring, and the list glides up by the added height instead of jumping.
- Reader avatars share a layoutId, so a receipt travels to the newly read message.
- Reaction chips pop in, counts roll up or down, and the reaction row and attachment tray spring open.
- The send arrow lifts out and a new one rises in on each send; the jump to latest pill springs in from below.
- Reduced motion turns these into short fades, stops the typing dots, and scrolls to the bottom instantly.

## Responsive behavior

- Bubbles are capped at min(78%, 34rem); below 420px the content padding tightens to 12px and bubbles may reach 84%.
- On touch devices (hover none) the add reaction button stays visible at reduced opacity instead of waiting for hover.
- The reaction picker opens above the bubble unless that would pass the top of the scroller, then opens below.

## Performance

- Every message renders; there is no virtualization, so page older history in chunks past a few hundred messages.
- A ResizeObserver on the content keeps the list pinned and drives the glide; images take width and height to avoid layout shift.
- Uncontrolled sends create object URLs for attached files and never revoke them; controlled threads should upload and pass real URLs.

## Notes for AI

- Choose it for support widgets, DMs, and team chat. Use log-stream for machine output and timeline for dated events that are not a conversation.
- Give the parent a fixed height; the root is a flex column at height 100% with its own scroller.
- For a server-backed thread, pass messages and append an optimistic message with status sending in onSend, then update status or set failed and handle onRetry.
- Keep readBy in sync from your realtime source; only the furthest read message marks the viewer's latest message as Read.
- Use ChatComposer alone when messages render elsewhere, such as an AI chat.

## Related

- [Mention input](https://uiarc.dev/components/mention-input/markdown): A textarea with @people and #channel mentions that act as single tokens, with suggestions at the caret.
- [Reactions](https://uiarc.dev/components/reactions/markdown): Acknowledge a post in one tap, with counts that roll.
- [File dropzone](https://uiarc.dev/components/file-dropzone/markdown): A generous target for dropping one or more files.
- [Avatar](https://uiarc.dev/components/avatar/markdown): A compact identity marker for people and accounts.
- [Textarea](https://uiarc.dev/components/textarea/markdown): A multiline field for notes, descriptions, and longer text.

## Also in activity

- [Timeline](https://uiarc.dev/components/timeline/markdown): Follow what happened, newest first, grouped by day.
- [Comment thread](https://uiarc.dev/components/comment-thread/markdown): Threaded comments with replies, reactions, mentions, inline edit, and resolve.

## Guidance for AI tools

Chat thread: A chat thread with grouped messages, reactions, read receipts, typing, and a composer with attachments. Follow the declared prop types and do not invent props. Keep keyboard access, reduced motion support, and both light and dark themes intact when adapting it.

Full library index: https://uiarc.dev/llms.txt
