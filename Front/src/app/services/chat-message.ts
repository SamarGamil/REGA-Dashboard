export interface ChatMessage {
  id: string;
  role: 'user' | 'assistant';
  text: string;
}

/**
 * Converts the engine's markdown-ish text into safe HTML for assistant bubbles:
 * HTML is escaped first (defence against injected/steered markup), then **bold** ->
 * <strong> and literal "\n" escapes -> real newlines (rendered with whitespace-pre-wrap).
 */
export function formatAssistantText(text: string): string {
  const escaped = text
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
  return escaped
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/\\n/g, '\n');
}
