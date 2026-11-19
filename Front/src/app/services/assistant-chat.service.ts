import { Injectable, inject } from '@angular/core';
import { environment } from '../../environments/environment';
import { AuthService } from './auth.service';

export interface AssistantHistoryTurn {
  role: 'user' | 'assistant';
  content: string;
}

/**
 * Streams the backend "Talk to Data" LLM answer (POST /api/assistant/ask, SSE) and reports the
 * growing answer via `onDelta`. LLM-only: on any failure it throws so the caller shows an error.
 * Uses raw fetch (SSE), so the JWT is attached manually (the HTTP interceptor does not apply).
 */
@Injectable({ providedIn: 'root' })
export class AssistantChatService {
  private readonly auth = inject(AuthService);
  private readonly base = environment.apiUrl;

  private readonly rentalSuggestions: string[] = [
    'ما هي المدن ذات أعلى نمو إيجاري؟',
    'أي المدن تجاوزت حد العبء السكني؟',
    'قارن بين الرياض وجدة في مؤشرات الإيجار',
    'ما وضع تفعيل الضوابط على مستوى المملكة؟',
  ];

  private readonly foreignSuggestions: string[] = [
    'ما هي أعلى الجنسيات تملكاً للعقار؟',
    'أي المناطق الأكثر نشاطاً في التملك؟',
    'ما متوسط قيمة الصفقة وإجمالي القيمة؟',
    'ما أبرز أنواع العقارات من حيث الصفقات؟',
  ];

  /** Product-aware starter prompts. */
  suggestionsFor(productId: string): string[] {
    return productId?.includes('foreign') ? this.foreignSuggestions : this.rentalSuggestions;
  }

  async ask(
    question: string,
    history: AssistantHistoryTurn[],
    onDelta: (fullText: string) => void,
    product: string,
    period?: string,
  ): Promise<string> {
    const trimmed = question.trim();
    if (!trimmed) return '';

    const token = this.auth.getToken();
    const response = await fetch(`${this.base}/assistant/ask`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify({
        question: trimmed,
        lang: 'ar',
        product,
        history: history.slice(-6),
        ...(period ? { period } : {}),
      }),
    });

    if (!response.ok || !response.body) {
      throw new Error(`assistant-unavailable-${response.status}`);
    }

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let full = '';
    let sawError = false;

    for (;;) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      const lines = buffer.split('\n');
      buffer = lines.pop() ?? '';
      for (const line of lines) {
        const text = line.trim();
        if (!text.startsWith('data:')) continue;
        const data = text.slice('data:'.length).trim();
        if (!data) continue;
        try {
          const evt = JSON.parse(data) as { delta?: string; done?: boolean; error?: string };
          if (evt.error) { sawError = true; continue; }
          if (evt.delta) { full += evt.delta; onDelta(full); }
        } catch {
          // Ignore malformed keep-alive chunks.
        }
      }
    }

    if (sawError && !full) throw new Error('assistant-error');
    if (!full.trim()) throw new Error('assistant-empty');
    return full;
  }
}
