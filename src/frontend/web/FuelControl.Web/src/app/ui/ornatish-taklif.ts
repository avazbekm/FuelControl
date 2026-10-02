import { Component, computed, inject, input } from '@angular/core';
import { Til } from '../core/til';
import { Ornatish } from '../core/ornatish';
import { Bildirish } from '../core/bildirish';
import { Ikon } from './ikon';

/** "Bosh ekranga qo'shish" kartasi: Chromium'da tugma, iOS'da 2 qadamli yo'riqnoma. */
@Component({
  selector: 'ornatish-taklif',
  imports: [Ikon],
  template: `
    @if (korinadi()) {
      <section class="shisha taklif" aria-live="polite">
        <div class="qator">
          <span class="ikon-doira"><ikon nomi="install" [olcham]="18" /></span>
          <div class="bosh-joy">
            <div class="sarlavha">{{ til.t('IlovaniOrnatish') }}</div>
            <div class="ikkilamchi kichik-matn">{{ til.t('OrnatishIzoh') }}</div>
          </div>
        </div>
        @if (o.tayyor()) {
          <div class="qator">
            @if (yopsaBoladi()) { <button type="button" class="tugma kichik shaffof" (click)="o.keyinroq()">{{ til.t('Keyinroq') }}</button> }
            <button type="button" class="tugma asosiy bosh-joy" (click)="ornat()"><ikon nomi="download" [olcham]="18" /> {{ til.t('IlovaniOrnatish') }}</button>
          </div>
        } @else {
          <ol class="qadamlar">
            <li><span class="raqam">1</span> {{ til.t('IosOrnatish1') }} <ikon nomi="share" [olcham]="18" /></li>
            <li><span class="raqam">2</span> {{ til.t('IosOrnatish2') }} <ikon nomi="plus" [olcham]="18" /></li>
          </ol>
          @if (yopsaBoladi()) { <button type="button" class="tugma kichik shaffof keyinroq" (click)="o.keyinroq()">{{ til.t('Keyinroq') }}</button> }
        }
      </section>
    }
  `,
  styles: `
    .taklif { padding: 16px; display: flex; flex-direction: column; gap: 12px; width: 100%; }
    .sarlavha { font-weight: 650; }
    .qadamlar { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; font-size: 14px; }
    .qadamlar li { display: flex; align-items: center; gap: 8px; }
    .qadamlar ikon { color: var(--asosiy); }
    .raqam { width: 22px; height: 22px; border-radius: 999px; flex: none; display: inline-flex; align-items: center; justify-content: center;
      font-size: 12px; font-weight: 700; background: var(--asosiy-och); color: var(--asosiy); }
    .keyinroq { align-self: flex-end; }
  `,
})
export class OrnatishTaklif {
  protected readonly til = inject(Til);
  protected readonly o = inject(Ornatish);
  private readonly bildirish = inject(Bildirish);
  /** Login sahifasida "Keyinroq" bilan yashirsa bo'ladi; sozlamalarda doim ko'rinadi. */
  readonly yopsaBoladi = input(false);
  protected readonly korinadi = computed(() => this.o.mumkin() && !(this.yopsaBoladi() && this.o.kechiktirilgan()));

  async ornat() {
    if (await this.o.ornat()) this.bildirish.korsat(this.til.t('IlovaOrnatilgan'));
  }
}
