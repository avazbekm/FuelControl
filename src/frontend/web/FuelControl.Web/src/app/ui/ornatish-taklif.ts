import { Component, computed, inject, input } from '@angular/core';
import { Til } from '../core/til';
import { Ornatish } from '../core/ornatish';
import { Bildirish } from '../core/bildirish';
import { Ikon } from './ikon';

/**
 * "Bosh ekranga qo'shish" kartasi. Chromium'da tugma, iOS'da 2 qadamli yo'riqnoma.
 * Keng joyda (≥560px) ixcham bitta qator: ikonka, matn, o'ngda kichik "O'rnatish"; tor joyda (telefon) — to'liq kenglikdagi tugma.
 * O'rnatish imkoni yo'q bo'lsa (o'rnatilgan, yoki brauzer taklif qilmaydi va iOS emas) — karta umuman ko'rinmaydi.
 */
@Component({
  selector: 'ornatish-taklif',
  imports: [Ikon],
  host: { '[class.yashirin]': '!korinadi()' },
  template: `
    @if (korinadi()) {
      <section class="shisha taklif" aria-live="polite">
        <div class="bosh-qator">
          <span class="ikon-doira"><ikon nomi="install" [olcham]="18" /></span>
          <div class="matn">
            <div class="sarlavha">{{ til.t('IlovaniOrnatish') }}</div>
            <div class="ikkilamchi kichik-matn">{{ til.t('OrnatishIzoh') }}</div>
          </div>
          @if (o.tayyor()) {
            <div class="amal">
              @if (yopsaBoladi()) { <button type="button" class="tugma kichik shaffof" (click)="o.keyinroq()">{{ til.t('Keyinroq') }}</button> }
              <button type="button" class="tugma asosiy ornat" [attr.aria-label]="til.t('IlovaniOrnatish')" (click)="ornat()">
                <ikon nomi="download" [olcham]="18" />
                <span class="uzun">{{ til.t('IlovaniOrnatish') }}</span><span class="qisqa">{{ til.t('Ornatish') }}</span>
              </button>
            </div>
          }
        </div>
        @if (!o.tayyor()) {
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
    :host { display: block; width: 100%; container-type: inline-size; }
    :host(.yashirin) { display: none; }
    .taklif { padding: 16px; display: flex; flex-direction: column; gap: 12px; }
    .bosh-qator { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
    .matn { flex: 1 1 200px; min-width: 0; }
    .sarlavha { font-weight: 650; }
    .amal { display: flex; align-items: center; gap: 8px; flex: 1 1 100%; }
    .amal .ornat { flex: 1 1 auto; }
    .qisqa { display: none; }
    .qadamlar { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 8px; font-size: 14px; }
    .qadamlar li { display: flex; align-items: center; gap: 8px; }
    .qadamlar ikon { color: var(--asosiy); }
    .raqam { width: 22px; height: 22px; border-radius: 999px; flex: none; display: inline-flex; align-items: center; justify-content: center;
      font-size: 12px; font-weight: 700; background: var(--asosiy-och); color: var(--asosiy); }
    .keyinroq { align-self: flex-end; }

    /* Keng joy: ixcham bitta qator */
    @container (min-width: 560px) {
      .taklif { padding: 12px 16px; }
      .bosh-qator { flex-wrap: nowrap; }
      .amal { flex: 0 0 auto; }
      .amal .ornat { flex: 0 0 auto; }
      .uzun { display: none; }
      .qisqa { display: inline; }
    }
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
