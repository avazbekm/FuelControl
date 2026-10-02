import type { Hisobot, HisobotQatori } from '../../api/turlar';

/**
 * Hisobot qatorlari tartib bilan keladi: guruh ichida yoqilg'i qatorlari, keyin guruh jami qatori (jami=true, yoqilgi=null).
 * Kamomat/avans/bekor soni faqat jami qatorlarida. Sahifa uchun guruhlarga ajratamiz.
 */
export interface HisobotBolimi {
  guruh: string;
  jami: HisobotQatori;
  yoqilgilar: HisobotQatori[];
}

export function bolimlarga(h: Hisobot): HisobotBolimi[] {
  const tartib: string[] = [];
  const map = new Map<string, { jami?: HisobotQatori; yoq: HisobotQatori[] }>();
  for (const q of h.qatorlar) {
    if (!map.has(q.guruh)) { map.set(q.guruh, { yoq: [] }); tartib.push(q.guruh); }
    const b = map.get(q.guruh)!;
    if (q.jami) b.jami = q;
    else b.yoq.push(q);
  }
  return tartib.flatMap((g) => {
    const b = map.get(g)!;
    return b.jami ? [{ guruh: g, jami: b.jami, yoqilgilar: b.yoq }] : [];
  });
}
