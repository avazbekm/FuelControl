import { Component, ElementRef, booleanAttribute, computed, effect, inject, input, model, output } from '@angular/core';
import { Til } from '../core/til';
import { litr } from '../core/format';
import { krugKorinish, type PultNatijasi } from '../core/hisob';
import { Ikon } from './ikon';
import { Oyna } from './oyna';

/**
 * Pult qiymatini ko'rsatish (docs §8.16): API'dagi UMUMIY litr → krugli aparatda pult va ostida kichik "krug k" (10 560.52 → "560.52", "krug 2").
 * Krug yo'q bo'lsa — oddiy son. `satrda` — krug belgisi pult yonida ("560.52 L · krug 2"), aks holda ostida. Tekislash: `--krug-tekis` (flex-start | flex-end).
 */
@Component({
  selector: 'pult-qiymat',
  template: `
    <span class="son" data-test="pult-son">{{ litr(k().pult) }}@if (birlik()) { <small class="birlik"> L</small> }@if (satrda() && k().krug != null) { <small class="krug" data-test="krug"> · {{ belgi() }}</small> }</span>
    @if (!satrda() && k().krug != null) { <small class="krug" data-test="krug">{{ belgi() }}</small> }
  `,
  styles: `
    :host { display: inline-flex; flex-direction: column; align-items: var(--krug-tekis, flex-start); gap: 1px; }
    .krug { font-size: 11.5px; font-weight: 700; letter-spacing: 0; color: var(--matn-2); white-space: nowrap; }
    .birlik { font-size: 13px; font-weight: 600; color: var(--matn-2); }
  `,
})
export class PultQiymat {
  protected readonly til = inject(Til);
  protected readonly litr = litr;
  /** Umumiy litr (API qiymati). */
  readonly jami = input.required<number>();
  /** `AparatDto.krugHajmi` (null — krug yo'q). */
  readonly krugHajmi = input<number | null | undefined>(null);
  readonly birlik = input(false, { transform: booleanAttribute });
  readonly satrda = input(false, { transform: booleanAttribute });
  protected readonly k = computed(() => krugKorinish(this.jami(), this.krugHajmi()));
  protected readonly belgi = computed(() => this.til.t('Krug_Belgi', this.k().krug));
}

/**
 * Pult maydoni ostidagi izoh (docs §8.16): `0 ≤ p < H` buzilsa qizil xato, krug oshsa ajralib turadigan "krug k+1" yozuvi.
 * Ishlatilishi: `<krug-izoh [natija]="n" [krugHajmi]="aparat.krugHajmi" />`.
 */
@Component({
  selector: 'krug-izoh',
  imports: [Ikon],
  template: `
    @if (natija(); as n) {
      @if (n.xato) {
        <span class="krug-xato" role="alert" data-test="krug-xato"><ikon nomi="warning" [olcham]="14" [qalinlik]="2" /><span>{{ xatoMatni() }}</span></span>
      } @else if (n.oshdi) {
        <span class="krug-oshdi" role="status" data-test="krug-oshdi"><ikon nomi="refresh" [olcham]="14" [qalinlik]="2.2" /><span>{{ belgi() }}</span></span>
      }
    }
  `,
  styles: `
    :host { display: contents; }
    .krug-xato, .krug-oshdi { display: flex; align-items: flex-start; gap: 6px; margin-top: 6px; font-size: 12.5px; font-weight: 700; line-height: 1.4; }
    .krug-xato { color: var(--b-qizil); }
    .krug-oshdi { color: var(--asosiy-toq); }
    ikon { flex: none; margin-top: 1px; }
  `,
})
export class KrugIzoh {
  protected readonly til = inject(Til);
  readonly natija = input<PultNatijasi | null>(null);
  readonly krugHajmi = input<number | null | undefined>(null);
  protected readonly xatoMatni = computed(() => this.til.t('Krug_XatoKatta', litr(this.krugHajmi() ?? 0)));
  protected readonly belgi = computed(() => this.til.t('Krug_Belgi', this.natija()?.krug));
}

/** Tasdiq oynasidagi bitta aparat qatori: "4-aparat: pult 0 dan qayta boshlangan, krug 2 → 3, sotilgan 550.00 L". */
export interface KrugQatori { raqam: number; krugdan: number; krugga: number; sotilgan: number }

/**
 * Krug oshgan aparat bo'lsa saqlashdan oldin chiqadigan tasdiq (docs §8.16): "Tekshiraman" (fokus shunda, oynani yopadi) va "Ha, to'g'ri" (`davom`).
 */
@Component({
  selector: 'krug-tasdiq-dialog',
  imports: [Oyna],
  template: `
    <oyna [(ochiq)]="ochiq" [sarlavha]="til.t('Krug_TasdiqSarlavha')" ikon="refresh" ikonRang="moviy" [kenglik]="500">
      <div class="forma-ustun" data-test="krug-tasdiq">
        <ul class="qatorlar">
          @for (q of qatorlar(); track q.raqam) {
            <li>{{ til.t('Krug_TasdiqQator', til.t('Aparat_Raqami', q.raqam), q.krugdan, q.krugga, litr(q.sotilgan)) }}</li>
          }
        </ul>
        <div class="amallar">
          <button type="button" class="tugma" (click)="davomEt()" data-test="krug-togri">{{ til.t('Krug_HaTogri') }}</button>
          <button type="button" class="tugma asosiy" data-avto (click)="ochiq.set(false)" data-test="krug-tekshiraman">{{ til.t('BakOgoh_Tekshiraman') }}</button>
        </div>
      </div>
    </oyna>
  `,
  styles: `
    .qatorlar { margin: 0; padding: 12px 16px 12px 32px; display: flex; flex-direction: column; gap: 6px; border-radius: 16px; background: var(--malumot-fon); border: 1px solid var(--plitka-chegara); font-size: 14px; font-weight: 700; font-variant-numeric: tabular-nums; }
  `,
})
export class KrugTasdiqDialog {
  protected readonly til = inject(Til);
  protected readonly litr = litr;

  readonly ochiq = model(false);
  readonly qatorlar = input.required<KrugQatori[]>();
  readonly davom = output<void>();
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);

  constructor() {
    // "Tekshiraman" fokus oladi — sensorli qurilmada ham (Oyna u yerda avto-fokus bermaydi).
    effect(() => {
      if (this.ochiq()) setTimeout(() => this.el.nativeElement.querySelector<HTMLElement>('[data-avto]')?.focus(), 80);
    });
  }

  protected davomEt() {
    this.ochiq.set(false);
    this.davom.emit();
  }
}
