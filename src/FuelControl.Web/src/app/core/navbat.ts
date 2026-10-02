import { Injectable, signal, inject, computed, effect } from '@angular/core';
import { openDB, type IDBPDatabase } from 'idb';
import { Subject } from 'rxjs';
import { api, ol, ApiXato, AloqaXato } from '../api/api';
import { Aloqa } from './aloqa';
import { Auth } from './auth';
import type { Sotuv, SotuvYaratish } from '../api/turlar';

/** Navbatdagi sotuv: so'rov tanasi + ko'rsatish uchun qo'shimcha ma'lumot. Kalit — IdempotencyKey. */
export interface NavbatSotuvi {
  idempotencyKey: string;
  foydalanuvchiId: number;
  sorov: SotuvYaratish;
  yaratildi: string;
  /** Ko'rsatish uchun (serverdan javob kelguncha). */
  aparatRaqami: number;
  yoqilgiNomi: string;
  summa: number;
  litr: number;
  /** Server rad etgan bo'lsa (4xx) — sabab; qayta yuborilmaydi, foydalanuvchi o'chiradi. */
  xato?: string;
}

const BAZA = 'fuelcontrol';
const JADVAL = 'sotuvNavbati';

/**
 * Offline sotuv navbati. Har sotuv avval IndexedDB'ga yoziladi, keyin yuboriladi; muvaffaqiyatda o'chiriladi.
 * Aloqa uzilsa yozuv qoladi va internet/hub tiklanganda qayta yuboriladi. Server IdempotencyKey bo'yicha
 * takrorni tanib, mavjud sotuvni qaytaradi — shuning uchun ikki marta yuborilsa ham bitta sotuv yoziladi.
 */
@Injectable({ providedIn: 'root' })
export class Navbat {
  private readonly aloqa = inject(Aloqa);
  private readonly auth = inject(Auth);
  private baza: Promise<IDBPDatabase> | null = null;
  private ishlayapti = false;

  readonly royxat = signal<NavbatSotuvi[]>([]);
  readonly kutilayotgan = computed(() => this.royxat().filter((x) => !x.xato));
  /** Serverga yetib borgan sotuvlar (sahifa ro'yxatini yangilash uchun). */
  readonly yuborildi$ = new Subject<Sotuv>();

  constructor() {
    this.aloqa.tiklandi$.subscribe(() => this.yubor());
    setInterval(() => this.yubor(), 30000);
    effect(() => { this.auth.foydalanuvchi(); this.yangila().then(() => this.yubor()); });
  }

  private db() {
    this.baza ??= openDB(BAZA, 1, {
      upgrade(db) { db.createObjectStore(JADVAL, { keyPath: 'idempotencyKey' }); },
    });
    return this.baza;
  }

  private async yangila() {
    try {
      const id = this.auth.foydalanuvchi()?.id;
      const hammasi = (await (await this.db()).getAll(JADVAL)) as NavbatSotuvi[];
      this.royxat.set(hammasi.filter((x) => x.foydalanuvchiId === id).sort((a, b) => a.yaratildi.localeCompare(b.yaratildi)));
    } catch { /* IndexedDB yopiq (private rejim) */ }
  }

  /**
   * Sotuvni navbatga yozadi va darhol yuborishga urinadi.
   * Natija: serverdan sotuv, yoki null — navbatda qoldi (aloqa yo'q). Server rad etsa ApiXato otiladi va yozuv o'chiriladi.
   */
  async qosh(y: NavbatSotuvi): Promise<Sotuv | null> {
    let saqlandi = true;
    try { await (await this.db()).put(JADVAL, y); } catch { saqlandi = false; }
    await this.yangila();
    try {
      const s = await this.bittasi(y.sorov);
      await this.ochir(y.idempotencyKey);
      return s;
    } catch (e) {
      if (saqlandi && (e instanceof AloqaXato || (e instanceof ApiXato && (e.status === 401 || e.status >= 500)))) return null;
      await this.ochir(y.idempotencyKey);
      throw e;
    }
  }

  async ochir(kalit: string) {
    try { await (await this.db()).delete(JADVAL, kalit); } catch { /* */ }
    await this.yangila();
  }

  /** Navbatdagilarni ketma-ket yuboradi. Aloqa xatosida to'xtaydi; 4xx bo'lsa yozuvga xato belgilanadi. */
  async yubor() {
    if (this.ishlayapti || !this.auth.kirganmi()) return;
    this.ishlayapti = true;
    try {
      await this.yangila();
      for (const y of this.kutilayotgan()) {
        try {
          const s = await this.bittasi(y.sorov);
          await (await this.db()).delete(JADVAL, y.idempotencyKey);
          this.yuborildi$.next(s);
        } catch (e) {
          if (e instanceof AloqaXato) break;
          if (e instanceof ApiXato && (e.status === 401 || e.status >= 500)) break;
          await (await this.db()).put(JADVAL, { ...y, xato: (e as Error).message });
        }
      }
    } finally {
      this.ishlayapti = false;
      await this.yangila();
    }
  }

  private bittasi(s: SotuvYaratish) {
    return ol(api.POST('/sotuvlar', { body: s }));
  }
}
