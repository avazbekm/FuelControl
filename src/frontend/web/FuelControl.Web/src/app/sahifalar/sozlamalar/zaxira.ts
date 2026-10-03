import { Component, computed, inject, signal } from '@angular/core';
import { Til } from '../../core/til';
import { Bildirish } from '../../core/bildirish';
import { kun, soat } from '../../core/format';
import { SozlamalarXizmati } from './sozlamalar-xizmati';

const KALIT = 'fc.oxirgiNusxa';
interface Nusxa { vaqt: string; fayl: string; hajmBayt: number }

/** "Zaxira nusxa": server kuniga bir marta nusxa oladi; bu yerda oxirgi nusxa va "Hozir nusxa olish". */
@Component({
  selector: 'sozlamalar-zaxira',
  template: `
    <section class="shisha karta ustunlar zaxira">
      <h2 style="margin:0">{{ til.t('ZaxiraNusxa') }}</h2>
      <div class="plitka">
        <div style="font-weight:650">{{ til.t('AvtomatikNusxa') }}</div>
        <div class="ikkilamchi kichik-matn">{{ til.t('NusxaIzoh') }}</div>
      </div>
      <div class="plitka oxirgi">
        <div class="bosh-joy">
          <div style="font-weight:650">{{ til.t('OxirgiNusxa') }}</div>
          <div class="ikkilamchi kichik-matn matn">{{ oxirgiMatn() }}</div>
        </div>
        <button type="button" class="tugma asosiy" [disabled]="band()" (click)="olish()">
          @if (band()) { <span class="aylanma"></span> } {{ til.t('HozirNusxa') }}
        </button>
      </div>
      <p class="ikkilamchi kichik-matn" style="margin:0">{{ til.t('ZaxiraServerda') }}</p>
    </section>
  `,
  styles: `
    .zaxira { max-width: 700px; }
    .oxirgi { display: flex; flex-wrap: wrap; align-items: center; gap: 12px; }
    .oxirgi .bosh-joy { flex: 1 1 220px; min-width: 0; }
    .matn { overflow-wrap: anywhere; }
    @media (max-width: 480px) { .oxirgi .tugma { width: 100%; } }
  `,
})
export class ZaxiraBolimi {
  protected readonly til = inject(Til);
  private readonly x = inject(SozlamalarXizmati);
  private readonly bildirish = inject(Bildirish);

  protected readonly band = signal(false);
  private readonly nusxa = signal<Nusxa | null>(this.oqi());

  /** Desktop bilan bir xil: "Bugun 14:02 · fuelcontrol-….db · 4200 KB · muvaffaqiyatli" (boshqa kunda sana bilan). */
  protected readonly oxirgiMatn = computed(() => {
    const n = this.nusxa();
    if (!n) return this.til.t('OxirgiNusxaIzoh');
    const bugun = kun(n.vaqt) === kun(new Date());
    return `${bugun ? this.til.t('Bugun') : kun(n.vaqt)} ${soat(n.vaqt)} · ${n.fayl} · ${Math.floor(n.hajmBayt / 1024)} KB · ${this.til.t('Muvaffaqiyatli')}`;
  });

  async olish() {
    if (this.band()) return;
    this.band.set(true);
    try {
      const z = await this.x.zaxiraOl();
      const n: Nusxa = { vaqt: z.vaqt, fayl: z.faylNomi, hajmBayt: z.hajmBayt };
      this.nusxa.set(n);
      try { localStorage.setItem(KALIT, JSON.stringify(n)); } catch { /* */ }
      this.bildirish.korsat(this.til.t('NusxaOlindi'));
    } catch (e) {
      this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    } finally {
      this.band.set(false);
    }
  }

  private oqi(): Nusxa | null {
    try { return JSON.parse(localStorage.getItem(KALIT) ?? 'null') as Nusxa | null; } catch { return null; }
  }
}
