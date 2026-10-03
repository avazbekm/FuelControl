import { Injectable, signal } from '@angular/core';
import { api, ol } from '../../api/api';
import type { Aparat, Foydalanuvchi, NarxTarixi, Ruxsat, Rol, Yoqilgi, ZaxiraJavobi } from '../../api/turlar';

/** Yoqilg'i dialogidagi 8 ta rang (desktop SozlamalarViewModel.Palitra bilan bir xil). */
export const PALITRA = ['#2F6BFF', '#7C5CFF', '#E8A317', '#1EA66A', '#19B5C9', '#E5484D', '#F0668A', '#5A6B88'];

/** Ruxsat va guruhi: B — bo'limlar, A — amallar (desktop Ruxsatlar.Royxat bilan bir xil tartibda). */
export const RUXSAT_ROYXATI: { ruxsat: Ruxsat; guruh: 'B' | 'A' }[] = [
  { ruxsat: 'Boshqaruv', guruh: 'B' }, { ruxsat: 'SotuvKiritish', guruh: 'B' }, { ruxsat: 'Smenalar', guruh: 'B' },
  { ruxsat: 'Hisobotlar', guruh: 'B' }, { ruxsat: 'Operatorlar', guruh: 'B' }, { ruxsat: 'Audit', guruh: 'B' },
  { ruxsat: 'Sozlamalar', guruh: 'B' },
  { ruxsat: 'SmenaOchish', guruh: 'A' }, { ruxsat: 'SmenaYopish', guruh: 'A' }, { ruxsat: 'SotuvTahrirlash', guruh: 'A' },
  { ruxsat: 'SotuvBekorQilish', guruh: 'A' }, { ruxsat: 'AvansBerish', guruh: 'A' }, { ruxsat: 'Eksport', guruh: 'A' },
];

/** Rol bo'yicha boshlang'ich ruxsatlar (desktop Ruxsatlar.Standart; backend RuxsatXizmati bilan mos). */
export function standartRuxsatlar(rol: Rol): Ruxsat[] {
  const hammasi = RUXSAT_ROYXATI.map((r) => r.ruxsat);
  if (rol === 'Operator') return ['SotuvKiritish', 'SmenaOchish', 'SmenaYopish'];
  if (rol === 'Boshliq') return hammasi.filter((r) => r !== 'Sozlamalar');
  return hammasi;
}

/** Matndan butun son (bo'shliq/nuqta/vergul ajratgichlari e'tiborsiz): "12 200" → 12200. */
export function butunSon(m: string): number {
  const t = m.replace(/\D/g, '');
  return t ? Number(t) : 0;
}

/**
 * Sozlamalar sahifasi ma'lumotlari (sahifa darajasida; bo'limlar orasida o'tganda qayta yuklanmaydi).
 * Har bo'lim CRUD'dan keyin o'ziga tegishli ro'yxatlarni yangilaydi.
 */
@Injectable()
export class SozlamalarXizmati {
  readonly yoqilgilar = signal<Yoqilgi[]>([]);
  readonly aparatlar = signal<Aparat[]>([]);
  readonly foydalanuvchilar = signal<Foydalanuvchi[]>([]);
  readonly narxTarixi = signal<NarxTarixi[]>([]);
  readonly yuklandi = signal(false);
  readonly xato = signal<string | null>(null);

  async yukla() {
    const natijalar = await Promise.allSettled([
      this.yoqilgilarniYukla(), this.aparatlarniYukla(), this.foydalanuvchilarniYukla(), this.tarixniYukla(),
    ]);
    const rad = natijalar.find((n): n is PromiseRejectedResult => n.status === 'rejected');
    this.xato.set(rad ? (rad.reason as Error).message : null);
    this.yuklandi.set(true);
    if (rad) throw rad.reason;
  }

  async yoqilgilarniYukla() { this.yoqilgilar.set(await ol(api.GET('/yoqilgilar'))); }
  async aparatlarniYukla() { this.aparatlar.set([...(await ol(api.GET('/aparatlar')))].sort((a, b) => a.raqam - b.raqam)); }
  async foydalanuvchilarniYukla() { this.foydalanuvchilar.set(await ol(api.GET('/foydalanuvchilar'))); }
  async tarixniYukla() { this.narxTarixi.set(await ol(api.GET('/yoqilgilar/narx-tarixi'))); }

  // ---- Yoqilg'i
  async yoqilgiYarat(nomi: string, narx: number, rang: string) {
    await ol(api.POST('/yoqilgilar', { body: { nomi, narx, rang } }));
    await this.yoqilgilarniYukla();
  }
  async yoqilgiTahrirla(id: number, nomi: string, narx: number, rang: string) {
    await ol(api.PUT('/yoqilgilar/{id}', { params: { path: { id } }, body: { nomi, narx, rang } }));
    // Nomi o'zgarsa aparat kartalaridagi yoqilg'i nomi ham o'zgaradi; narx o'zgarsa tarixga yoziladi.
    await Promise.all([this.yoqilgilarniYukla(), this.aparatlarniYukla(), this.tarixniYukla()]);
  }
  async yoqilgiOchir(id: number) {
    await ol(api.DELETE('/yoqilgilar/{id}', { params: { path: { id } } }));
    await this.yoqilgilarniYukla();
  }

  // ---- Aparat
  async aparatYarat(raqam: number, yoqilgiTuriId: number, boshlangichTotalLitr: number) {
    await ol(api.POST('/aparatlar', { body: { raqam, yoqilgiTuriId, boshlangichTotalLitr } }));
    await this.aparatlarniYukla();
  }
  async aparatTahrirla(id: number, raqam: number, yoqilgiTuriId: number, totalLitr: number) {
    await ol(api.PUT('/aparatlar/{id}', { params: { path: { id } }, body: { raqam, yoqilgiTuriId, totalLitr } }));
    await this.aparatlarniYukla();
  }

  // ---- Foydalanuvchi
  async foydalanuvchiYarat(toliqIsm: string, login: string, rol: Rol, oylikMaosh: number, parolYokiPin: string): Promise<Foydalanuvchi> {
    const f = await ol(api.POST('/foydalanuvchilar', { body: { toliqIsm, login, rol, oylikMaosh, parolYokiPin } }));
    await this.foydalanuvchilarniYukla();
    return f;
  }
  async foydalanuvchiTahrirla(id: number, d: { toliqIsm: string; login: string; rol: Rol; faol: boolean; oylikMaosh: number }, yangiPin?: string) {
    await ol(api.PUT('/foydalanuvchilar/{id}', { params: { path: { id } }, body: d }));
    if (yangiPin?.trim()) await ol(api.POST('/foydalanuvchilar/{id}/pin', { params: { path: { id } }, body: { yangiParolYokiPin: yangiPin.trim() } }));
    await this.foydalanuvchilarniYukla();
  }
  async pinOrnat(id: number, pin: string) {
    await ol(api.POST('/foydalanuvchilar/{id}/pin', { params: { path: { id } }, body: { yangiParolYokiPin: pin } }));
  }
  /** Butun ruxsatlar to'plamini yozadi; server yangilangan foydalanuvchini qaytaradi. */
  async ruxsatlarniOrnat(id: number, ruxsatlar: Ruxsat[]): Promise<Foydalanuvchi> {
    return ol(api.PUT('/foydalanuvchilar/{id}/ruxsatlar', { params: { path: { id } }, body: { ruxsatlar } }));
  }

  // ---- Zaxira
  zaxiraOl(): Promise<ZaxiraJavobi> { return ol(api.POST('/zaxira')); }
}
