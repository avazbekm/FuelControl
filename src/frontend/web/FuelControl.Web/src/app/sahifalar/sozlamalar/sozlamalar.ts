import { Component, computed, effect, inject, signal, untracked } from '@angular/core';
import { Auth } from '../../core/auth';
import { Til, TilKodi } from '../../core/til';
import { Tema, TemaRejimi } from '../../core/tema';
import { Navbat } from '../../core/navbat';
import { Bildirish } from '../../core/bildirish';
import { jonliYangila } from '../../core/malumot';
import { Ikon } from '../../ui/ikon';
import { OrnatishTaklif } from '../../ui/ornatish-taklif';
import { SozlamalarXizmati } from './sozlamalar-xizmati';
import { NarxlarBolimi } from './narxlar';
import { AparatlarBolimi } from './aparatlar';
import { FoydalanuvchilarBolimi } from './foydalanuvchilar';
import { RuxsatlarBolimi } from './ruxsatlar';
import { ZaxiraBolimi } from './zaxira';

const BOLIM_KALIT = 'fc.sozlamalarBolimi';

/**
 * Sozlamalar. "Sozlamalar" ruxsati borlarga (Admin) — desktop bilan bir xil 5 bo'lim: yoqilg'i narxlari, aparatlar,
 * foydalanuvchilar, ruxsatlar, zaxira nusxa; tepada til va tema tanlovi. Boshqalarga — faqat til, tema, o'rnatish va chiqish.
 */
@Component({
  selector: 'sozlamalar-sahifa',
  imports: [Ikon, OrnatishTaklif, NarxlarBolimi, AparatlarBolimi, FoydalanuvchilarBolimi, RuxsatlarBolimi, ZaxiraBolimi],
  providers: [SozlamalarXizmati],
  templateUrl: './sozlamalar.html',
  styleUrl: './sozlamalar.scss',
})
export class SozlamalarSahifa {
  protected readonly auth = inject(Auth);
  protected readonly til = inject(Til);
  protected readonly tema = inject(Tema);
  protected readonly navbat = inject(Navbat);
  protected readonly x = inject(SozlamalarXizmati);
  private readonly bildirish = inject(Bildirish);

  /** Desktop'dagi Sozlamalar bo'limi — faqat shu ruxsat bilan. */
  protected readonly admin = computed(() => this.auth.bor('Sozlamalar'));

  protected readonly tillar: { kod: TilKodi; kalit: string; qisqa: string }[] = [
    { kod: 'uz', kalit: 'Lotin', qisqa: 'UZ' }, { kod: 'uzk', kalit: 'Kirill', qisqa: 'ЎЗ' }, { kod: 'ru', kalit: 'Rus', qisqa: 'RU' },
  ];
  protected readonly rejimlar: { kod: TemaRejimi; kalit: string; ikon: string }[] = [
    { kod: 'yorug', kalit: 'Yorug', ikon: 'sun' }, { kod: 'qorongi', kalit: 'Qorongi', ikon: 'moon' }, { kod: 'tizim', kalit: 'Tizim', ikon: 'phone' },
  ];
  protected readonly bolimlar = [
    { kalit: 'YoqilgiNarxlari' }, { kalit: 'Aparatlar' }, { kalit: 'Foydalanuvchilar' }, { kalit: 'Ruxsatlar' }, { kalit: 'ZaxiraNusxa' },
  ];
  protected readonly bolim = signal(this.saqlangan());

  constructor() {
    // Ruxsat berilganda (yoki sahifa ochilganda) ma'lumotlar bir marta yuklanadi.
    effect(() => { if (this.admin()) untracked(() => this.yukla()); });
    // Narx boshqa qurilmadan o'zgarsa yoki aloqa tiklansa — ro'yxatlar yangilanadi.
    jonliYangila(() => { if (this.admin()) this.yukla(true); }, (h) => h.turi === 'NarxOzgardi');
  }

  async yukla(jim = false) {
    try {
      await this.x.yukla();
    } catch (e) {
      if (!jim) this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    }
  }

  protected tanla(i: number) {
    this.bolim.set(i);
    try { sessionStorage.setItem(BOLIM_KALIT, String(i)); } catch { /* */ }
  }

  private saqlangan(): number {
    try {
      const i = Number(sessionStorage.getItem(BOLIM_KALIT));
      return Number.isInteger(i) && i >= 0 && i < 5 ? i : 0;
    } catch {
      return 0;
    }
  }
}
