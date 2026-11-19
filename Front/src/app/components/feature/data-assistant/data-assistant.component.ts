import { ChangeDetectionStrategy, Component, ElementRef, effect, inject, signal, viewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AssistantService } from '../../../services/assistant.service';
import { AssistantChatService } from '../../../services/assistant-chat.service';
import { ProductService } from '../../../services/product.service';
import { PortalPeriodService } from '../../../services/portal-period.service';
import { ChatMessage, formatAssistantText } from '../../../services/chat-message';
import { InputHistory } from '../../../utils/input-history';

@Component({
  selector: 'app-data-assistant',
  standalone: true,
  imports: [CommonModule, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (assistant.isOpen()) {
      <div class="fixed inset-0 z-[60] bg-black/40" (click)="assistant.close()"></div>
      <aside class="siri-panel fixed inset-y-0 right-0 z-[70] w-full max-w-[380px] flex flex-col text-white" dir="rtl"
        (keydown.escape)="assistant.close()">
        <header class="shrink-0 flex items-center justify-between px-5 h-16 border-b border-white/10">
          <div class="flex items-center gap-3">
            <div class="relative w-8 h-8 flex items-center justify-center">
              <div class="siri-welcome-orb absolute inset-0"></div>
              <div class="siri-welcome-orb-inner absolute inset-[2px]"></div>
              <i class="ri-sparkling-line text-sm text-white relative z-10"></i>
            </div>
            <div class="flex flex-col leading-tight">
              <span class="text-sm font-bold text-white">تحدث مع البيانات</span>
              <span class="text-[11px] text-white/45">محلل بيانات لوحة المؤشرات</span>
            </div>
          </div>
          <button (click)="assistant.close()"
            class="w-8 h-8 flex items-center justify-center rounded-full bg-white/10 hover:bg-white/20 text-white/60 hover:text-white transition-all cursor-pointer">
            <i class="ri-close-line text-base"></i>
          </button>
        </header>

        <div class="siri-chat-scroll flex-1 overflow-y-auto px-3 py-3 space-y-2.5">
          @if (messages().length === 0) {
            <div class="flex flex-col">
              <div class="flex flex-col items-center pt-2 pb-4">
                <div class="relative w-16 h-16 mb-3 flex items-center justify-center">
                  <div class="siri-welcome-orb absolute inset-0"></div>
                  <div class="siri-welcome-orb-inner absolute inset-[4px]"></div>
                  <i class="ri-sparkling-line text-2xl text-white relative z-10"></i>
                </div>
                <h2 class="text-base font-semibold text-white mb-1">تحدث مع البيانات</h2>
                <p class="text-[12px] text-white/55 text-center leading-relaxed max-w-[240px]">
                  اسأل عن أي مؤشر في لوحة البيانات وسأحلله لك مباشرة
                </p>
              </div>
              <p class="text-[11px] font-medium text-white/40 mb-2.5 px-1">جرّب أحد هذه الأسئلة</p>
              <div class="space-y-1.5">
                @for (s of suggestions; track s) {
                  <button type="button" (click)="send(s)"
                    class="w-full text-right px-3 py-2.5 rounded-lg text-[12px] leading-relaxed font-bold text-white bg-white/[0.08] hover:bg-white/[0.14] border border-white/[0.10] hover:border-white/25 transition-all active:scale-[0.98] cursor-pointer">
                    {{ s }}
                  </button>
                }
              </div>
            </div>
          }

          @for (m of messages(); track m.id) {
            <div class="flex" [class.justify-end]="m.role === 'user'" [class.justify-start]="m.role !== 'user'"
              style="animation: siri-message-fade-in 0.3s ease-out forwards;">
              <div class="max-w-[90%] rounded-xl px-3 py-2 text-[12px] leading-relaxed {{ m.role === 'user' ? 'siri-user-bubble text-white' : 'siri-assistant-bubble text-white/90' }}">
                @if (m.role === 'assistant') {
                  <div class="whitespace-pre-wrap [&_strong]:text-white [&_strong]:font-semibold" [innerHTML]="format(m.text)"></div>
                  @if (m.text) {
                    <div class="mt-1 -mb-0.5 flex justify-end">
                      <button type="button" (click)="copy(m)" aria-label="نسخ الإجابة" title="نسخ الإجابة"
                        class="w-6 h-6 flex items-center justify-center rounded-md text-white/35 hover:text-white hover:bg-white/10 transition-all cursor-pointer">
                        <i class="text-[13px]" [ngClass]="copiedId() === m.id ? 'ri-check-line text-emerald-300' : 'ri-file-copy-line'"></i>
                      </button>
                    </div>
                  }
                } @else {
                  {{ m.text }}
                }
              </div>
            </div>
          }

          @if (thinking()) {
            <div class="flex justify-start" style="animation: siri-message-fade-in 0.3s ease-out forwards;">
              <div class="siri-assistant-bubble rounded-xl px-4 py-2.5">
                <div class="flex items-center gap-1.5 py-1" dir="ltr">
                  @for (b of waveBars; track b) {
                    <div class="siri-wave-bar" [style.animation-delay]="(b * 0.12) + 's'" style="height:16px"></div>
                  }
                </div>
              </div>
            </div>
          }

          @if (error()) {
            <div class="flex justify-start" style="animation: siri-message-fade-in 0.3s ease-out forwards;">
              <div class="rounded-xl px-3 py-2 text-[12px] text-red-200 bg-red-500/15 border border-red-400/20">
                تعذّر الوصول إلى المساعد حالياً. حاول مرة أخرى لاحقاً.
              </div>
            </div>
          }

          <div #anchor></div>
        </div>

        <div class="shrink-0 px-3 py-3 border-t border-white/10">
          <form (submit)="submit($event)" class="relative">
            <textarea #chatInput rows="1" [ngModel]="inputValue()" (ngModelChange)="onInput($event)"
              (keydown)="onInputKeydown($event)"
              [ngModelOptions]="{ standalone: true }" dir="rtl"
              placeholder="اسأل عن البيانات..."
              class="w-full min-h-10 max-h-[120px] py-2.5 pr-3 pl-10 rounded-lg text-[13px] leading-relaxed text-white placeholder-white/30 bg-white/10 border border-white/12 focus:border-white/25 focus:bg-white/15 focus:outline-none transition-all resize-none overflow-hidden"></textarea>
            <button type="submit" [disabled]="!inputValue().trim() || thinking()"
              class="absolute left-1.5 bottom-[9px] w-7 h-7 flex items-center justify-center rounded-md text-white/60 hover:text-white hover:bg-white/15 disabled:opacity-30 disabled:cursor-not-allowed transition-all cursor-pointer">
              <i class="ri-send-plane-fill text-sm"></i>
            </button>
          </form>
        </div>
      </aside>
    }
  `,
})
export class DataAssistantComponent {
  readonly assistant = inject(AssistantService);
  private readonly chat = inject(AssistantChatService);
  private readonly product = inject(ProductService);
  private readonly portalPeriod = inject(PortalPeriodService);

  get suggestions(): string[] { return this.chat.suggestionsFor(this.product.activeProductId()); }
  readonly messages = signal<ChatMessage[]>([]);
  readonly thinking = signal(false);
  readonly error = signal(false);
  readonly inputValue = signal('');
  readonly copiedId = signal<string | null>(null);
  readonly waveBars = [0, 1, 2, 3, 4];
  private seq = 0;
  private typewriter: { stop: () => void } | null = null;
  private readonly history = new InputHistory();
  private copyResetTimer: ReturnType<typeof setTimeout> | null = null;

  readonly format = formatAssistantText;
  private readonly anchor = viewChild<ElementRef<HTMLElement>>('anchor');
  private readonly chatInput = viewChild<ElementRef<HTMLTextAreaElement>>('chatInput');

  constructor() {
    effect(() => {
      this.messages();
      this.thinking();
      queueMicrotask(() => this.anchor()?.nativeElement.scrollIntoView({ behavior: 'smooth' }));
    });
    // Focus the input when the panel opens and again once an answer finishes.
    effect(() => {
      const el = this.chatInput()?.nativeElement;
      if (el && !this.thinking()) queueMicrotask(() => { el.focus(); this.fitHeight(el); });
    });
  }

  submit(event: Event): void {
    event.preventDefault();
    this.send(this.inputValue());
  }

  onInput(value: string): void {
    this.inputValue.set(value);
    this.history.stopBrowsing();
    this.resizeInput();
  }

  onInputKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send(this.inputValue());
      return;
    }
    // Terminal-style recall: only when the input is empty or a browsing session is already active,
    // so the arrows keep moving the caret while composing a multi-line question.
    if (event.key === 'ArrowUp' && (this.history.browsing || !this.inputValue().trim())) {
      const older = this.history.prev(this.inputValue());
      if (older !== null) {
        event.preventDefault();
        this.recall(older);
      }
    } else if (event.key === 'ArrowDown' && this.history.browsing) {
      event.preventDefault();
      this.recall(this.history.next() ?? '');
    }
  }

  copy(message: ChatMessage): void {
    const plain = message.text.replace(/\*\*/g, '');
    navigator.clipboard?.writeText(plain).then(() => {
      this.copiedId.set(message.id);
      if (this.copyResetTimer) clearTimeout(this.copyResetTimer);
      this.copyResetTimer = setTimeout(() => this.copiedId.set(null), 1500);
    }).catch(() => { /* clipboard unavailable: leave the text selectable */ });
  }

  private recall(text: string): void {
    this.inputValue.set(text);
    // After the signal renders: caret at the end, height fitted to the recalled text.
    setTimeout(() => {
      const el = this.chatInput()?.nativeElement;
      if (!el) return;
      el.setSelectionRange(el.value.length, el.value.length);
      this.fitHeight(el);
    });
  }

  private resizeInput(): void {
    setTimeout(() => {
      const el = this.chatInput()?.nativeElement;
      if (el) this.fitHeight(el);
    });
  }

  private fitHeight(el: HTMLTextAreaElement): void {
    const max = 120;
    el.style.height = 'auto';
    el.style.height = `${Math.min(el.scrollHeight, max)}px`;
    // The scrollbar appears only once the input reaches its maximum height.
    el.style.overflowY = el.scrollHeight > max ? 'auto' : 'hidden';
  }

  send(text: string): void {
    const trimmed = text.trim();
    if (!trimmed || this.thinking()) return;

    // Finalize any answer still typing out before starting a new turn.
    this.typewriter?.stop();

    this.error.set(false);
    const history = this.messages().map((m) => ({ role: m.role, content: m.text }));
    this.messages.update((prev) => [...prev, { id: this.nextId(), role: 'user', text: trimmed }]);
    this.inputValue.set('');
    this.history.push(trimmed);
    this.resizeInput();
    this.thinking.set(true);

    const assistantId = this.nextId();
    let created = false;
    let target = '';
    let shown = 0;
    let streamDone = false;
    let timer: ReturnType<typeof setInterval> | null = null;

    const setText = (t: string) =>
      this.messages.update((prev) => prev.map((m) => (m.id === assistantId ? { ...m, text: t } : m)));

    const stop = () => {
      if (timer) { clearInterval(timer); timer = null; }
      shown = target.length;
      setText(target);
      this.thinking.set(false);
      if (this.typewriter?.stop === stop) this.typewriter = null;
    };
    this.typewriter = { stop };

    // Reveal the streamed text gradually for a natural "typing" effect,
    // decoupled from how fast the provider delivers tokens.
    const tick = () => {
      if (shown < target.length) {
        shown = Math.min(target.length, shown + Math.max(2, Math.ceil((target.length - shown) / 10)));
        setText(target.slice(0, shown));
      } else if (streamDone) {
        stop();
      }
    };

    const render = (full: string) => {
      if (!created) {
        created = true;
        this.thinking.set(false);
        this.messages.update((prev) => [...prev, { id: assistantId, role: 'assistant', text: '' }]);
      }
      target = full;
      timer ??= setInterval(tick, 16);
    };

    const productId = this.product.activeProductId();
    // Ground the answer on the rental period selected in the portal (null = the live quarter).
    const period = productId?.includes('foreign') ? undefined : this.portalPeriod.selected() ?? undefined;
    this.chat
      .ask(trimmed, history, render, productId, period)
      .then((finalText) => { target = finalText; })
      .catch(() => this.error.set(true))
      .finally(() => {
        streamDone = true;
        if (!timer) this.thinking.set(false);
      });
  }

  private nextId(): string {
    return `${Date.now()}-${this.seq++}`;
  }
}
