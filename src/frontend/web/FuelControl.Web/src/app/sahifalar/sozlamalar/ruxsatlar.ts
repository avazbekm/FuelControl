import { Component, computed, inject, signal } from '@angular/core';
import { Auth } from '../../core/auth';
import { Til } from '../../core/til';
import { Bildirish } from '../../core/bildirish';
import type { Foydalanuvchi, Ruxsat } from '../../api/turlar';
import { RUXSAT_ROYXATI, SozlamalarXizmati, standartRuxsatlar } from './sozlamalar-xizmati';

/**
 * "Ruxsatlar": chapda foydalanuvchi kartalari, o'ngda tanlangani uchun bo'limlar va amallar plitkalari.
 * Belgi o'zgarishi darhol serverga yoziladi (butun to'plam); rad etilsa belgi qaytariladi.
 */
@Component({
  selector: 'sozlamalar-ruxsatlar',
  template: `
    <div class="panjara ruxsat-panjara">
      <section class="shisha karta royxat-karta">
        <h2>{{ til.t('Foydalanuvchilar') }}</h2>
        <div class="kartalar">
          @for (f of x.foydalanuvchilar(); track f.id) {
            <button type="button" class="plitka f-karta" [class.tanlangan]="joriy()?.id === f.id" [attr.aria-pressed]="joriy()?.id === f.id" (click)="tanlanganId.set(f.id)">
              <span class="avatar">{{ bosh(f) }}</span>
              <span class="matnlar">
                <span class="ism">{{ f.toliqIsm }}</span>
                <span class="ikkilamchi kichik-matn">{{ til.t('Rol_' + f.rol) }}</span>
              </span>
              <span class="badge kok">{{ f.ruxsatlar.length }}</span>
            </button>
          } @empty { <div class="bosh">{{ til.t('MalumotYoq') }}</div> }
        </div>
      </section>

      <section class="shisha karta ustunlar">
        @if (joriy(); as f) {
          <div class="qator ora orala">
            <div>
              <h2 style="font-size:18px;margin:0">{{ f.toliqIsm }}</h2>
              <div class="ikkilamchi kichik-matn">{{ til.t('Rol_' + f.rol) }} · {{ f.ruxsatlar.length }} {{ til.t('TaRuxsat') }}</div>
            </div>
            <button type="button" class="tugma" (click)="standart(f)">{{ til.t('RolBoyichaStandart') }}</button>
          </div>
          <p class="ikkilamchi kichik-matn" style="margin:0">{{ til.t('RuxsatIzoh') }}</p>

          <div class="katta-yorliq guruh">{{ til.t('Bolimlar') }}</div>
          <div class="plitkalar">
            @for (r of bolimlar; track r) {
              <label class="plitka p-plitka" [class.yoqilgan]="bor(f, r)">
                <input type="checkbox" [checked]="bor(f, r)" (change)="almashtir(f, r)" />
                <span class="matnlar"><span class="nom">{{ til.t('R_' + r) }}</span><span class="ikkilamchi kichik-matn">{{ til.t('RI_' + r) }}</span></span>
              </label>
            }
          </div>

          <div class="katta-yorliq guruh">{{ til.t('Amallar') }}</div>
          <div class="plitkalar">
            @for (r of amallar; track r) {
              <label class="plitka p-plitka" [class.yoqilgan]="bor(f, r)">
                <input type="checkbox" [checked]="bor(f, r)" (change)="almashtir(f, r)" />
                <span class="matnlar"><span class="nom">{{ til.t('R_' + r) }}</span><span class="ikkilamchi kichik-matn">{{ til.t('RI_' + r) }}</span></span>
              </label>
            }
          </div>
        } @else {
          <div class="bosh">{{ til.t('MalumotYoq') }}</div>
        }
      </section>
    </div>
  `,
  styles: `
    .ruxsat-panjara { align-items: start; }
    @media (min-width: 900px) { .ruxsat-panjara { grid-template-columns: 320px minmax(0, 1fr); } }
    .royxat-karta { padding: 14px; }
    .royxat-karta h2 { margin: 2px 4px 12px; }
    /* Telefonda foydalanuvchilar — yon tomonga suriladigan qator; keng ekranda — ustun. */
    .kartalar { display: flex; gap: 8px; overflow-x: auto; scroll-snap-type: x proximity; padding-bottom: 4px; }
    @media (min-width: 900px) { .kartalar { flex-direction: column; overflow: visible; } }
    .f-karta { display: flex; align-items: center; gap: 10px; padding: 10px; text-align: left; cursor: pointer; min-width: 220px; min-height: 56px; scroll-snap-align: start; border-width: 2px; }
    @media (min-width: 900px) { .f-karta { min-width: 0; } }
    .f-karta.tanlangan { border-color: var(--asosiy); box-shadow: 0 0 0 3px rgba(47, 107, 255, 0.15); }
    .f-karta .matnlar { flex: 1; min-width: 0; display: flex; flex-direction: column; }
    .f-karta .ism { font-weight: 700; font-size: 13px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .avatar { width: 36px; height: 36px; border-radius: 999px; flex: none; display: inline-flex; align-items: center; justify-content: center; font-size: 12px; font-weight: 700; background: var(--asosiy-och); color: var(--asosiy); }
    .guruh { margin-top: 6px; }
    .plitkalar { display: grid; grid-template-columns: minmax(0, 1fr); gap: 10px; }
    @media (min-width: 560px) { .plitkalar { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
    .p-plitka { display: flex; align-items: flex-start; gap: 12px; cursor: pointer; min-height: 56px; padding: 10px 12px; }
    .p-plitka input { width: 22px; height: 22px; margin-top: 1px; flex: none; accent-color: var(--asosiy); }
    .p-plitka .matnlar { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .p-plitka .nom { font-weight: 700; font-size: 13px; }
    .p-plitka.yoqilgan { border-color: rgba(47, 107, 255, 0.45); }
  `,
})
export class RuxsatlarBolimi {
  protected readonly til = inject(Til);
  protected readonly x = inject(SozlamalarXizmati);
  private readonly auth = inject(Auth);
  private readonly bildirish = inject(Bildirish);

  protected readonly bolimlar = RUXSAT_ROYXATI.filter((r) => r.guruh === 'B').map((r) => r.ruxsat);
  protected readonly amallar = RUXSAT_ROYXATI.filter((r) => r.guruh === 'A').map((r) => r.ruxsat);
  protected readonly tanlanganId = signal<number | null>(null);
  /** Tanlangan foydalanuvchi; tanlanmagan bo'lsa birinchi operator (desktop bilan bir xil). */
  protected readonly joriy = computed<Foydalanuvchi | null>(() => {
    const l = this.x.foydalanuvchilar();
    return l.find((f) => f.id === this.tanlanganId()) ?? l.find((f) => f.rol === 'Operator') ?? l[0] ?? null;
  });

  /** So'rovlar ketma-ket yuboriladi: har biri butun to'plamni yozadi, shuning uchun tartib buzilmasligi kerak. */
  private kuyruk: Promise<unknown> = Promise.resolve();

  protected bor(f: Foydalanuvchi, r: Ruxsat) { return f.ruxsatlar.includes(r); }

  protected bosh(f: Foydalanuvchi): string {
    const q = f.toliqIsm.split(/\s+/).filter(Boolean);
    return (q.length >= 2 ? q[0][0] + q[1][0] : (f.toliqIsm[0] ?? '?')).toUpperCase();
  }

  protected almashtir(f: Foydalanuvchi, r: Ruxsat) {
    const yangi = new Set(f.ruxsatlar);
    if (!yangi.delete(r)) yangi.add(r);
    this.yoz(f.id, RUXSAT_ROYXATI.map((q) => q.ruxsat).filter((q) => yangi.has(q)));
  }

  protected standart(f: Foydalanuvchi) {
    this.yoz(f.id, standartRuxsatlar(f.rol));
  }

  /** Optimistik: belgi darhol o'zgaradi; server rad etsa avvalgi to'plam qaytariladi. */
  private yoz(id: number, ruxsatlar: Ruxsat[]) {
    const eski = this.x.foydalanuvchilar().find((f) => f.id === id)?.ruxsatlar ?? [];
    this.almashtirList(id, ruxsatlar);
    this.kuyruk = this.kuyruk.then(async () => {
      try {
        const yangi = await this.x.ruxsatlarniOrnat(id, ruxsatlar);
        this.x.foydalanuvchilar.update((l) => l.map((f) => (f.id === id ? yangi : f)));
        // O'zining ruxsati o'zgargan bo'lsa — menyu va tugmalar darhol yangilansin.
        if (id === this.auth.foydalanuvchi()?.id) await this.auth.yangila();
      } catch (e) {
        this.almashtirList(id, eski);
        this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
      }
    });
  }

  private almashtirList(id: number, ruxsatlar: Ruxsat[]) {
    this.x.foydalanuvchilar.update((l) => l.map((f) => (f.id === id ? { ...f, ruxsatlar } : f)));
  }
}
