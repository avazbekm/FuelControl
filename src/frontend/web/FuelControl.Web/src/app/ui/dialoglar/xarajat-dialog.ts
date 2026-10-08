import { Component, effect, inject, model, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import type { XarajatManbai } from '../../api/model';
import { Ikon } from '../ikon';
import { Oyna } from '../oyna';
import { EnterKeyingi } from '../enter-keyingi';
import { SonKiritish } from '../son-kiritish';

const TEZLAR = ['Xarajat_Tez1', 'Xarajat_Tez2', 'Xarajat_Tez3'];

/** "Xarajat yozish" dialogi (docs/dizayn/XarajatQoshish): ochiq smenaga xarajat; manba — kassa yoki depozit karta. */
@Component({
  selector: 'xarajat-dialog',
  imports: [FormsModule, Ikon, Oyna, SonKiritish, EnterKeyingi],
  template: `
    <oyna [(ochiq)]="ochiq" [sarlavha]="til.t('Xarajat_Yozish')" [tagsarlavha]="smenaMatn()" ikon="receipt" ikonRang="sariq">
      <form class="forma-ustun" enterKeyingi (ngSubmit)="saqla()" autocomplete="off" novalidate>
        <div class="maydon">
          <label for="xd-summa">{{ til.t('Xarajat_Summa') }}</label>
          <son-kiritish [(qiymat)]="summa" sinf="kiritish katta" inputId="xd-summa" [avto]="true" />
        </div>
        <div class="maydon">
          <label for="xd-nima">{{ til.t('Xarajat_NimaUchun') }}</label>
          <input id="xd-nima" class="kiritish" name="nima" [(ngModel)]="nima" [placeholder]="til.t('Xarajat_NimaPlaceholder')" autocomplete="off" />
          <div class="chiplar">
            @for (t of tezlar; track t) {
              <button type="button" class="chip kichik tez" (click)="nima = til.t(t)">{{ til.t(t) }}</button>
            }
          </div>
        </div>
        <div class="maydon">
          <span class="yorliq" id="xd-joy-l">{{ til.t('Xarajat_PulQayerdan') }}</span>
          <div class="segment" role="group" aria-labelledby="xd-joy-l">
            <button type="button" [class.tanlangan]="manba() === 'Kassa'" [attr.aria-pressed]="manba() === 'Kassa'" (click)="manba.set('Kassa')">{{ til.t('Xarajat_Kassadan') }}</button>
            <button type="button" [class.tanlangan]="manba() === 'Depozit'" [attr.aria-pressed]="manba() === 'Depozit'" (click)="manba.set('Depozit')">{{ til.t('Xarajat_DepozitKartadan') }}</button>
          </div>
        </div>
        <div class="malumot-blok"><ikon nomi="info" [olcham]="16" [qalinlik]="2" /><span>{{ til.t(manba() === 'Kassa' ? 'Xarajat_KassaIzoh' : 'Xarajat_DepozitIzoh') }}</span></div>
        @if (xato()) { <div class="xato-matn" role="alert">{{ xato() }}</div> }
        <div class="amallar">
          <button type="button" class="tugma" (click)="ochiq.set(false)">{{ til.t('BekorQilish') }}</button>
          <button type="submit" class="tugma asosiy" [disabled]="band()">
            @if (band()) { <span class="aylanma"></span> } @else { <ikon nomi="check" [olcham]="16" [qalinlik]="2.4" /> } {{ til.t('Saqlash') }}
          </button>
        </div>
      </form>
    </oyna>
  `,
  styles: `.chip.tez { font-size: 13.5px; font-weight: 600; }`,
})
export class XarajatDialog {
  protected readonly til = inject(Til);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);

  readonly ochiq = model(false);
  readonly saqlandi = output<void>();

  protected readonly tezlar = TEZLAR;
  protected readonly smenaMatn = signal('');
  protected readonly summa = signal<number | null>(null);
  protected nima = '';
  protected readonly manba = signal<XarajatManbai>('Kassa');
  protected readonly band = signal(false);
  protected readonly xato = signal<string | null>(null);

  constructor() {
    effect(() => {
      if (!this.ochiq()) return;
      untracked(() => {
        this.summa.set(null); this.nima = ''; this.manba.set('Kassa'); this.xato.set(null); this.smenaMatn.set('');
        this.server.joriySmena().then((t) => this.smenaMatn.set(t ? this.til.t('Xarajat_Izoh', t.smena.id) : '')).catch(() => undefined);
      });
    });
  }

  async saqla() {
    const s = this.summa();
    const nima = this.nima.trim();
    if (!s || s <= 0) return this.xato.set(this.til.t('Nasiya_XatoSumma'));
    if (!nima) return this.xato.set(this.til.t('Xarajat_XatoSabab'));
    this.xato.set(null);
    this.band.set(true);
    try {
      await this.server.xarajatYarat({ summa: s, sabab: nima, manba: this.manba() });
      this.ochiq.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
      this.saqlandi.emit();
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
