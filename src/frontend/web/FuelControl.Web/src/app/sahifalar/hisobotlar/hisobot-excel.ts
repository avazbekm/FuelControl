import type { HisobotDto, HisobotGuruhi, HisobotQatoriDto } from '../../api/model';

type Hujayra = { value?: string | number; type?: StringConstructor | NumberConstructor; format?: string; fontWeight?: 'bold'; backgroundColor?: string };

interface Parametr {
  T: (kalit: string, ...args: unknown[]) => string;
  guruh: HisobotGuruhi;
  nomi: (q: HisobotQatoriDto) => string;
  ikkinchi: (q: HisobotQatoriDto) => string;
  fayl: string;
}

const PUL = '#,##0';
const LITR = '#,##0.00';
const SARLAVHA = '#E9F0FF';

/** Hisobotni haqiqiy .xlsx ga chiqaradi: 1-varaq — guruh jadvali, 2-varaq — aparatlar va baklar (write-excel-file, faqat eksportda yuklanadi). */
export async function hisobotExcel(n: HisobotDto, p: Parametr): Promise<void> {
  const { default: writeXlsxFile } = await import('write-excel-file/browser');
  const T = p.T;
  const matn = (v: string, qalin = false, fon?: string): Hujayra => ({ value: v, type: String, ...(qalin ? { fontWeight: 'bold' as const } : {}), ...(fon ? { backgroundColor: fon } : {}) });
  const son = (v: number, f = PUL, qalin = false): Hujayra => ({ value: v, type: Number, format: f, ...(qalin ? { fontWeight: 'bold' as const } : {}) });
  const sarlavha = (r: string[]) => r.map((x) => matn(x, true, SARLAVHA));

  const birinchi = T(({ Smena: 'Hisobot_ColSmena', Kun: 'Hisobot_ColKun', Oy: 'Hisobot_ColOy', Operator: 'Operator' } as const)[p.guruh]);
  const ikkinchi = T(p.guruh === 'Smena' ? 'Hisobot_ColOperator' : 'Hisobot_ColSmenalar');
  const d1: Hujayra[][] = [sarlavha([
    birinchi, ikkinchi, T('Hisobot_ColLitr'), T('Hisobot_ColSavdo'), T('Hisobot_ColPlastik'), T('Hisobot_ColDepozit'), T('Hisobot_ColNasiya'),
    T('Hisobot_ColQaytgan'), T('Hisobot_ColXarajat'), T('Hisobot_ColNaqd'), T('Hisobot_KpiKamomat'), T('Hisobot_ColOrtiqcha'), T('Hisobot_ColFarq'),
  ])];
  const qator = (q: HisobotQatoriDto, nom: string, ik: string, qalin: boolean): Hujayra[] => [
    matn(nom, qalin), matn(ik, qalin), son(q.litr, LITR, qalin), son(q.savdo, PUL, qalin), son(q.plastik, PUL, qalin), son(q.depozit, PUL, qalin), son(q.nasiya, PUL, qalin),
    son(q.qaytganNasiya, PUL, qalin), son(q.xarajat, PUL, qalin), son(q.naqdSavdo, PUL, qalin), son(q.kamomat, PUL, qalin), son(q.ortiqcha, PUL, qalin), son(q.ortiqcha - q.kamomat, PUL, qalin),
  ];
  for (const q of n.qatorlar) d1.push(qator(q, p.nomi(q), p.ikkinchi(q), false));
  d1.push(qator(n.jami, T('Jami'), '', true));
  if (n.avans > 0) d1.push([matn(T('Hisobot_KpiAvans')), matn(''), ...Array(10).fill({}), son(n.avans)]);

  const d2: Hujayra[][] = [sarlavha([
    T('Hisobot_ColAparat'), T('Hisobot_ColYoqilgi'), T('Hisobot_ColBakBoshida'), T('Hisobot_ColKirim'), T('Hisobot_ColSotildi'), T('Hisobot_ColBakOxirida'), T('Hisobot_ColSavdo'),
  ])];
  for (const b of n.aparatlar) {
    d2.push([matn(T('Aparat_Raqami', b.raqam)), matn(b.yoqilgiNomi), son(b.bakBoshida, LITR), son(b.kirim, LITR), son(b.sotildi, LITR), son(b.bakOxirida, LITR), son(b.savdo)]);
  }
  const y = (f: (x: HisobotDto['aparatlar'][number]) => number) => Math.round(n.aparatlar.reduce((s, x) => s + f(x), 0) * 100) / 100;
  d2.push([matn(T('Jami'), true), matn(''), son(y((x) => x.bakBoshida), LITR, true), son(y((x) => x.kirim), LITR, true), son(y((x) => x.sotildi), LITR, true), son(y((x) => x.bakOxirida), LITR, true), son(n.aparatlar.reduce((s, x) => s + x.savdo, 0), PUL, true)]);

  const k1 = [22, 20, 12, 14, 14, 14, 14, 14, 14, 14, 12, 12, 12].map((width) => ({ width }));
  const k2 = [16, 14, 16, 14, 14, 16, 16].map((width) => ({ width }));
  await writeXlsxFile([
    { data: d1, sheet: T('Hisobotlar').slice(0, 31), columns: k1, stickyRowsCount: 1 },
    { data: d2, sheet: T('Hisobot_BakSarlavha').slice(0, 31), columns: k2, stickyRowsCount: 1 },
  ] as never).toFile(p.fayl);
}
