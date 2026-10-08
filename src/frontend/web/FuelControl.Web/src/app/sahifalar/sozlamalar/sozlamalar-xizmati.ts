import { Injectable, inject, signal } from '@angular/core';
import { Server } from '../../core/server';
import type {
  AparatDto, AparatKorsatkichDto, AparatTahrirlashDto, AparatYaratishDto, FoydalanuvchiDto, FoydalanuvchiTahrirlashDto, NarxTarixiDto,
  Rol, Ruxsat, SmenaTafsilotDto, YoqilgiTuriDto,
} from '../../api/model';

/** Yoqilg'i dialogidagi 8 ta rang (desktop SozlamalarViewModel.Palitra bilan bir xil). */
export const PALITRA = ['#2F6BFF', '#7C5CFF', '#E8A317', '#1EA66A', '#19B5C9', '#E5484D', '#F0668A', '#5A6B88'];

/** Ruxsatlar bo'limi (docs/dizayn/SozlamalarRuxsatlar): bo'limlar (B) va amallar (A), `yangi` — "Yangi" belgisi. */
export interface RuxsatElementi { ruxsat: Ruxsat; guruh: 'B' | 'A'; yangi?: boolean; izoh: boolean }
export const RUXSAT_ROYXATI: RuxsatElementi[] = [
  { ruxsat: 'Boshqaruv', guruh: 'B', izoh: false }, { ruxsat: 'Savdo', guruh: 'B', izoh: false }, { ruxsat: 'Smenalar', guruh: 'B', izoh: false },
  { ruxsat: 'Nasiyalar', guruh: 'B', yangi: true, izoh: false }, { ruxsat: 'Hisobotlar', guruh: 'B', izoh: false },
  { ruxsat: 'Operatorlar', guruh: 'B', izoh: false }, { ruxsat: 'Audit', guruh: 'B', izoh: false }, { ruxsat: 'Sozlamalar', guruh: 'B', izoh: false },
  { ruxsat: 'SmenaOchish', guruh: 'A', izoh: true }, { ruxsat: 'SmenaYopish', guruh: 'A', izoh: true },
  { ruxsat: 'NasiyaYozish', guruh: 'A', yangi: true, izoh: true }, { ruxsat: 'QarzQaytdi', guruh: 'A', yangi: true, izoh: true },
  { ruxsat: 'XarajatYozish', guruh: 'A', yangi: true, izoh: true }, { ruxsat: 'BakKirim', guruh: 'A', yangi: true, izoh: true },
  { ruxsat: 'KorsatkichTuzatish', guruh: 'A', yangi: true, izoh: true }, { ruxsat: 'AvansBerish', guruh: 'A', izoh: true },
  { ruxsat: 'Eksport', guruh: 'A', izoh: true },
];

/** Rol bo'yicha boshlang'ich ruxsatlar (backend RuxsatXizmati bilan bir xil). */
export function standartRuxsatlar(rol: Rol): Ruxsat[] {
  const hammasi = RUXSAT_ROYXATI.map((r) => r.ruxsat);
  if (rol === 'Operator') return ['Savdo', 'Nasiyalar', 'SmenaOchish', 'SmenaYopish', 'NasiyaYozish', 'QarzQaytdi', 'XarajatYozish'];
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
  private readonly server = inject(Server);

  readonly yoqilgilar = signal<YoqilgiTuriDto[]>([]);
  readonly aparatlar = signal<AparatDto[]>([]);
  readonly foydalanuvchilar = signal<FoydalanuvchiDto[]>([]);
  readonly narxTarixi = signal<NarxTarixiDto[]>([]);
  /** Ochiq smena (narx o'zgartirishda ko'rsatkich so'rash uchun); yo'q bo'lsa null. */
  readonly joriySmena = signal<SmenaTafsilotDto | null>(null);
  readonly yuklandi = signal(false);
  readonly xato = signal<string | null>(null);

  async yukla() {
    const natijalar = await Promise.allSettled([
      this.yoqilgilarniYukla(), this.aparatlarniYukla(), this.foydalanuvchilarniYukla(), this.tarixniYukla(), this.smenaniYukla(),
    ]);
    const rad = natijalar.find((n): n is PromiseRejectedResult => n.status === 'rejected');
    this.xato.set(rad ? (rad.reason as Error).message : null);
    this.yuklandi.set(true);
    if (rad) throw rad.reason;
  }

  async yoqilgilarniYukla() { this.yoqilgilar.set(await this.server.yoqilgilar()); }
  async aparatlarniYukla() { this.aparatlar.set([...(await this.server.aparatlar())].sort((a, b) => a.raqam - b.raqam)); }
  async foydalanuvchilarniYukla() { this.foydalanuvchilar.set(await this.server.foydalanuvchilar()); }
  async tarixniYukla() { this.narxTarixi.set(await this.server.narxTarixi()); }
  async smenaniYukla() { this.joriySmena.set(await this.server.joriySmena()); }

  // ---- Yoqilg'i
  async yoqilgiYarat(nomi: string, narx: number, rang: string) {
    await this.server.yoqilgiYarat(nomi, narx, rang);
    await this.yoqilgilarniYukla();
  }
  /** Narx o'zgarmasa — faqat nom/rang. Narx o'zgarsa va smena ochiq bo'lsa `korsatkichlar` majburiy (server qoidasi). */
  async yoqilgiTahrirla(id: number, nomi: string, narx: number, rang: string, korsatkichlar?: AparatKorsatkichDto[]) {
    await this.server.yoqilgiTahrirla(id, { nomi, narx, rang, korsatkichlar: korsatkichlar?.length ? korsatkichlar : null });
    // Nomi o'zgarsa aparat kartalaridagi yoqilg'i nomi ham o'zgaradi; narx o'zgarsa tarixga yoziladi va smena segmenti paydo bo'ladi.
    await Promise.all([this.yoqilgilarniYukla(), this.aparatlarniYukla(), this.tarixniYukla(), this.smenaniYukla()]);
  }
  async yoqilgiOchir(id: number) {
    await this.server.yoqilgiOchir(id);
    await this.yoqilgilarniYukla();
  }

  // ---- Aparat
  async aparatYarat(d: AparatYaratishDto) {
    await this.server.aparatYarat(d);
    await this.aparatlarniYukla();
  }
  async aparatTahrirla(id: number, d: AparatTahrirlashDto) {
    await this.server.aparatTahrirla(id, d);
    await this.aparatlarniYukla();
  }

  // ---- Foydalanuvchi
  async foydalanuvchiYarat(toliqIsm: string, login: string, rol: Rol, oylikMaosh: number, parolYokiPin: string): Promise<FoydalanuvchiDto> {
    const f = await this.server.foydalanuvchiYarat(toliqIsm, login, rol, oylikMaosh, parolYokiPin);
    await this.foydalanuvchilarniYukla();
    return f;
  }
  async foydalanuvchiTahrirla(id: number, d: FoydalanuvchiTahrirlashDto, yangiPin?: string) {
    await this.server.foydalanuvchiTahrirla(id, d);
    if (yangiPin?.trim()) await this.server.pinOrnat(id, yangiPin.trim());
    await this.foydalanuvchilarniYukla();
  }
  async pinOrnat(id: number, pin: string) { await this.server.pinOrnat(id, pin); }
  /** Butun ruxsatlar to'plamini yozadi; server yangilangan foydalanuvchini qaytaradi. */
  async ruxsatlarniOrnat(id: number, ruxsatlar: Ruxsat[]): Promise<FoydalanuvchiDto> {
    const f = await this.server.ruxsatlarniOrnat(id, ruxsatlar);
    await this.foydalanuvchilarniYukla();
    return f;
  }
}
