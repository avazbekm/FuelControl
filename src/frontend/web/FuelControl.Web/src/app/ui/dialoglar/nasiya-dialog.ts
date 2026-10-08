import { Component, computed, effect, inject, model, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { harflar, hozirToshkent, kunFarqi, kunOy, kunQosh, pul } from '../../core/format';
import { telefonFormat, telefonRaqamlar } from '../../core/telefon';
import type { MijozTaklifDto } from '../../api/model';
import { Ikon } from '../ikon';
import { Oyna } from '../oyna';
import { EnterKeyingi } from '../enter-keyingi';
import { SonKiritish } from '../son-kiritish';
import { TelefonKiritish } from '../telefon-kiritish';

const MUDDATLAR = [{ kalit: 'Nasiya_Muddat3Kun', kun: 3 }, { kalit: 'Nasiya_Muddat1Hafta', kun: 7 }, { kalit: 'Nasiya_Muddat2Hafta', kun: 14 }, { kalit: 'Nasiya_Muddat1Oy', kun: 31 }];
const KECHIKISH_MS = 250;

interface Xatolar { ism?: string; aloqa?: string; telefon?: string; summa?: string; muddat?: string }

/**
 * "Nasiya yozish" dialogi (docs/dizayn/NasiyaQoshish): ochiq smenaga nasiya qo'shadi.
 * Qoidalar (docs §7.10): ism, summa > 0, muddat majburiy; telefon yoki mashina raqamidan kamida bittasi; muddat bugundan oldin emas.
 * Mavjud mijoz (docs §8.2): ism, telefon yoki raqam yozilganda `/nasiyalar/mijozlar` dan takliflar chiqadi; tanlansa uchala maydon to'ladi.
 * Telefon to'liq yozilib, mavjud mijozga to'g'ri kelsa — bo'sh maydonlar o'zi to'ladi va "Mavjud mijoz · faol qarz X" izohi chiqadi.
 */
@Component({
  selector: 'nasiya-dialog',
  imports: [FormsModule, Ikon, Oyna, SonKiritish, TelefonKiritish, EnterKeyingi],
  template: `
    <oyna [(ochiq)]="ochiq" [sarlavha]="til.t('Nasiya_Yozish')" [tagsarlavha]="smenaMatn()" ikon="book" ikonRang="to-rang">
      <form class="forma-ustun" enterKeyingi (ngSubmit)="saqla()" (input)="qaytaTekshir()" (change)="qaytaTekshir()" autocomplete="off" novalidate>
        <!-- Mijoz: ism, telefon, mashina raqami + mavjud mijoz takliflari -->
        <div class="mijoz-guruh" (focusout)="fokusChiqdi($event)" (keydown)="taklifTugma($event)">
          <div class="maydon">
            <label for="nd-ism">{{ til.t('Nasiya_MijozIsmi') }}</label>
            <input id="nd-ism" class="kiritish" [class.xato]="xatolar().ism" name="ism" [ngModel]="ism()" (ngModelChange)="ismOzgar($event)"
                   [placeholder]="til.t('Nasiya_IsmPlaceholder')" autocomplete="off" data-avto role="combobox" aria-autocomplete="list"
                   [attr.aria-expanded]="takliflarOchiq()" aria-controls="nd-takliflar" [attr.aria-invalid]="!!xatolar().ism" />
            @if (xatolar().ism) { <span class="xato-matn" role="alert">{{ xatolar().ism }}</span> }
          </div>
          @if (takliflarOchiq()) {
            <div class="takliflar" id="nd-takliflar" role="listbox" (mousedown)="$event.preventDefault()">
              <span class="taklif-sarlavha">{{ til.t('Nasiya_MavjudMijozlar') }}</span>
              @for (m of takliflar(); track $index) {
                <button type="button" role="option" class="taklif" [class.faol]="$index === faolIndeks()" [attr.aria-selected]="$index === faolIndeks()" (click)="tanla(m)">
                  <span class="avatar">{{ harflar(m.mijozIsmi) }}</span>
                  <span class="taklif-matn">
                    <b>{{ m.mijozIsmi }}</b>
                    <span>{{ telFormat(m.telefon) }}@if (m.telefon && m.mashinaRaqami) { · }{{ m.mashinaRaqami }}</span>
                  </span>
                  <span class="taklif-qarz" [class.bor]="m.faolQarz > 0">{{ m.faolQarz > 0 ? til.t('Nasiya_TaklifQarz', pul(m.faolQarz)) : til.t('Nasiya_TaklifQarzYoq') }}</span>
                </button>
              }
            </div>
          }
          <div class="ikki-ustun">
            <div class="maydon">
              <label for="nd-tel">{{ til.t('Nasiya_Telefon') }}</label>
              <telefon-kiritish [qiymat]="tel()" (qiymatChange)="telOzgar($event)" [sinf]="xatolar().aloqa || xatolar().telefon ? 'kiritish son xato' : 'kiritish son'" inputId="nd-tel" />
              @if (xatolar().telefon) { <span class="xato-matn" role="alert">{{ xatolar().telefon }}</span> }
            </div>
            <div class="maydon">
              <label for="nd-raqam">{{ til.t('Nasiya_MashinaRaqami') }}</label>
              <input id="nd-raqam" class="kiritish mashina" [class.xato]="xatolar().aloqa" name="raqam" [ngModel]="raqam()" (ngModelChange)="raqamOzgar($event)"
                     placeholder="01 A 123 BC" autocomplete="off" autocapitalize="characters" />
            </div>
          </div>
          @if (xatolar().aloqa) { <span class="xato-matn aloqa-xato" role="alert">{{ xatolar().aloqa }}</span> }
          @if (mavjud(); as m) {
            <div class="mavjud-izoh" role="status"><ikon nomi="check" [olcham]="14" [qalinlik]="2.4" /> {{ til.t('Nasiya_MavjudMijoz', pul(m.faolQarz)) }}</div>
          }
        </div>
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
          <textarea id="nd-izoh" class="kiritish" name="izoh" rows="2" [(ngModel)]="izoh" [placeholder]="til.t('Nasiya_IzohPlaceholder')"></textarea>
        </div>
        <div class="malumot-blok"><ikon nomi="info" [olcham]="16" [qalinlik]="2" /><span>{{ til.t('Nasiya_YozishIzoh') }}</span></div>
        @if (serverXato()) { <div class="xato-matn" role="alert">{{ serverXato() }}</div> }
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
    .mijoz-guruh { display: flex; flex-direction: column; gap: 14px; }
    .sana-qator { gap: 10px; }
    .sana-yorliq { font-size: 13px; font-weight: 600; color: var(--matn-3); }
    .kiritish.sana { flex: 1 1 180px; min-width: 0; width: auto; }
    .aloqa-xato { margin-top: -8px; }
    .takliflar {
      display: flex; flex-direction: column; gap: 2px; padding: 8px; max-height: 232px; overflow-y: auto; border-radius: 20px;
      background: var(--plitka-fon); border: 1px solid var(--plitka-chegara); box-shadow: 0 10px 26px rgba(28, 54, 110, 0.10);
    }
    .taklif-sarlavha { padding: 2px 8px 6px; font-size: 11px; font-weight: 700; letter-spacing: 1px; text-transform: uppercase; color: var(--matn-2); }
    .taklif { display: flex; align-items: center; gap: 10px; padding: 8px 10px; border: 0; border-radius: 14px; background: transparent; cursor: pointer; text-align: left; color: inherit; min-height: 48px; }
    .taklif:hover, .taklif.faol { background: var(--asosiy-och); }
    .taklif .avatar { width: 34px; height: 34px; font-size: 12px; }
    .taklif-matn { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
    .taklif-matn b { font-size: 14px; font-weight: 700; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .taklif-matn span { font-size: 12.5px; color: var(--matn-2); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .taklif-qarz { flex: none; padding: 3px 10px; border-radius: 999px; font-size: 12px; font-weight: 700; background: var(--b-kul-fon); color: var(--b-kul); }
    .taklif-qarz.bor { background: var(--b-sariq-fon); color: var(--b-sariq); }
    .mavjud-izoh {
      display: flex; align-items: center; gap: 8px; padding: 9px 14px; border-radius: 14px; font-size: 13px; font-weight: 600;
      background: var(--b-yashil-fon); color: var(--b-yashil);
    }
  `,
})
export class NasiyaDialog {
  protected readonly til = inject(Til);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);

  readonly ochiq = model(false);
  readonly saqlandi = output<void>();

  protected readonly pul = pul;
  protected readonly harflar = harflar;
  protected readonly tel = signal('');
  protected readonly telFormat = telefonFormat;
  protected readonly muddatlar = MUDDATLAR;
  protected readonly bugun = hozirToshkent().kun;
  protected readonly smenaMatn = signal('');
  protected readonly band = signal(false);
  protected readonly xatolar = signal<Xatolar>({});
  protected readonly serverXato = signal<string | null>(null);
  private urinildi = false;
  protected readonly ism = signal('');
  protected readonly raqam = signal('');
  protected readonly summa = signal<number | null>(null);
  protected readonly sana = signal(kunQosh(this.bugun, 7));
  protected izoh = '';

  // Mavjud mijoz takliflari
  protected readonly takliflar = signal<MijozTaklifDto[]>([]);
  protected readonly takliflarOchiq = signal(false);
  protected readonly faolIndeks = signal(-1);
  protected readonly mavjud = signal<MijozTaklifDto | null>(null);
  private qidirTaymer: ReturnType<typeof setTimeout> | undefined;
  private qidirTartibi = 0;

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
        this.takliflarniYop(); this.mavjud.set(null);
        this.smenaMatn.set('');
        this.server.joriySmena().then((t) => this.smenaMatn.set(t ? this.til.t('Smena_Operator', t.smena.id, t.smena.operatorIsmi) : '')).catch(() => undefined);
      });
    });
  }

  protected muddatTanla(kun: number) {
    this.sana.set(kunQosh(this.bugun, kun));
  }

  // ---- Mavjud mijoz takliflari (docs §8.2)
  protected ismOzgar(v: string) { this.ism.set(v); this.mavjud.set(null); this.qidir(v.trim().length >= 2 ? v : null); }
  protected raqamOzgar(v: string) { this.raqam.set(v); this.mavjud.set(null); this.qidir(v.trim().length >= 2 ? v : null); }
  protected telOzgar(v: string) {
    this.tel.set(v);
    this.mavjud.set(null);
    const r = telefonRaqamlar(v);
    if (r.length >= 3) this.qidir(r, r.length === 9 ? r : undefined);
    else this.qidir(null);
  }
  /** `q`: null — takliflarni yopish (so'rov bo'sh bo'lsa takliflar ko'rsatilmaydi, faqat yozish boshlanganda chiqadi). `toliqTelefon`: telefon to'liq yozilgan — mos mijoz bo'lsa maydonlar o'zi to'ladi. */
  private qidir(q: string | null, toliqTelefon?: string) {
    clearTimeout(this.qidirTaymer);
    const tartib = ++this.qidirTartibi;
    if (q === null) { this.takliflarniYop(); return; }
    this.qidirTaymer = setTimeout(async () => {
      try {
        const r = await this.server.mijozlar(q);
        if (tartib !== this.qidirTartibi || !this.ochiq()) return; // eskirgan javob
        if (toliqTelefon) {
          const m = r.find((x) => telefonRaqamlar(x.telefon) === toliqTelefon);
          if (m) { this.avtoToldir(m); this.takliflarniYop(); return; }
        }
        this.takliflar.set(r);
        this.faolIndeks.set(-1);
        this.takliflarOchiq.set(r.length > 0);
      } catch { /* takliflar ixtiyoriy — xato bo'lsa jim */ }
    }, KECHIKISH_MS);
  }

  private takliflarniYop() {
    clearTimeout(this.qidirTaymer);
    this.qidirTartibi++;
    this.takliflarOchiq.set(false);
    this.faolIndeks.set(-1);
  }

  /** Tanlanganda: ism, telefon va raqam to'ladi. */
  protected tanla(m: MijozTaklifDto) {
    this.ism.set(m.mijozIsmi);
    this.tel.set(telefonFormat(m.telefon));
    this.raqam.set(m.mashinaRaqami);
    this.mavjud.set(m);
    this.takliflarniYop();
    this.xatolar.set({});
    this.urinildi = false;
  }

  /** Telefon bo'yicha topilganda faqat bo'sh maydonlar to'ladi (yozilgan narsa o'chmaydi). */
  private avtoToldir(m: MijozTaklifDto) {
    if (!this.ism().trim()) this.ism.set(m.mijozIsmi);
    if (!this.raqam().trim()) this.raqam.set(m.mashinaRaqami);
    this.mavjud.set(m);
  }

  protected fokusChiqdi(e: FocusEvent) {
    const guruh = e.currentTarget as HTMLElement;
    if (!e.relatedTarget || !guruh.contains(e.relatedTarget as Node)) this.takliflarniYop();
  }

  /** Klaviatura: ↑/↓ — tanlash, Enter — tanlangan taklifni qo'yish, Esc — yopish (dialog yopilmaydi). */
  protected taklifTugma(e: KeyboardEvent) {
    if (!this.takliflarOchiq()) return;
    const n = this.takliflar().length;
    if (e.key === 'ArrowDown') { e.preventDefault(); this.faolIndeks.update((i) => (i + 1) % n); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); this.faolIndeks.update((i) => (i <= 0 ? n - 1 : i - 1)); }
    else if (e.key === 'Enter' && this.faolIndeks() >= 0) { e.preventDefault(); e.stopPropagation(); this.tanla(this.takliflar()[this.faolIndeks()]); }
    else if (e.key === 'Escape') { e.stopPropagation(); this.takliflarniYop(); }
  }

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
    if (!this.tekshir()) return;
    this.band.set(true);
    try {
      await this.server.nasiyaYarat({ mijozIsmi: this.ism().trim(), telefon: this.tel(), mashinaRaqami: this.raqam().trim().toUpperCase(), summa: this.summa()!, muddat: this.sana(), izoh: this.izoh.trim() || null });
      this.ochiq.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
      this.saqlandi.emit();
    } catch (e) {
      this.serverXato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
