import { Component, computed, inject, signal } from '@angular/core';
import { Auth } from '../../core/auth';
import { Til } from '../../core/til';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { harflar } from '../../core/format';
import type { FoydalanuvchiDto, Ruxsat } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { RUXSAT_ROYXATI, SozlamalarXizmati, standartRuxsatlar } from './sozlamalar-xizmati';

/**
 * "Ruxsatlar" (docs/dizayn/SozlamalarRuxsatlar): chapda foydalanuvchilar, o'ngda tanlanganining bo'limlar va amallar ruxsatlari.
 * O'zgarishlar qoralama (har foydalanuvchi uchun alohida) — "Ruxsatlarni saqlash" bilan yoziladi; "Rol bo'yicha tiklash" standartga qaytaradi.
 */
@Component({
  selector: 'sozlamalar-ruxsatlar',
  imports: [Ikon],
  template: `
    <div class="qator-panjara">
      <section class="shisha foyd-karta yon" aria-labelledby="rx-foyd">
        <h2 id="rx-foyd">{{ til.t('Foydalanuvchilar') }}</h2>
        @for (f of x.foydalanuvchilar(); track f.id) {
          <button type="button" class="foyd" [class.tanlangan]="f.id === tanlanganId()" [attr.aria-pressed]="f.id === tanlanganId()" (click)="tanlash.set(f.id)">
            <span class="avatar katta">{{ harflar(f.toliqIsm) }}</span>
            <span class="foyd-matn">
              <span class="ism">{{ f.toliqIsm }}</span>
              <span class="rol">{{ til.t('Rol_' + f.rol) }}</span>
            </span>
            @if (ozgargan(f)) { <span class="belgi-nuqta" [attr.title]="til.t('Sozlama_Saqlanmagan')"></span> }
          </button>
        }
      </section>

      <section class="shisha ruxsat-karta asosiy" aria-labelledby="rx-joriy">
        @if (tanlangan(); as f) {
          <div class="bosh-qator">
            <div class="bosh-matn">
              <h2 id="rx-joriy">{{ f.toliqIsm }}</h2>
              <span class="izoh">{{ til.t('Rol_' + f.rol) }} · {{ til.t('Sozlama_RuxsatSoni', joriyRuxsatlar().size) }}</span>
            </div>
            <button type="button" class="tugma" (click)="standart()"><ikon nomi="refresh" [olcham]="15" [qalinlik]="2" /> {{ til.t('Sozlama_RolBoyichaTiklash') }}</button>
          </div>

          <fieldset>
            <legend>{{ til.t('Sozlama_Bolimlar') }}</legend>
            <div class="bolim-panjara">
              @for (r of bolimlar; track r.ruxsat) {
                <label class="bolim-katak">
                  <input type="checkbox" [checked]="joriyRuxsatlar().has(r.ruxsat)" (change)="almashtir(r.ruxsat)" />
                  <span class="nom">{{ til.t('Ruxsat_' + r.ruxsat) }}</span>
                  @if (r.yangi) { <span class="yangi">{{ til.t('Yangi') }}</span> }
                </label>
              }
            </div>
          </fieldset>

          <fieldset>
            <legend>{{ til.t('Sozlama_Amallar') }}</legend>
            <div class="amal-panjara">
              @for (r of amallar; track r.ruxsat) {
                <label class="amal-katak">
                  <input type="checkbox" [checked]="joriyRuxsatlar().has(r.ruxsat)" (change)="almashtir(r.ruxsat)" />
                  <span class="amal-matn">
                    <span class="amal-nom">{{ til.t('Ruxsat_' + r.ruxsat) }}@if (r.yangi) { <span class="yangi">{{ til.t('Yangi') }}</span> }</span>
                    <span class="amal-izoh">{{ til.t('Ruxsat_' + r.ruxsat + '_Izoh') }}</span>
                  </span>
                </label>
              }
            </div>
          </fieldset>

          <div class="eslatma"><ikon nomi="info" [olcham]="16" [qalinlik]="2" /><span>{{ til.t('Sozlama_RuxsatEslatma') }}</span></div>
          @if (xato()) { <div class="xato-matn" role="alert">{{ xato() }}</div> }
          <div class="saqlash-qator">
            <button type="button" class="tugma asosiy" [disabled]="band() || !ozgargan(f)" (click)="saqla()">
              @if (band()) { <span class="aylanma"></span> } @else { <ikon nomi="check" [olcham]="16" [qalinlik]="2.4" /> } {{ til.t('Sozlama_RuxsatlarniSaqlash') }}
            </button>
          </div>
        } @else {
          <div class="bosh">{{ til.t('MalumotYoq') }}</div>
        }
      </section>
    </div>
  `,
  styles: `
    .qator-panjara { align-items: flex-start; }
    .qator-panjara > .yon { flex: 1 1 300px; }
    .qator-panjara > .asosiy { flex: 999 1 560px; }
    .foyd-karta { padding: 18px 14px; display: flex; flex-direction: column; gap: 8px; min-width: 0; }
    .foyd-karta h2 { margin: 0 6px 4px; }
    .foyd {
      display: flex; align-items: center; gap: 12px; width: 100%; min-height: 56px; padding: 8px 12px; border-radius: 18px; font: inherit; color: var(--matn);
      cursor: pointer; background: transparent; border: 1px solid transparent; text-align: left;
    }
    .foyd.tanlangan { background: var(--malumot-fon); border: 2px solid #2F6BFF; padding: 7px 11px; }
    .avatar.katta { width: 38px; height: 38px; }
    .foyd-matn { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
    .ism { font-size: 14px; font-weight: 700; }
    .rol { font-size: 12.5px; color: var(--matn-2); }
    .belgi-nuqta { width: 8px; height: 8px; border-radius: 999px; background: var(--sariq); flex: none; }

    .ruxsat-karta { padding: 20px; display: flex; flex-direction: column; gap: 16px; min-width: 0; }
    .bosh-qator { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 10px; }
    .bosh-matn { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
    .bosh-matn h2 { font-size: 19px; font-weight: 800; }
    .izoh { font-size: 13px; color: var(--matn-2); }
    fieldset { margin: 0; padding: 0; border: 0; display: flex; flex-direction: column; gap: 8px; min-width: 0; }
    legend { padding: 0 0 8px; font-size: 11px; font-weight: 700; letter-spacing: 1px; text-transform: uppercase; color: var(--matn-2); }
    .bolim-panjara { display: grid; grid-template-columns: repeat(auto-fill, minmax(190px, 1fr)); gap: 8px; }
    .amal-panjara { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 8px; }
    .bolim-katak, .amal-katak { display: flex; gap: 12px; border-radius: 16px; cursor: pointer; background: var(--plitka-fon); border: 1px solid var(--chiziq); min-width: 0; }
    .bolim-katak { align-items: center; min-height: 48px; padding: 0 14px; font-size: 14px; font-weight: 600; gap: 10px; }
    .amal-katak { align-items: flex-start; min-height: 56px; padding: 12px 14px; }
    .bolim-katak input, .amal-katak input { width: 20px; height: 20px; flex: none; margin: 0; accent-color: var(--asosiy); }
    .amal-katak input { margin-top: 1px; }
    .nom { flex: 1; min-width: 0; }
    .amal-matn { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 3px; }
    .amal-nom { display: flex; align-items: center; gap: 8px; font-size: 14px; font-weight: 700; }
    .amal-izoh { font-size: 12.5px; line-height: 1.4; color: var(--matn-2); }
    .yangi { padding: 2px 8px; border-radius: 999px; font-size: 11px; font-weight: 700; background: var(--b-kok-fon); color: var(--b-kok); }
    .eslatma { display: flex; align-items: flex-start; gap: 10px; padding: 12px 14px; border-radius: 16px; background: light-dark(#FFF4DB, var(--ogohlik-fon)); border: 1px solid light-dark(#F3D48A, var(--ogohlik-chegara)); color: light-dark(#4A3A12, var(--b-sariq)); font-size: 13px; line-height: 1.45; }
    .eslatma ikon { color: var(--sariq); margin-top: 1px; }
    .saqlash-qator { display: flex; justify-content: flex-end; }
    @media (max-width: 480px) { .amal-panjara { grid-template-columns: minmax(0, 1fr); } .saqlash-qator .tugma { width: 100%; } }
  `,
})
export class RuxsatlarBolimi {
  protected readonly til = inject(Til);
  protected readonly x = inject(SozlamalarXizmati);
  private readonly auth = inject(Auth);
  private readonly bildirish = inject(Bildirish);

  protected readonly harflar = harflar;
  protected readonly bolimlar = RUXSAT_ROYXATI.filter((r) => r.guruh === 'B');
  protected readonly amallar = RUXSAT_ROYXATI.filter((r) => r.guruh === 'A');

  protected readonly tanlash = signal<number | null>(null);
  /** Tanlangan foydalanuvchi: belgilanmagan bo'lsa birinchi operator (dizayndagidek), u ham bo'lmasa birinchisi. */
  protected readonly tanlanganId = computed(() => this.tanlash() ?? this.birinchi());
  protected readonly tanlangan = computed<FoydalanuvchiDto | null>(() => this.x.foydalanuvchilar().find((f) => f.id === this.tanlanganId()) ?? null);
  /** Qoralamalar (saqlanmagan o'zgarishlar), foydalanuvchi id'si bo'yicha. */
  private readonly qoralama = signal<Record<number, Ruxsat[]>>({});
  protected readonly joriyRuxsatlar = computed(() => {
    const f = this.tanlangan();
    return new Set<Ruxsat>(f ? this.qoralama()[f.id] ?? f.ruxsatlar : []);
  });
  protected readonly band = signal(false);
  protected readonly xato = signal<string | null>(null);

  private birinchi(): number | null {
    const r = this.x.foydalanuvchilar();
    return (r.find((f) => f.rol === 'Operator') ?? r[0])?.id ?? null;
  }

  protected ozgargan(f: FoydalanuvchiDto): boolean {
    const q = this.qoralama()[f.id];
    if (!q) return false;
    const a = new Set(q), b = new Set(f.ruxsatlar);
    return a.size !== b.size || [...a].some((r) => !b.has(r));
  }

  protected almashtir(r: Ruxsat) {
    const f = this.tanlangan();
    if (!f) return;
    const joriy = new Set(this.joriyRuxsatlar());
    if (joriy.has(r)) joriy.delete(r); else joriy.add(r);
    this.xato.set(null);
    this.qoralama.update((o) => ({ ...o, [f.id]: RUXSAT_ROYXATI.map((x) => x.ruxsat).filter((x) => joriy.has(x)) }));
  }

  protected standart() {
    const f = this.tanlangan();
    if (!f) return;
    this.xato.set(null);
    this.qoralama.update((o) => ({ ...o, [f.id]: standartRuxsatlar(f.rol) }));
  }

  protected async saqla() {
    const f = this.tanlangan();
    if (!f || !this.ozgargan(f)) return;
    this.band.set(true);
    this.xato.set(null);
    try {
      await this.x.ruxsatlarniOrnat(f.id, this.qoralama()[f.id]);
      this.qoralama.update((o) => { const { [f.id]: _, ...qolgan } = o; return qolgan; });
      // O'zining ruxsatlari o'zgargan bo'lsa — menyu darhol yangilansin.
      if (f.id === this.auth.foydalanuvchi()?.id) await this.auth.yangila();
      this.bildirish.korsat(this.til.t('Saqlandi'));
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
