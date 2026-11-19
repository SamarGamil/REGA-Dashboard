/**
 * Terminal-style input history for the assistant chat: ArrowUp recalls older entries,
 * ArrowDown walks back toward the unsent draft. Typing ends the browsing session.
 */
export class InputHistory {
  private entries: string[] = [];
  private index: number | null = null; // null = not browsing
  private draft = '';

  get browsing(): boolean {
    return this.index !== null;
  }

  /** Records a sent entry (consecutive duplicates collapse, blanks are ignored) and stops browsing. */
  push(entry: string): void {
    const trimmed = entry.trim();
    if (trimmed && this.entries[this.entries.length - 1] !== trimmed) this.entries.push(trimmed);
    this.index = null;
    this.draft = '';
  }

  /**
   * The next older entry (ArrowUp); the current input is kept as the draft when browsing starts.
   * Stays on the oldest entry at the end. Null when there is no history at all.
   */
  prev(current: string): string | null {
    if (this.entries.length === 0) return null;
    if (this.index === null) {
      this.draft = current;
      this.index = this.entries.length - 1;
    } else if (this.index > 0) {
      this.index -= 1;
    }
    return this.entries[this.index];
  }

  /** The next newer entry (ArrowDown); stepping past the newest restores the draft and stops browsing. Null when not browsing. */
  next(): string | null {
    if (this.index === null) return null;
    if (this.index < this.entries.length - 1) {
      this.index += 1;
      return this.entries[this.index];
    }
    this.index = null;
    const draft = this.draft;
    this.draft = '';
    return draft;
  }

  /** A manual edit ends browsing; the edited text becomes the new draft on the next ArrowUp. */
  stopBrowsing(): void {
    this.index = null;
  }
}
