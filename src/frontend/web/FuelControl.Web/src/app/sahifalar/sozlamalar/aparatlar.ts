import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { litr } from '../../core/format';
import type { Aparat } from '../../api/turlar';
import { Ikon } from '../../ui/ikon';
import { Oyna } from '../../ui/oyna';
import { SozlamalarXizmati } from './sozlamalar-xizmati';

/** "Aparatlar": kartalar (raqam, yoqilg'i badge, pult holati, totalizator L) va aparat dialogi. */
@Component({
  selector: 'sozlamalar-aparatlar',
  imports: [FormsModule, Ikon, Oyna],
  template: `
    <section class="shisha karta">
      <div class="qator ora orala sarlavha-qator">
        <h2 style="margin:0">{{ til.t('Aparatlar') }}</h2>
        <button type="button" class="tugma asosiy" (click)="qosh()"><ikon nomi="plus" [olcham]="16" /> {{ til.t('AparatQoshish') }}</button>
      </div>
      <p class="ikkilamchi kichik-matn izoh">{{ til.t('AparatlarIzoh') }}</p>
      <div class="kartalar">
        @for (a of x.aparatlar(); track a.id) {
          <div class="plitka aparat">
            <div class="raqam">{{ til.t('Aparat') }} {{ a.raqam }}</div>
            <span class="badge-yoq" [style.background]="rang(a.yoqilgiTuriId)">{{ a.yoqilgiNomi }}</span>
            <div class="qator pult"><span class="nuqta kichik" style="background:#8A97B1"></span><span class="ikkilamchi kichik-matn">{{ til.t('PultUlanmagan') }}</span></div>
            <div class="chiziq"></div>
            <div class="qator ora"><span class="ikkilamchi l">L</span><span class="total son">{{ litr(a.totalLitr) }}</span></div>
            <button type="button" class="tugma keng" (click)="tahrirla(a)">{{ til.t('Tahrirlash') }}</button>
          </div>
        } @empty {
          <div class="bosh">{{ til.t('MalumotYoq') }}</div>
        }
      </div>
    </section>

    <oyna [(ochiq)]="dialog" [sarlavha]="tahrirA() ? til.t('AparatniTahrirlash') : til.t('YangiAparat')" [kenglik]="440">
      <form class="forma-ustun" (ngSubmit)="saqla()">
        <div class="maydon">
          <label class="katta-yorliq" for="a-raqam">{{ til.t('AparatRaqami') }}</label>
          <input id="a-raqam" class="kiritish katta" name="raqam" inputmode="numeric" style="text-align:center" [(ngModel)]="raqam" autocomplete="off" data-avto />
        </div>
        <div class="maydon">
          <span class="katta-yorliq">{{ til.t('YoqilgiTuri') }}</span>
          <div class="chiplar yoq-tanlov" role="radiogroup" [attr.aria-label]="til.t('YoqilgiTuri')">
            @for (y of x.yoqilgilar(); track y.id) {
              <button type="button" class="chip yoq-chip" role="radio" [attr.aria-checked]="yoqilgiId() === y.id" [class.tanlangan]="yoqilgiId() === y.id" (click)="yoqilgiId.set(y.id)">
                <span class="nuqta" [style.background]="y.rang"></span> {{ y.nomi }}
              </button>
            }
          </div>
        </div>
        <div class="plitka totalizator">
          <div style="font-weight:700">{{ til.t('Totalizator') }}</div>
          <p class="ikkilamchi kichik-matn" style="margin:6px 0 10px">{{ til.t('TotalizatorIzoh') }}</p>
          <div class="maydon">
            <label class="katta-yorliq" for="a-total">{{ til.t('TotalL') }}</label>
            <input id="a-total" class="kiritish katta son" name="total" inputmode="decimal" style="text-align:right;font-size:22px" [(ngModel)]="totalLitr" placeholder="232815.41" autocomplete="off" />
          </div>
        </div>
        @if (xato()) { <div class="xato-matn" role="alert">{{ xato() }}</div> }
        <div class="amallar">
          <button type="button" class="tugma" (click)="dialog.set(false)">{{ til.t('BekorQilish') }}</button>
          <button type="submit" class="tugma asosiy" [disabled]="band()">
            @if (band()) { <span class="aylanma"></span> } {{ til.t('Saqlash') }}
          </button>
        </div>
      </form>
    </oyna>
  `,
  styles: `
    .sarlavha-qator { margin-bottom: 4px; }
    .izoh { margin: 8px 0 14px; }
    .kartalar { display: grid; grid-template-columns: repeat(auto-fill, minmax(170px, 1fr)); gap: 12px; }
    .aparat { display: flex; flex-direction: column; gap: 8px; }
    .raqam { font-weight: 700; font-size: 16px; }
    .badge-yoq { align-self: flex-start; padding: 3px 11px; border-radius: 999px; color: #fff; font-size: 12px; font-weight: 650; }
    .nuqta.kichik { width: 8px; height: 8px; }
    .pult { gap: 6px; }
    .chiziq { height: 1px; background: var(--chiziq); }
    .l { font-weight: 700; font-size: 13px; }
    .total { font-weight: 800; font-size: 13px; }
    .yoq-tanlov { gap: 8px; }
    .yoq-chip { display: inline-flex; align-items: center; gap: 8px; min-height: 44px; }
    .yoq-chip .nuqta { box-shadow: 0 0 0 2px rgba(255, 255, 255, 0.9); }
    .totalizator { box-shadow: none; }
  `,
})
export class AparatlarBolimi {
  protected readonly til = inject(Til);
  protected readonly x = inject(SozlamalarXizmati);
  private readonly bildirish = inject(Bildirish);

  protected readonly litr = litr;
  protected readonly dialog = signal(false);
  protected readonly tahrirA = signal<Aparat | null>(null);
  protected readonly xato = signal<string | null>(null);
  protected readonly band = signal(false);
  protected readonly yoqilgiId = signal<number | null>(null);
  protected raqam = '';
  protected totalLitr = '';

  protected rang(yoqilgiTuriId: number) {
    return this.x.yoqilgilar().find((y) => y.id === yoqilgiTuriId)?.rang ?? '#5A6B88';
  }

  protected qosh() {
    const a = this.x.aparatlar();
    this.tahrirA.set(null);
    // Keyingi raqam avtomatik (eng kattasi + 1).
    this.raqam = String(a.length ? Math.max(...a.map((q) => q.raqam)) + 1 : 1);
    this.yoqilgiId.set(this.x.yoqilgilar()[0]?.id ?? null);
    this.totalLitr = '';
    this.xato.set(null);
    this.dialog.set(true);
  }

  protected tahrirla(a: Aparat) {
    this.tahrirA.set(a);
    this.raqam = String(a.raqam);
    this.yoqilgiId.set(a.yoqilgiTuriId);
    this.totalLitr = a.totalLitr.toFixed(2);
    this.xato.set(null);
    this.dialog.set(true);
  }

  async saqla() {
    const raqam = Number(this.raqam.trim());
    const yid = this.yoqilgiId();
    const t = this.tahrirA();
    if (!Number.isInteger(raqam) || raqam <= 0 || yid === null) return this.xato.set(this.til.t('Xato_Maydon'));
    if (this.x.aparatlar().some((a) => a.raqam === raqam && a.id !== t?.id)) return this.xato.set(this.til.t('Xato_AparatBand'));
    const matn = this.totalLitr.trim().replace(/\s/g, '').replace(',', '.');
    const total = matn === '' ? 0 : Number(matn);
    if (!isFinite(total) || total < 0) return this.xato.set(this.til.t('Xato_Maydon'));
    this.band.set(true);
    try {
      // Tahrirda totalizator o'zgargan bo'lsa server tuzatadi va auditga "Totalizator tuzatildi" deb yozadi.
      if (t) await this.x.aparatTahrirla(t.id, raqam, yid, total);
      else await this.x.aparatYarat(raqam, yid, total);
      this.dialog.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
