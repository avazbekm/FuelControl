import type { SmenaTafsilotDto } from '../../api/model';
import { kunSoat, kunToliq, pul } from '../../core/format';
import { plastikIfodasi } from '../../core/hisob';

type T = (kalit: string, ...args: unknown[]) => string;
type Hujayra = { value?: string | number; type?: StringConstructor | NumberConstructor; format?: string; fontWeight?: 'bold'; backgroundColor?: string; align?: 'left' | 'right' | 'center' };

const PUL = '#,##0';
const LITR = '#,##0.00';

/** Bitta smena hisobini haqiqiy .xlsx ga chiqaradi (write-excel-file, faqat eksportda yuklanadi). */
export async function smenaExcel(t: SmenaTafsilotDto, T: T): Promise<void> {
  const { default: writeXlsxFile } = await import('write-excel-file/browser');
  const s = t.smena;
  const matn = (v: string, qalin = false, fon?: string): Hujayra => ({ value: v, type: String, ...(qalin ? { fontWeight: 'bold' as const } : {}), ...(fon ? { backgroundColor: fon } : {}) });
  const son = (v: number, f = PUL, qalin = false): Hujayra => ({ value: v, type: Number, format: f, ...(qalin ? { fontWeight: 'bold' as const } : {}) });
  const SARLAVHA = '#E9F0FF';
  const qator = (a: string, b: Hujayra | string, qalin = false): Hujayra[] => [matn(a, qalin), typeof b === 'string' ? matn(b) : b];

  const m: Hujayra[][] = [];
  m.push([matn(T('Smenalar_Smena', s.id), true)]);
  m.push(qator(T('Smenalar_Operator'), s.operatorIsmi));
  m.push(qator(T('Smenalar_ColOchildi'), kunSoat(s.boshlandi)));
  m.push(qator(T('Smenalar_Yopilgan'), s.tugadi ? kunSoat(s.tugadi) : '—'));
  m.push([]);
  if (t.korsatkichlar.length) {
    m.push([T('Smenalar_Aparat'), T('Smenalar_Oldingi'), T('Smenalar_Yangi'), T('Smenalar_Narx'), T('Smenalar_Litr'), T('Smenalar_Summa')].map((x) => matn(x, true, SARLAVHA)));
    for (const k of t.korsatkichlar) {
      m.push([matn(`${k.aparatRaqam} · ${k.yoqilgiNomi}`), son(k.boshi, LITR), son(k.oxiri, LITR), son(k.narx), son(k.litr, LITR), son(k.summa)]);
    }
    m.push([matn(T('Jami'), true), {}, {}, {}, son(s.jamiLitr, LITR, true), son(s.savdo, PUL, true)]);
    m.push([]);
  }
  m.push([matn(T('Smenalar_PulHisobi'), true, SARLAVHA), matn('', false, SARLAVHA)]);
  const pulQator = (k: string, v: number, ...a: unknown[]) => qator(T(k, ...a), son(v));
  m.push(pulQator('Smenalar_Qaytim', s.ochishQaytim));
  m.push(pulQator('Smenalar_SavdoAparatlar', s.savdo));
  m.push(pulQator('Smenalar_QaytganNasiya', s.qaytganNasiya));
  m.push(qator(T('Smenalar_PlastikSatr', plastikIfodasi(s, pul), pul(s.ochishTerminal)), son(-s.plastik)));
  m.push(qator(T('Smenalar_DepozitSatr', pul(s.ochishDepozit), pul(s.yopishDepozit)), son(-s.depozitFarqi)));
  m.push(qator(T('Smenalar_NasiyalarJami'), son(-s.nasiyaJami)));
  m.push(qator(T('Smenalar_XarajatlarJami'), son(-s.xarajatJami)));
  m.push(qator(T('Smenalar_KassadaKerak'), son(s.kutilgan, PUL, true), true));
  m.push(qator(T('Smenalar_SanalganNaqd'), son(s.sanalganNaqd ?? 0, PUL, true), true));
  m.push(qator(s.farq < 0 ? T('Smenalar_Kamomat') : s.farq > 0 ? T('Smenalar_Ortiqcha') : T('Smenalar_ColFarq'), son(s.farq, PUL, true), true));
  if (s.izoh) m.push(qator(T('Smenalar_Izoh'), s.izoh));

  if (t.nasiyalar.length || t.qaytishlar.length || t.xarajatlar.length) {
    m.push([]);
    for (const n of t.nasiyalar) m.push([matn(T('Smenalar_Nasiyalar')), matn(`${n.mijozIsmi} · ${n.mashinaRaqami} · ${kunToliq(n.muddat)}`), son(n.summa)]);
    for (const q of t.qaytishlar) m.push([matn(T('Smenalar_Qaytishlar')), matn(`${q.mijozIsmi} · ${T('Tolov_' + q.usul)}`), son(q.summa)]);
    for (const x of t.xarajatlar) m.push([matn(T('Smenalar_Xarajatlar')), matn(`${x.sabab}`), son(x.summa)]);
  }

  const kenglik = [34, 18, 18, 14, 14, 16].map((width) => ({ width }));
  await writeXlsxFile(m as never, { sheet: T('Smenalar_Smena', s.id).slice(0, 31), columns: kenglik } as never).toFile(`fuelcontrol-smena-${s.id}.xlsx`);
}
