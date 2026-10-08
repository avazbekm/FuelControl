import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { kun, pul } from '../../core/format';
import type { YoqilgiTuriDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { Oyna } from '../../ui/oyna';
import { SonKiritish } from '../../ui/son-kiritish';
import { NarxDialog } from './narx-dialog';
import { PALITRA, SozlamalarXizmati } from './sozlamalar-xizmati';

/**
 * "Yoqilg'i narxlari": ro'yxat (joriy narx, "Narxni o'zgartirish" — ochiq smenada aparat ko'rsatkichlari bilan, "Tahrirlash"),
 * narx tarixi va yoqilg'i dialogi (nom, rang; yangi yoqilg'ida narx ham).
 */
@Component({
  selector: 'sozlamalar-narxlar',
  imports: [FormsModule, Ikon, Oyna, SonKiritish, NarxDialog],
  template: `
    <div class="qator-panjara">
      <section class="shisha karta asosiy">
        <div class="karta-bosh sarlavha-qator">
          <div class="sarlavha-matn">
            <h2>{{ til.t('YoqilgiNarxlari') }}</h2>
            <span class="karta-izoh">{{ til.t('Narx_Izoh') }}</span>
          </div>
          <button type="button" class="tugma asosiy" (click)="qosh()"><ikon nomi="plus" [olcham]="16" [qalinlik]="2.4" /> {{ til.t('YoqilgiQoshish') }}</button>
        </div>
        <div class="ustunlar">
          @for (y of x.yoqilgilar(); track y.id) {
            <div class="plitka yoq-qator">
              <span class="nuqta kata" [style.background]="y.rang"></span>
              <div class="nomi">
                <div class="ism">{{ y.nomi }}</div>
                <div class="ikkilamchi kichik-matn">{{ aparatSoni(y.id) }} {{ til.t('TaAparat') }}</div>
              </div>
              <div class="joriy">
                <div class="yorliq">{{ til.t('Joriy') }}</div>
                <div class="narx son">{{ pul(y.narx) }}</div>
              </div>
              <div class="amal">
                <button type="button" class="tugma asosiy" (click)="narxOch(y)"><ikon nomi="tag" [olcham]="16" [qalinlik]="2" /> {{ til.t('Narx_Ozgartirish') }}</button>
                <button type="button" class="tugma" (click)="tahrirla(y)">{{ til.t('Tahrirlash') }}</button>
              </div>
            </div>
          } @empty {
            <div class="malumot-blok" role="status"><ikon nomi="info" [olcham]="16" [qalinlik]="2" /><span>{{ til.t('Bosh_YoqilgiYoq') }}</span></div>
          }
        </div>
      </section>

      <section class="shisha karta yon tarix-karta">
        <h2>{{ til.t('NarxTarixi') }}</h2>
        <!-- Keng ekran: jadval -->
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

    <narx-dialog [(ochiq)]="narxDialog" [yoqilgi]="narxY()" />

    <oyna [(ochiq)]="dialog" [sarlavha]="tahrirY() ? til.t('YoqilginiTahrirlash') : til.t('YangiYoqilgi')" [kenglik]="460" ikon="tag">
      <form class="forma-ustun" (ngSubmit)="saqla()" autocomplete="off" novalidate>
        <div class="maydon">
          <label for="y-nomi">{{ til.t('Nomi') }}</label>
          <input id="y-nomi" class="kiritish qalin" name="nomi" [(ngModel)]="nomi" [placeholder]="til.t('NomiMisol')" autocomplete="off" data-avto />
        </div>
        @if (!tahrirY()) {
          <div class="maydon">
            <label for="y-narx">{{ til.t('Narx') }}</label>
            <son-kiritish [(qiymat)]="narx" sinf="kiritish pul" inputId="y-narx" />
          </div>
        } @else {
          <div class="malumot-blok"><ikon nomi="info" [olcham]="16" [qalinlik]="2" /><span>{{ til.t('Narx_TahrirIzoh') }}</span></div>
        }
        <div class="maydon">
          <span class="yorliq">{{ til.t('Rang') }}</span>
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
          @if (tahrirY()) { <button type="button" class="tugma xavfli" [disabled]="band()" (click)="ochir()">{{ til.t('Ochirish') }}</button> }
          <span class="bosh-joy"></span>
          <button type="button" class="tugma" (click)="dialog.set(false)">{{ til.t('BekorQilish') }}</button>
          <button type="submit" class="tugma asosiy" [disabled]="band()">
            @if (band()) { <span class="aylanma"></span> } @else { <ikon nomi="check" [olcham]="16" [qalinlik]="2.4" /> } {{ til.t('Saqlash') }}
          </button>
        </div>
      </form>
    </oyna>
  `,
  styles: `
    .qator-panjara { align-items: flex-start; }
    .qator-panjara > .asosiy { flex: 999 1 520px; }
    .qator-panjara > .yon { flex: 1 1 460px; }
    .sarlavha-qator { align-items: center; }
    .sarlavha-matn { display: flex; flex-direction: column; gap: 3px; flex: 1 1 280px; max-width: 560px; }
    .sarlavha-matn h2 { font-size: 16px; }
    .yoq-qator { display: flex; flex-wrap: wrap; align-items: center; gap: 12px 14px; }
    .kata { width: 12px; height: 12px; }
    .nomi { flex: 1 1 120px; min-width: 0; }
    .ism { font-weight: 700; font-size: 15px; }
    .joriy .narx { font-weight: 800; font-size: 16px; }
    .amal { display: flex; flex-wrap: wrap; align-items: center; gap: 8px; flex: 1 1 100%; }
    .amal .tugma { flex: 1 1 auto; }
    @media (min-width: 700px) { .amal { flex: 0 0 auto; } .amal .tugma { flex: 0 0 auto; } }
    /* Narx tarixi: keng ekranda karta balandligi cheklangan (ichida skroll), ustunlar qat'iy — "Kim" chetdan chiqmaydi. */
    .tarix-karta { display: flex; flex-direction: column; }
    .tarix-karta .faqat-tor .keng { margin-top: 8px; }
    @media (min-width: 700px) { .tarix-karta { max-height: 520px; } }
    .tarix-skroll { overflow: auto; min-height: 0; flex: 1 1 auto; margin: 0 -6px; padding: 0 6px; }
    .tarix-jadval { table-layout: fixed; width: 100%; }
    .tarix-jadval th, .tarix-jadval td { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; padding: 9px 6px; }
    .tarix-jadval thead th { position: sticky; top: 0; z-index: 1; background: var(--dialog); box-shadow: 0 1px 0 var(--chiziq); }
    .ranglar { display: flex; flex-wrap: wrap; gap: 4px; }
    .rang { width: 44px; height: 44px; border: 0; background: none; padding: 0; display: inline-flex; align-items: center; justify-content: center; cursor: pointer; border-radius: 999px; }
    .doira { width: 34px; height: 34px; border-radius: 999px; border: 3px solid transparent; display: inline-flex; align-items: center; justify-content: center; color: #fff; transition: transform 0.12s; }
    .doira.tanlangan { border-color: var(--matn); transform: scale(1.06); }
    .ikki-chet { align-items: center; }
    .ikki-chet .bosh-joy { flex: 1; }
    :host ::ng-deep .kiritish.qalin { font-weight: 700; }
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

  protected readonly narxDialog = signal(false);
  protected readonly narxY = signal<YoqilgiTuriDto | null>(null);

  protected readonly dialog = signal(false);
  protected readonly tahrirY = signal<YoqilgiTuriDto | null>(null);
  protected readonly band = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected nomi = '';
  protected readonly narx = signal<number | null>(null);
  protected readonly rang = signal(PALITRA[0]);

  private readonly aparatSonlari = computed(() => {
    const m = new Map<number, number>();
    for (const a of this.x.aparatlar()) m.set(a.yoqilgiTuriId, (m.get(a.yoqilgiTuriId) ?? 0) + 1);
    return m;
  });
  protected aparatSoni(id: number) { return this.aparatSonlari().get(id) ?? 0; }

  protected narxOch(y: YoqilgiTuriDto) {
    this.narxY.set(y);
    this.narxDialog.set(true);
  }

  protected qosh() { this.dialogOch(null); }
  protected tahrirla(y: YoqilgiTuriDto) { this.dialogOch(y); }

  private dialogOch(y: YoqilgiTuriDto | null) {
    this.tahrirY.set(y);
    this.nomi = y?.nomi ?? '';
    this.narx.set(y ? y.narx : null);
    // Yangi yoqilg'i uchun ishlatilmagan birinchi rang (desktop bilan bir xil).
    this.rang.set(y?.rang ?? PALITRA.find((r) => !this.x.yoqilgilar().some((q) => q.rang === r)) ?? PALITRA[0]);
    this.xato.set(null);
    this.dialog.set(true);
  }

  async saqla() {
    const nomi = this.nomi.trim();
    const narx = this.narx() ?? 0;
    const t = this.tahrirY();
    if (!nomi || narx <= 0) return this.xato.set(this.til.t('Xato_Maydon'));
    if (this.x.yoqilgilar().some((y) => y.nomi.toLowerCase() === nomi.toLowerCase() && y.id !== t?.id)) return this.xato.set(this.til.t('Xato_YoqilgiBand'));
    this.band.set(true);
    try {
      // Mavjud yoqilg'ida narx shu yerda o'zgarmaydi (u "Narxni o'zgartirish" dialogi orqali: ochiq smenada ko'rsatkichlar bilan).
      if (t) await this.x.yoqilgiTahrirla(t.id, nomi, t.narx, this.rang());
      else await this.x.yoqilgiYarat(nomi, narx, this.rang());
      this.dialog.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }

  async ochir() {
    const t = this.tahrirY();
    if (!t) return;
    // Aparatga biriktirilgan yoqilg'ini o'chirib bo'lmaydi (server ham rad etadi).
    if (this.aparatSoni(t.id) > 0) return this.xato.set(this.til.t('Xato_YoqilgiIshlatilmoqda'));
    this.band.set(true);
    try {
      await this.x.yoqilgiOchir(t.id);
      this.dialog.set(false);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
