import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { kun, pul } from '../../core/format';
import type { Yoqilgi } from '../../api/turlar';
import { Ikon } from '../../ui/ikon';
import { Oyna } from '../../ui/oyna';
import { PALITRA, SozlamalarXizmati, butunSon } from './sozlamalar-xizmati';

/** "Yoqilg'i narxlari": ro'yxat (joriy narx, yangi narx + Saqlash, Tahrirlash), narx tarixi, yoqilg'i dialogi. */
@Component({
  selector: 'sozlamalar-narxlar',
  imports: [FormsModule, Ikon, Oyna],
  template: `
    <div class="panjara narx-panjara">
      <section class="shisha karta">
        <div class="qator ora orala sarlavha-qator">
          <h2 style="margin:0">{{ til.t('YoqilgiNarxlari') }}</h2>
          <button type="button" class="tugma asosiy" (click)="qosh()"><ikon nomi="plus" [olcham]="16" /> {{ til.t('YoqilgiQoshish') }}</button>
        </div>
        <p class="ikkilamchi kichik-matn izoh">{{ til.t('NarxIzoh') }}</p>
        <div class="ustunlar">
          @for (y of x.yoqilgilar(); track y.id) {
            <div class="plitka yoq-qator">
              <span class="nuqta kata" [style.background]="y.rang"></span>
              <div class="nomi">
                <div class="ism">{{ y.nomi }}</div>
                <div class="ikkilamchi kichik-matn">{{ aparatSoni(y.id) }} {{ til.t('TaAparat') }}</div>
              </div>
              <div class="joriy">
                <div class="katta-yorliq">{{ til.t('Joriy') }}</div>
                <div class="narx son">{{ pul(y.narx) }}</div>
              </div>
              <div class="amal">
                <input class="kiritish son" inputmode="numeric" autocomplete="off" [placeholder]="til.t('YangiNarx')" [attr.aria-label]="til.t('YangiNarx') + ' — ' + y.nomi"
                       [ngModel]="yangiNarx()[y.id] ?? ''" (ngModelChange)="narxKirit(y.id, $event)" (keydown.enter)="narxniSaqla(y)" />
                <button type="button" class="tugma asosiy" [disabled]="!narxYaroqli(y) || band() === y.id" (click)="narxniSaqla(y)">{{ til.t('Saqlash') }}</button>
                <button type="button" class="tugma" (click)="tahrirla(y)">{{ til.t('Tahrirlash') }}</button>
              </div>
            </div>
          } @empty {
            <div class="bosh">{{ til.t('MalumotYoq') }}</div>
          }
        </div>
      </section>

      <section class="shisha karta tarix-karta">
        <h2>{{ til.t('NarxTarixi') }}</h2>
        <!-- Keng ekran: jadval (karta balandligi cheklangan, ichida skroll) -->
        <div class="faqat-keng tarix-skroll">
          <table class="jadval tarix-jadval">
            <colgroup><col style="width:21%" /><col style="width:24%" /><col style="width:16%" /><col style="width:16%" /><col style="width:23%" /></colgroup>
            <thead><tr><th>{{ til.t('Sana') }}</th><th>{{ til.t('Yoqilgi') }}</th><th class="o">{{ til.t('Eski') }}</th><th class="o">{{ til.t('Yangi') }}</th><th>{{ til.t('Kim') }}</th></tr></thead>
            <tbody>
              @for (n of x.narxTarixi(); track $index) {
                <tr>
                  <td>{{ kun(n.vaqt) }}</td>
                  <td [title]="n.yoqilgi">{{ n.yoqilgi }}</td>
                  <td class="o">{{ pul(n.eskiNarx) }}</td>
                  <td class="o"><b>{{ pul(n.yangiNarx) }}</b></td>
                  <td [title]="n.kim">{{ n.kim }}</td>
                </tr>
              } @empty { <tr><td colspan="5" class="bosh">{{ til.t('MalumotYoq') }}</td></tr> }
            </tbody>
          </table>
        </div>
        <!-- Telefon: kartalar — oxirgi 10 ta, "Hammasini ko'rsatish" bilan to'liq -->
        <div class="faqat-tor">
          <div class="royxat">
            @for (n of tarixKorinadigan(); track $index) {
              <div class="element">
                <span class="matnlar">
                  <span class="asosiy-matn">{{ n.yoqilgi }}</span>
                  <span class="ikkinchi son">{{ pul(n.eskiNarx) }} → <b>{{ pul(n.yangiNarx) }}</b></span>
                  <span class="ikkinchi">{{ n.kim }}</span>
                </span>
                <span class="ong ikkilamchi kichik-matn son">{{ kun(n.vaqt) }}</span>
              </div>
            } @empty { <div class="bosh">{{ til.t('MalumotYoq') }}</div> }
          </div>
          @if (!tarixHammasi() && x.narxTarixi().length > TARIX_TELEFON) {
            <button type="button" class="tugma keng" (click)="tarixHammasi.set(true)">{{ til.t('HammasiniKorsat') }} ({{ x.narxTarixi().length }})</button>
          }
        </div>
      </section>
    </div>

    <oyna [(ochiq)]="dialog" [sarlavha]="tahrirY() ? til.t('YoqilginiTahrirlash') : til.t('YangiYoqilgi')" [kenglik]="460">
      <form class="forma-ustun" (ngSubmit)="saqla()">
        <div class="maydon">
          <label class="katta-yorliq" for="y-nomi">{{ til.t('Nomi') }}</label>
          <input id="y-nomi" class="kiritish" name="nomi" style="font-weight:700" [(ngModel)]="nomi" [placeholder]="til.t('NomiMisol')" autocomplete="off" data-avto />
        </div>
        <div class="maydon">
          <label class="katta-yorliq" for="y-narx">{{ til.t('Narx') }}</label>
          <input id="y-narx" class="kiritish son" name="narx" inputmode="numeric" style="text-align:right" [(ngModel)]="narx" placeholder="0" autocomplete="off" />
        </div>
        <div class="maydon">
          <span class="katta-yorliq">{{ til.t('Rang') }}</span>
          <div class="ranglar" role="radiogroup" [attr.aria-label]="til.t('Rang')">
            @for (r of palitra; track r) {
              <button type="button" class="rang" role="radio" [attr.aria-checked]="rang() === r" [attr.aria-label]="r" (click)="rang.set(r)">
                <span class="doira" [style.background]="r" [class.tanlangan]="rang() === r">
                  @if (rang() === r) { <ikon nomi="check" [olcham]="16" [qalinlik]="2.6" /> }
                </span>
              </button>
            }
          </div>
        </div>
        @if (xato()) { <div class="xato-matn" role="alert">{{ xato() }}</div> }
        <div class="amallar ikki-chet">
          @if (tahrirY()) { <button type="button" class="tugma xavfli" [disabled]="band() === -1" (click)="ochir()">{{ til.t('Ochirish') }}</button> }
          <span class="bosh-joy"></span>
          <button type="button" class="tugma" (click)="dialog.set(false)">{{ til.t('BekorQilish') }}</button>
          <button type="submit" class="tugma asosiy" [disabled]="band() === -1">
            @if (band() === -1) { <span class="aylanma"></span> } {{ til.t('Saqlash') }}
          </button>
        </div>
      </form>
    </oyna>
  `,
  styles: `
    .narx-panjara { align-items: start; }
    @media (min-width: 1100px) { .narx-panjara { grid-template-columns: minmax(0, 1fr) 420px; } }
    .sarlavha-qator { margin-bottom: 4px; }
    .izoh { margin: 8px 0 14px; }
    .yoq-qator { display: flex; flex-wrap: wrap; align-items: center; gap: 12px 14px; }
    .kata { width: 12px; height: 12px; }
    .nomi { flex: 1 1 120px; min-width: 0; }
    .ism { font-weight: 700; font-size: 15px; }
    .joriy .narx { font-weight: 700; font-size: 16px; }
    .amal { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; flex: 1 1 100%; }
    .amal .kiritish { flex: 1 1 100%; min-width: 0; text-align: right; }
    .amal .tugma { flex: 1 1 0; }
    @media (min-width: 520px) { .amal { flex-wrap: nowrap; } .amal .kiritish { flex: 1 1 110px; } .amal .tugma { flex: 0 0 auto; } }
    @media (min-width: 700px) { .amal { flex: 0 0 auto; } .amal .kiritish { flex: 0 0 140px; } }
    /* Narx tarixi: keng ekranda karta balandligi cheklangan (ichida skroll), ustunlar qat'iy — "Kim" chetdan chiqmaydi. */
    .tarix-karta { display: flex; flex-direction: column; }
    .tarix-karta .faqat-tor .keng { margin-top: 8px; }
    @media (min-width: 700px) { .tarix-karta { max-height: 520px; } }
    .tarix-skroll { overflow: auto; min-height: 0; flex: 1 1 auto; margin: 0 -6px; padding: 0 6px; }
    .tarix-jadval { table-layout: fixed; width: 100%; }
    .tarix-jadval th, .tarix-jadval td { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; padding: 9px 6px; }
    .tarix-jadval thead th { position: sticky; top: 0; z-index: 1; background: var(--fon); box-shadow: 0 1px 0 var(--chiziq); }
    .ranglar { display: flex; flex-wrap: wrap; gap: 4px; }
    .rang { width: 44px; height: 44px; border: 0; background: none; padding: 0; display: inline-flex; align-items: center; justify-content: center; cursor: pointer; border-radius: 999px; }
    .doira { width: 34px; height: 34px; border-radius: 999px; border: 3px solid transparent; display: inline-flex; align-items: center; justify-content: center; color: #fff; transition: transform 0.12s; }
    .doira.tanlangan { border-color: var(--matn); transform: scale(1.06); }
    .ikki-chet { align-items: center; }
    .ikki-chet .bosh-joy { flex: 1; }
  `,
})
export class NarxlarBolimi {
  protected readonly til = inject(Til);
  protected readonly x = inject(SozlamalarXizmati);
  private readonly bildirish = inject(Bildirish);

  protected readonly pul = pul;
  protected readonly kun = kun;
  protected readonly palitra = PALITRA;
  protected readonly TARIX_TELEFON = 10;
  /** Telefonda tarix uzun bo'lib ketmasin: oxirgi 10 ta, "Hammasini ko'rsatish" bosilsa — hammasi (API yangisini birinchi beradi). */
  protected readonly tarixHammasi = signal(false);
  protected readonly tarixKorinadigan = computed(() => {
    const t = this.x.narxTarixi();
    return this.tarixHammasi() ? t : t.slice(0, this.TARIX_TELEFON);
  });

  /** Har yoqilg'i qatoridagi "Yangi narx" matni (id bo'yicha) — yuklanishlar kiritilayotgan matnni o'chirmaydi. */
  protected readonly yangiNarx = signal<Record<number, string>>({});
  /** Saqlanayotgan yoqilg'i id'si (-1 — dialog). */
  protected readonly band = signal<number | null>(null);

  protected readonly dialog = signal(false);
  protected readonly tahrirY = signal<Yoqilgi | null>(null);
  protected readonly xato = signal<string | null>(null);
  protected nomi = '';
  protected narx = '';
  protected readonly rang = signal(PALITRA[0]);

  private readonly aparatSonlari = computed(() => {
    const m = new Map<number, number>();
    for (const a of this.x.aparatlar()) m.set(a.yoqilgiTuriId, (m.get(a.yoqilgiTuriId) ?? 0) + 1);
    return m;
  });
  protected aparatSoni(id: number) { return this.aparatSonlari().get(id) ?? 0; }

  protected narxKirit(id: number, m: string) { this.yangiNarx.update((o) => ({ ...o, [id]: m })); }
  protected narxYaroqli(y: Yoqilgi) {
    const n = butunSon(this.yangiNarx()[y.id] ?? '');
    return n > 0 && n !== y.narx;
  }

  async narxniSaqla(y: Yoqilgi) {
    if (!this.narxYaroqli(y) || this.band() !== null) return;
    this.band.set(y.id);
    try {
      await this.x.yoqilgiTahrirla(y.id, y.nomi, butunSon(this.yangiNarx()[y.id]), y.rang);
      this.yangiNarx.update((o) => ({ ...o, [y.id]: '' }));
      this.bildirish.korsat(this.til.t('Saqlandi'));
    } catch (e) {
      this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    } finally {
      this.band.set(null);
    }
  }

  protected qosh() { this.dialogOch(null); }
  protected tahrirla(y: Yoqilgi) { this.dialogOch(y); }

  private dialogOch(y: Yoqilgi | null) {
    this.tahrirY.set(y);
    this.nomi = y?.nomi ?? '';
    this.narx = y ? pul(y.narx) : '';
    // Yangi yoqilg'i uchun ishlatilmagan birinchi rang (desktop bilan bir xil).
    this.rang.set(y?.rang ?? PALITRA.find((r) => !this.x.yoqilgilar().some((q) => q.rang === r)) ?? PALITRA[0]);
    this.xato.set(null);
    this.dialog.set(true);
  }

  async saqla() {
    const nomi = this.nomi.trim();
    const narx = butunSon(this.narx);
    const t = this.tahrirY();
    if (!nomi || narx <= 0) return this.xato.set(this.til.t('Xato_Maydon'));
    if (this.x.yoqilgilar().some((y) => y.nomi.toLowerCase() === nomi.toLowerCase() && y.id !== t?.id)) return this.xato.set(this.til.t('Xato_YoqilgiBand'));
    this.band.set(-1);
    try {
      if (t) await this.x.yoqilgiTahrirla(t.id, nomi, narx, this.rang());
      else await this.x.yoqilgiYarat(nomi, narx, this.rang());
      this.dialog.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(null);
    }
  }

  async ochir() {
    const t = this.tahrirY();
    if (!t) return;
    // Aparatga biriktirilgan yoqilg'ini o'chirib bo'lmaydi (server ham rad etadi).
    if (this.aparatSoni(t.id) > 0) return this.xato.set(this.til.t('Xato_YoqilgiIshlatilmoqda'));
    this.band.set(-1);
    try {
      await this.x.yoqilgiOchir(t.id);
      this.dialog.set(false);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(null);
    }
  }
}
