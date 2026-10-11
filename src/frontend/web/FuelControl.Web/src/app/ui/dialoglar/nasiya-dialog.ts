import { Component, computed, effect, inject, model, output, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { hozirToshkent, kunFarqi, kunOy, kunQosh } from '../../core/format';
import { telefonRaqamlar } from '../../core/telefon';
import { Ikon } from '../ikon';
import { Oyna } from '../oyna';
import { EnterKeyingi } from '../enter-keyingi';
import { SonKiritish } from '../son-kiritish';
import { MijozMaydonlari } from '../mijoz-maydonlari';

const MUDDATLAR = [{ kalit: 'Nasiya_Muddat3Kun', kun: 3 }, { kalit: 'Nasiya_Muddat1Hafta', kun: 7 }, { kalit: 'Nasiya_Muddat2Hafta', kun: 14 }, { kalit: 'Nasiya_Muddat1Oy', kun: 31 }];

interface Xatolar { ism?: string; aloqa?: string; telefon?: string; summa?: string; muddat?: string }

/**
 * "Nasiya yozish" dialogi (docs/dizayn/NasiyaQoshish): ochiq smenaga nasiya qo'shadi.
 * Qoidalar (docs §7.10): ism, summa > 0, muddat majburiy; telefon yoki mashina raqamidan kamida bittasi; muddat bugundan oldin emas.
 * Mavjud mijoz (docs §8.2): ism, telefon yoki raqam yozilganda `/nasiyalar/mijozlar` dan takliflar chiqadi; tanlansa uchala maydon to'ladi.
 * Telefon to'liq yozilib, mavjud mijozga to'g'ri kelsa — bo'sh maydonlar o'zi to'ladi va "Mavjud mijoz · faol qarz X" izohi chiqadi.
 */
@Component({
  selector: 'nasiya-dialog',
  imports: [FormsModule, Ikon, Oyna, SonKiritish, MijozMaydonlari, EnterKeyingi],
  template: `
    <oyna [(ochiq)]="ochiq" [sarlavha]="til.t('Nasiya_Yozish')" [tagsarlavha]="smenaMatn()" ikon="book" ikonRang="to-rang">
      <form class="forma-ustun" enterKeyingi (ngSubmit)="saqla()" (input)="qaytaTekshir()" (change)="qaytaTekshir()" autocomplete="off" novalidate>
        <!-- Mijoz: ism, telefon, mashina raqami + mavjud mijoz takliflari (umumiy komponent) -->
        <mijoz-maydonlari prefiks="nd" keyingi="nd-summa" [faol]="ochiq()" [(ism)]="ism" [(tel)]="tel" [(raqam)]="raqam" [xatolar]="xatolar()" (tanlandi)="mijozTanlandi()" />
        <div class="maydon">
          <label for="nd-summa">{{ til.t('Nasiya_QarzSummasi') }}</label>
          <son-kiritish [(qiymat)]="summa" [sinf]="xatolar().summa ? 'kiritish katta xato' : 'kiritish katta'" inputId="nd-summa" />
          @if (xatolar().summa) { <span class="xato-matn" role="alert">{{ xatolar().summa }}</span> }
        </div>
        <div class="maydon">
          <span class="yorliq" id="nd-muddat-l">{{ til.t('Nasiya_Muddat') }}</span>
          <div class="chiplar" role="group" aria-labelledby="nd-muddat-l">
            @for (m of muddatlar; track m.kun) {
              <button type="button" class="chip" [class.tanlangan]="tanlanganKun() === m.kun" [attr.aria-pressed]="tanlanganKun() === m.kun" (click)="muddatTanla(m.kun)">{{ til.t(m.kalit) }}</button>
            }
          </div>
          <div class="qator orala sana-qator">
            <label for="nd-sana" class="sana-yorliq">{{ til.t('Nasiya_Sana') }}</label>
            <input id="nd-sana" class="kiritish sana" [class.xato]="xatolar().muddat" name="sana" type="date" [ngModel]="sana()" (ngModelChange)="sana.set($event)" [min]="bugun" />
            <span class="pill kok katta">{{ muddatIzoh() }}</span>
          </div>
          @if (xatolar().muddat) { <span class="xato-matn" role="alert">{{ xatolar().muddat }}</span> }
        </div>
        <div class="maydon">
          <label for="nd-izoh">{{ til.t('Nasiya_Izoh') }}</label>
          <textarea id="nd-izoh" class="kiritish" name="izoh" rows="2" data-enter-keyingi [(ngModel)]="izoh" [placeholder]="til.t('Nasiya_IzohPlaceholder')"></textarea>
        </div>
        <div class="malumot-blok"><ikon nomi="info" [olcham]="16" [qalinlik]="2" /><span>{{ til.t('Nasiya_YozishIzoh') }}</span></div>
        @if (birinchiXato(); as m) { <div class="xato-matn" role="alert">{{ m }}</div> }
        @if (serverXato()) { <div class="xato-matn" id="nd-xato" role="alert">{{ serverXato() }}</div> }
        <div class="amallar">
          <button type="button" class="tugma" (click)="ochiq.set(false)">{{ til.t('BekorQilish') }}</button>
          <button type="submit" class="tugma asosiy" [disabled]="band()">
            @if (band()) { <span class="aylanma"></span> } @else { <ikon nomi="check" [olcham]="16" [qalinlik]="2.4" /> } {{ til.t('Saqlash') }}
          </button>
        </div>
      </form>
    </oyna>
  `,
  styles: `
    .sana-qator { gap: 10px; }
    .sana-yorliq { font-size: 13px; font-weight: 600; color: var(--matn-3); }
    .kiritish.sana { flex: 1 1 180px; min-width: 0; width: auto; }
  `,
})
export class NasiyaDialog {
  protected readonly til = inject(Til);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);

  readonly ochiq = model(false);
  readonly saqlandi = output<void>();

  protected readonly tel = signal('');
  protected readonly muddatlar = MUDDATLAR;
  protected readonly bugun = hozirToshkent().kun;
  protected readonly smenaMatn = signal('');
  protected readonly band = signal(false);
  protected readonly xatolar = signal<Xatolar>({});
  protected readonly serverXato = signal<string | null>(null);
  /** Birinchi maydon xatosi — "Saqlash" tugmasi yonida ko'rsatiladi (maydon ko'rinish oynasidan tashqarida bo'lsa ham sabab ko'rinsin). */
  protected readonly birinchiXato = computed(() => { const x = this.xatolar(); return x.ism ?? x.telefon ?? x.aloqa ?? x.summa ?? x.muddat ?? null; });
  private urinildi = false;
  protected readonly ism = signal('');
  protected readonly raqam = signal('');
  protected readonly summa = signal<number | null>(null);
  protected readonly sana = signal(kunQosh(this.bugun, 7));
  protected izoh = '';

  private readonly mijoz = viewChild(MijozMaydonlari);

  /** Tanlangan tez muddat (sana shu kunlarga to'g'ri kelsa belgilanadi). */
  protected readonly tanlanganKun = computed(() => {
    const f = kunFarqi(this.bugun, this.sana());
    return MUDDATLAR.some((m) => m.kun === f) ? f : -1;
  });
  protected readonly muddatIzoh = computed(() => {
    const s = this.sana();
    const f = s ? kunFarqi(this.bugun, s) : -1;
    return f >= 0 ? this.til.t('Nasiya_MuddatIzoh', kunOy(s), f) : this.til.t('Nasiya_BoshqaSana');
  });

  constructor() {
    effect(() => {
      if (!this.ochiq()) return;
      untracked(() => {
        this.ism.set(''); this.tel.set(''); this.raqam.set(''); this.summa.set(null); this.izoh = '';
        this.xatolar.set({}); this.serverXato.set(null); this.urinildi = false;
        this.sana.set(kunQosh(this.bugun, 7));
        this.mijoz()?.tozala();
        this.smenaMatn.set('');
        this.server.joriySmena().then((t) => this.smenaMatn.set(t ? this.til.t('Smena_Operator', t.smena.id, t.smena.operatorIsmi) : '')).catch(() => undefined);
      });
    });
  }

  protected muddatTanla(kun: number) {
    this.sana.set(kunQosh(this.bugun, kun));
  }

  /** Taklifdan mijoz tanlandi (docs §8.2): tekshiruv xatolari tozalanadi. */
  protected mijozTanlandi() { this.xatolar.set({}); this.urinildi = false; }

  // ---- Tekshiruv va saqlash
  /** Maydon darajasidagi tekshiruv (server qoidalari bilan bir xil); xatolar maydon tagida ko'rinadi. */
  private tekshir(): boolean {
    const x: Xatolar = {};
    const summa = this.summa();
    if (!this.ism().trim()) x.ism = this.til.t('Nasiya_XatoIsm');
    const tel = telefonRaqamlar(this.tel());
    if (tel && tel.length !== 9) x.telefon = this.til.t('Nasiya_XatoTelefon');
    if (!tel && !this.raqam().trim()) x.aloqa = this.til.t('Nasiya_XatoAloqa');
    if (!summa || summa <= 0) x.summa = this.til.t('Nasiya_XatoSumma');
    if (!this.sana()) x.muddat = this.til.t('Nasiya_XatoMuddat');
    else if (this.sana() < this.bugun) x.muddat = this.til.t('Nasiya_XatoMuddatOtgan');
    this.xatolar.set(x);
    return Object.keys(x).length === 0;
  }

  /** Birinchi yuborishdan keyin maydonlar o'zgarganda xatolar darhol yangilanadi. */
  protected qaytaTekshir() {
    if (this.urinildi) queueMicrotask(() => this.tekshir());
  }

  async saqla() {
    this.serverXato.set(null);
    this.urinildi = true;
    if (!this.tekshir()) { this.xatoMaydonga(); return; }
    this.band.set(true);
    try {
      await this.server.nasiyaYarat({ mijozIsmi: this.ism().trim(), telefon: this.tel(), mashinaRaqami: this.raqam().trim().toUpperCase(), summa: this.summa()!, muddat: this.sana(), izoh: this.izoh.trim() || null });
      this.ochiq.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
      this.saqlandi.emit();
    } catch (e) {
      this.serverXato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
      setTimeout(() => document.getElementById('nd-xato')?.scrollIntoView({ block: 'nearest' }), 50);
    } finally {
      this.band.set(false);
    }
  }

  /** Tekshiruv o'tmasa fokus birinchi xato maydonga o'tadi (ism → telefon/aloqa → summa → muddat). */
  private xatoMaydonga() {
    const x = this.xatolar();
    const id = x.ism ? 'nd-ism' : x.telefon || x.aloqa ? 'nd-tel' : x.summa ? 'nd-summa' : x.muddat ? 'nd-sana' : null;
    const el = id ? document.getElementById(id) : null;
    el?.focus();
    el?.scrollIntoView({ block: 'center' });
  }
}
