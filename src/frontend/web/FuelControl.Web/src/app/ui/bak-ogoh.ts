import { Component, ElementRef, effect, inject, input, model, output } from '@angular/core';
import { Til } from '../core/til';
import { litr } from '../core/format';
import type { BakOgohi } from '../core/hisob';
import { Ikon } from './ikon';
import { Oyna } from './oyna';

/** Tasdiq oynasidagi bitta aparat qatori: "4-aparat: 990 000 L (bakda 5 017.83 L)". */
export interface BakOgohQatori { raqam: number; chiqadi: number; bakda: number }

/**
 * Bakdagidan ko'p litr chiqsa — pult maydoni ostidagi sariq matn (docs §8.15). Bloklamaydi; hisob `bakOgohi()` da.
 * Ishlatilishi: `<bak-ogoh-matn [ogoh]="q.ogoh" />`.
 */
@Component({
  selector: 'bak-ogoh-matn',
  imports: [Ikon],
  template: `
    @if (ogoh(); as o) {
      <span class="bak-ogoh" role="status" data-test="bak-ogoh"><ikon nomi="warning" [olcham]="14" [qalinlik]="2" /><span>{{ til.t('BakOgoh_Matn', litr(o.bakda), litr(o.chiqadi)) }}</span></span>
    }
  `,
  styles: `
    :host { display: contents; }
    .bak-ogoh { display: flex; align-items: flex-start; gap: 6px; margin-top: 6px; font-size: 12.5px; font-weight: 600; line-height: 1.4; color: var(--sariq); font-variant-numeric: tabular-nums; }
    .bak-ogoh ikon { flex: none; margin-top: 1px; }
  `,
})
export class BakOgohMatn {
  protected readonly til = inject(Til);
  protected readonly litr = litr;
  readonly ogoh = input<BakOgohi | null>(null);
}

/**
 * Saqlash tugmasi bosilganda, kamida bitta aparatda bakdagidan ko'p litr chiqayotgan bo'lsa chiqadigan tasdiq (docs §8.15).
 * "Tekshiraman" (fokus shunda) oynani yopadi; "Baribir saqlash" — oynani yopib `davom` ni chiqaradi, saqlashni egasi bajaradi.
 */
@Component({
  selector: 'bak-ogoh-dialog',
  imports: [Oyna],
  template: `
    <oyna [(ochiq)]="ochiq" [sarlavha]="til.t('BakOgoh_Sarlavha')" ikon="warning" ikonRang="sariq" [kenglik]="500">
      <div class="forma-ustun" data-test="bak-ogoh-dialog">
        <p class="savol">{{ til.t('BakOgoh_Savol') }}</p>
        <ul class="qatorlar">
          @for (q of qatorlar(); track q.raqam) {
            <li>{{ til.t('BakOgoh_Qator', til.t('Aparat_Raqami', q.raqam), litr(q.chiqadi), litr(q.bakda)) }}</li>
          }
        </ul>
        <p class="eslatma">{{ til.t('BakOgoh_Eslatma') }}</p>
        <div class="amallar">
          <button type="button" class="tugma" (click)="davomEt()" data-test="bak-baribir">{{ til.t('BakOgoh_BaribirSaqlash') }}</button>
          <button type="button" class="tugma asosiy" data-avto (click)="ochiq.set(false)" data-test="bak-tekshiraman">{{ til.t('BakOgoh_Tekshiraman') }}</button>
        </div>
      </div>
    </oyna>
  `,
  styles: `
    .savol { margin: 0; font-size: 14px; line-height: 1.45; }
    .qatorlar { margin: 0; padding: 12px 16px 12px 32px; display: flex; flex-direction: column; gap: 6px; border-radius: 16px; background: var(--ogohlik-fon); border: 1px solid var(--ogohlik-chegara); font-size: 14px; font-weight: 700; font-variant-numeric: tabular-nums; }
    .eslatma { margin: 0; font-size: 12.5px; line-height: 1.45; color: var(--matn-3); }
  `,
})
export class BakOgohDialog {
  protected readonly til = inject(Til);
  protected readonly litr = litr;

  readonly ochiq = model(false);
  readonly qatorlar = input.required<BakOgohQatori[]>();
  readonly davom = output<void>();
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);

  constructor() {
    // "Tekshiraman" fokus oladi — sensorli qurilmada ham (Oyna u yerda avto-fokus bermaydi, klaviatura chiqmasligi uchun).
    effect(() => {
      if (this.ochiq()) setTimeout(() => this.el.nativeElement.querySelector<HTMLElement>('[data-avto]')?.focus(), 80);
    });
  }

  protected davomEt() {
    this.ochiq.set(false);
    this.davom.emit();
  }
}
