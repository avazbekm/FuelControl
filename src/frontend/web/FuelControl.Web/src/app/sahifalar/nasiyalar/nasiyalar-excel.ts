import type { NasiyaDto } from '../../api/model';
import { kun, kunToliq } from '../../core/format';
import { telefonFormat } from '../../core/telefon';

type Hujayra = { value?: string | number; type?: StringConstructor | NumberConstructor; format?: string; fontWeight?: 'bold'; backgroundColor?: string };

interface Parametr {
  T: (kalit: string, ...args: unknown[]) => string;
  /** Holat matni (sahifadagi belgi bilan bir xil: "7 kun o'tdi", "Bugun", ...); yopilgan nasiya uchun chaqirilmaydi. */
  holat: (n: NasiyaDto) => string;
  fayl: string;
}

const PUL = '#,##0';
const SARLAVHA = '#E9F0FF';

/** Nasiyalar sahifasida ko'rinib turgan ro'yxat (joriy filtr + qidiruv) → .xlsx; oxirida Qarz/Qaytgan/Qoldiq yig'indisi (write-excel-file, faqat eksportda yuklanadi). */
export async function nasiyalarExcel(royxat: NasiyaDto[], p: Parametr): Promise<void> {
  const { default: writeXlsxFile } = await import('write-excel-file/browser');
  const T = p.T;
  const matn = (v: string, qalin = false, fon?: string): Hujayra => ({ value: v, type: String, ...(qalin ? { fontWeight: 'bold' as const } : {}), ...(fon ? { backgroundColor: fon } : {}) });
  const son = (v: number, f = PUL, qalin = false): Hujayra => ({ value: v, type: Number, format: f, ...(qalin ? { fontWeight: 'bold' as const } : {}) });

  const data: Hujayra[][] = [[
    T('Nasiyalar_Mijoz'), T('Nasiya_Telefon'), T('Nasiya_MashinaRaqami'), T('Nasiyalar_Yozilgan'), T('Nasiyalar_ColSmena'), T('Operator'),
    T('Nasiya_Qarz'), T('Nasiya_Qaytgan'), T('Nasiya_Qoldiq'), T('Nasiya_MuddatYorliq'), T('Nasiyalar_ColHolat'),
  ].map((x) => matn(x, true, SARLAVHA))];
  for (const n of royxat) {
    data.push([
      matn(n.mijozIsmi), matn(telefonFormat(n.telefon)), matn(n.mashinaRaqami), matn(kun(n.yozildi)), matn('#' + n.smenaId), matn(n.operatorIsmi),
      son(n.summa), son(n.qaytgan), son(n.qoldiq), matn(kunToliq(n.muddat)), matn(n.holati === 'Yopilgan' ? T('Nasiyalar_Yopilgan') : p.holat(n)),
    ]);
  }
  const jami = (f: (n: NasiyaDto) => number) => royxat.reduce((a, n) => a + f(n), 0);
  data.push([matn(T('Jami'), true), {}, {}, {}, {}, {}, son(jami((n) => n.summa), PUL, true), son(jami((n) => n.qaytgan), PUL, true), son(jami((n) => n.qoldiq), PUL, true), {}, {}]);

  await writeXlsxFile(data as never, {
    sheet: T('Nasiyalar_ExcelSarlavha').slice(0, 31),
    columns: [{ width: 26 }, { width: 18 }, { width: 16 }, { width: 13 }, { width: 9 }, { width: 22 }, { width: 14 }, { width: 14 }, { width: 14 }, { width: 13 }, { width: 18 }],
    stickyRowsCount: 1,
  } as never).toFile(p.fayl);
}
