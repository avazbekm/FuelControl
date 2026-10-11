import { Component, inject, input, model, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../core/til';
import { Server } from '../core/server';
import { harflar, pul } from '../core/format';
import { telefonFormat, telefonRaqamlar } from '../core/telefon';
import type { MijozTaklifDto } from '../api/model';
import { Ikon } from './ikon';
import { TelefonKiritish } from './telefon-kiritish';

const KECHIKISH_MS = 250;

export interface MijozXatolari { ism?: string; telefon?: string; aloqa?: string }

/**
 * Mijoz maydonlari (docs §8.2, §8.8): ism, telefon, mashina raqami + mavjud mijoz takliflari. "Nasiya yozish" va "Qarz qaytdi → Eski qarz" dialoglari ishlatadi.
 * Maydon id'lari `{id}-ism`, `{id}-tel`, `{id}-raqam`. Taklif tanlangach fokus birinchi bo'sh maydonga, undan keyin `keyingi` id'li maydonga o'tadi.
 */
@Component({
  selector: 'mijoz-maydonlari',
  imports: [FormsModule, Ikon, TelefonKiritish],
  host: { style: 'display: block' },
  template: `
    <div class="mijoz-guruh" (focusout)="fokusChiqdi($event)" (keydown)="taklifTugma($event)">
      <div class="maydon ism-maydon">
        <label [attr.for]="prefiks() + '-ism'">{{ til.t('Nasiya_MijozIsmi') }}</label>
        <input [id]="prefiks() + '-ism'" class="kiritish" [class.xato]="xatolar().ism" name="ism" [ngModel]="ism()" (ngModelChange)="ismOzgar($event)"
               (keydown.enter)="ismEnter($event)" enterkeyhint="next" [placeholder]="til.t('Nasiya_IsmPlaceholder')" autocomplete="off" data-avto role="combobox" aria-autocomplete="list"
               [attr.aria-expanded]="takliflarOchiq()" [attr.aria-controls]="prefiks() + '-takliflar'" [attr.aria-invalid]="!!xatolar().ism" />
        @if (xatolar().ism) { <span class="xato-matn" role="alert">{{ xatolar().ism }}</span> }
        @if (takliflarOchiq()) {
          <div class="takliflar" [id]="prefiks() + '-takliflar'" role="listbox" (mousedown)="$event.preventDefault()">
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
      </div>
      <div class="ikki-ustun">
        <div class="maydon">
          <label [attr.for]="prefiks() + '-tel'">{{ til.t('Nasiya_Telefon') }}</label>
          <telefon-kiritish [qiymat]="tel()" (qiymatChange)="telOzgar($event)" [sinf]="xatolar().aloqa || xatolar().telefon ? 'kiritish son xato' : 'kiritish son'" [inputId]="prefiks() + '-tel'" />
          @if (xatolar().telefon) { <span class="xato-matn" role="alert">{{ xatolar().telefon }}</span> }
        </div>
        <div class="maydon">
          <label [attr.for]="prefiks() + '-raqam'">{{ til.t('Nasiya_MashinaRaqami') }}</label>
          <input [id]="prefiks() + '-raqam'" class="kiritish mashina" [class.xato]="xatolar().aloqa" name="raqam" [ngModel]="raqam()" (ngModelChange)="raqamOzgar($event)"
                 placeholder="01 A 123 BC" autocomplete="off" autocapitalize="characters" />
        </div>
      </div>
      @if (xatolar().aloqa) { <span class="xato-matn aloqa-xato" role="alert">{{ xatolar().aloqa }}</span> }
      @if (mavjud(); as m) {
        <div class="mavjud-izoh" role="status"><ikon nomi="check" [olcham]="14" [qalinlik]="2.4" /> {{ til.t('Nasiya_MavjudMijoz', pul(m.faolQarz)) }}</div>
      }
    </div>
  `,
  styles: `
    .mijoz-guruh { display: flex; flex-direction: column; gap: 14px; }
    .aloqa-xato { margin-top: -8px; }
    .ism-maydon { position: relative; }
    /* Ro'yxat maydon ustida suzadi (absolut): yopilganda ostidagi tarkib (Saqlash tugmasi) siljimaydi — aks holda mousedown/mouseup orasida tugma qochib, birinchi bosish yo'qolardi. */
    .takliflar {
      position: absolute; left: 0; right: 0; top: calc(100% + 4px); z-index: 30;
      display: flex; flex-direction: column; gap: 2px; padding: 8px; max-height: 232px; overflow-y: auto; border-radius: 20px;
      background: var(--dialog); border: 1px solid var(--plitka-chegara); box-shadow: 0 14px 34px rgba(28, 54, 110, 0.18);
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
export class MijozMaydonlari {
  protected readonly til = inject(Til);
  private readonly server = inject(Server);

  /** Id old qo'shimchasi: maydonlar `{id}-ism`, `{id}-tel`, `{id}-raqam`. */
  readonly prefiks = input.required<string>();
  /** Mijoz maydonlaridan keyingi maydon id'si (taklif tanlangach, telefon va raqam to'lgan bo'lsa fokus shu yerga o'tadi). */
  readonly keyingi = input.required<string>();
  /** Dialog ochiq (eskirgan taklif javoblari yopilgan dialogda ko'rinmasin). */
  readonly faol = input(true);
  readonly ism = model('');
  readonly tel = model('');
  readonly raqam = model('');
  readonly xatolar = input<MijozXatolari>({});
  /** Taklifdan mijoz tanlandi — ota tekshiruv xatolarini tozalaydi. */
  readonly tanlandi = output<void>();

  protected readonly pul = pul;
  protected readonly harflar = harflar;
  protected readonly telFormat = telefonFormat;
  protected readonly takliflar = signal<MijozTaklifDto[]>([]);
  protected readonly takliflarOchiq = signal(false);
  protected readonly faolIndeks = signal(-1);
  protected readonly mavjud = signal<MijozTaklifDto | null>(null);
  private qidirTaymer: ReturnType<typeof setTimeout> | undefined;
  private qidirTartibi = 0;

  /** Dialog qayta ochilganda: takliflar yopiladi, "Mavjud mijoz" izohi o'chadi. */
  tozala() { this.takliflarniYop(); this.mavjud.set(null); }

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
        if (tartib !== this.qidirTartibi || !this.faol()) return; // eskirgan javob
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
    this.tanlandi.emit();
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

  /**
   * Mijoz ismida Enter (docs §8.8): ism bo'sh — fokus joyida; takliflardan biri tanlangan bo'lsa — `taklifTugma` uni qo'yadi;
   * aks holda (yangi mijoz, ro'yxat yopiq yoki hech narsa tanlanmagan) takliflar yopiladi va EnterKeyingi fokusni Telefon'ga o'tkazadi.
   */
  protected ismEnter(e: Event) {
    if ((e as KeyboardEvent).isComposing) return;
    if (!this.ism().trim()) { e.preventDefault(); e.stopPropagation(); return; }
    if (!(this.takliflarOchiq() && this.faolIndeks() >= 0)) this.takliflarniYop();
  }

  /** Taklif tanlangach fokus birinchi bo'sh maydonga o'tadi: Telefon → Mashina raqami → keyingi maydon (odatda Summa). */
  private boshMaydonga() {
    const id = !telefonRaqamlar(this.tel()) ? this.prefiks() + '-tel' : !this.raqam().trim() ? this.prefiks() + '-raqam' : this.keyingi();
    document.getElementById(id)?.focus();
  }

  /** Klaviatura: ↑/↓ — tanlash, Enter — tanlangan taklifni qo'yish, Esc — yopish (dialog yopilmaydi). */
  protected taklifTugma(e: KeyboardEvent) {
    if (!this.takliflarOchiq()) return;
    const n = this.takliflar().length;
    if (e.key === 'ArrowDown') { e.preventDefault(); this.faolIndeks.update((i) => (i + 1) % n); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); this.faolIndeks.update((i) => (i <= 0 ? n - 1 : i - 1)); }
    else if (e.key === 'Enter' && this.faolIndeks() >= 0) { e.preventDefault(); e.stopPropagation(); this.tanla(this.takliflar()[this.faolIndeks()]); this.boshMaydonga(); }
    else if (e.key === 'Escape') { e.stopPropagation(); this.takliflarniYop(); }
  }
}
