import { Component, input } from '@angular/core';

/**
 * KPI kartasi (docs/dizayn/Nasiyalar, Boshqaruv): katta bosh harfli nom, yirik qiymat + "so'm", izoh.
 * `ton` — qiymat rangi (qizil | yashil | bo'sh), `qizilKarta` — butun karta qizg'ish (muddati o'tgan qarz).
 */
@Component({
  selector: 'kpi-karta',
  template: `
    <div class="kpi-karta shisha" [class.qizil-fon]="qizilKarta()">
      <span class="yorliq">{{ nom() }}</span>
      <span class="qiymat" [class.qizil]="ton() === 'qizil'" [class.yashil]="ton() === 'yashil'">{{ qiymat() }}@if (birlik()) {&ngsp;<span class="birlik">{{ birlik() }}</span>}</span>
      @if (izoh()) { <span class="izoh">{{ izoh() }}</span> }
      @if (qizilIzoh()) { <span class="izoh qizil-izoh">{{ qizilIzoh() }}</span> }
    </div>
  `,
  styles: `
    :host { display: block; min-width: 0; }
    .kpi-karta { height: 100%; padding: 18px 20px; display: flex; flex-direction: column; gap: 6px; min-width: 0; color: var(--matn); }
    .qiymat { font-size: 26px; font-weight: 800; letter-spacing: -0.5px; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .qiymat.qizil { color: #C42127; }
    .qiymat.yashil { color: #0E7A4A; }
    .birlik { font-size: 12.5px; font-weight: 600; letter-spacing: 0; color: var(--matn-2); white-space: nowrap; }
    .izoh { font-size: 12.5px; color: var(--matn-2); }
    .izoh.qizil-izoh { font-weight: 700; color: #C42127; }
    .qizil-fon {
      background: linear-gradient(160deg, rgba(255, 238, 238, 0.92), rgba(255, 225, 225, 0.72));
      border-color: #FFB4B4; box-shadow: 0 18px 48px rgba(120, 30, 30, 0.10), inset 0 1px 0 rgba(255, 255, 255, 0.8);
    }
    :host-context(:root[data-theme="dark"]) .qizil-fon { background: linear-gradient(160deg, rgba(120, 40, 44, 0.5), rgba(80, 30, 34, 0.4)); border-color: #7A3034; }
    :host-context(:root[data-theme="dark"]) .qiymat.qizil { color: var(--qizil); }
    :host-context(:root[data-theme="dark"]) .qiymat.yashil { color: var(--yashil); }
    :host-context(:root[data-theme="dark"]) .izoh.qizil-izoh { color: var(--qizil); }
    @media (prefers-color-scheme: dark) {
      :host-context(:root:not([data-theme="light"])) .qizil-fon { background: linear-gradient(160deg, rgba(120, 40, 44, 0.5), rgba(80, 30, 34, 0.4)); border-color: #7A3034; }
      :host-context(:root:not([data-theme="light"])) .qiymat.qizil { color: var(--qizil); }
      :host-context(:root:not([data-theme="light"])) .qiymat.yashil { color: var(--yashil); }
      :host-context(:root:not([data-theme="light"])) .izoh.qizil-izoh { color: var(--qizil); }
    }
    @media (max-width: 520px) { .kpi-karta { padding: 14px; } .qiymat { font-size: 18px; white-space: normal; } }
  `,
})
export class KpiKarta {
  readonly nom = input.required<string>();
  readonly qiymat = input.required<string>();
  readonly birlik = input('');
  readonly izoh = input('');
  /** Qiymat ostidagi qizil qo'shimcha qator (masalan, jami qarzdorlik ostida "muddati o'tgan ..."). */
  readonly qizilIzoh = input('');
  readonly ton = input<'' | 'qizil' | 'yashil'>('');
  readonly qizilKarta = input(false);
}
