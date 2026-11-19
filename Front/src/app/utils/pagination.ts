/** Client-side paging over an in-memory list (1-based pages). */
export interface PageSlice {
  /** Current page, clamped to [1, totalPages]. */
  page: number;
  totalPages: number;
  /** Index of the first item on the page (inclusive). */
  start: number;
  /** Index after the last item on the page (exclusive). */
  end: number;
}

export function pageSlice(total: number, page: number, pageSize: number): PageSlice {
  const size = Math.max(1, Math.floor(pageSize));
  const totalPages = Math.max(1, Math.ceil(total / size));
  const current = Math.min(Math.max(1, Math.floor(page) || 1), totalPages);
  const start = (current - 1) * size;
  return { page: current, totalPages, start, end: Math.min(start + size, total) };
}

/**
 * Page buttons to show: first, last and the pages around the current one; 'gap' marks skipped ranges.
 * e.g. page 6 of 13 → [1, 'gap', 5, 6, 7, 'gap', 13]
 */
export function pageWindow(page: number, totalPages: number, around = 1): (number | 'gap')[] {
  if (totalPages <= 1) return [1];
  const pages = new Set<number>([1, totalPages]);
  for (let p = page - around; p <= page + around; p++) if (p >= 1 && p <= totalPages) pages.add(p);
  const sorted = [...pages].sort((a, b) => a - b);
  const out: (number | 'gap')[] = [];
  sorted.forEach((p, i) => {
    if (i > 0) {
      const gap = p - sorted[i - 1];
      if (gap === 2) out.push(p - 1); // a single hidden page is shown instead of a gap
      else if (gap > 2) out.push('gap');
    }
    out.push(p);
  });
  return out;
}
