import { Component, computed, effect, inject, input, model, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { ishoraPul, litr, pul } from '../../core/format';
import { segmentLitri, segmentSummasi } from '../../core/hisob';
import type { SmenaKorsatkichDto, SmenaTafsilotDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { Oyna } from '../../ui/oyna';
import { SonKiritish } from '../../ui/son-kiritish';

/**
 * "Ko'rsatkichni tuzatish" (sotuv tahririning o'rnida): faqat oxirgi yopilgan smena, sabab majburiy.
 * Tuzatishdan keyingi kutilgan naqd va farq jonli ko'rsatiladi (server natijasi bilan bir xil formula: savdo farqi × narx).
 */
@Component({
  selector: 'tuzatish-dialog',
  imports: [FormsModule, Ikon, Oyna, SonKiritish],
  template: `
    <oyna [(ochiq)]="ochiq" [sarlavha]="til.t('Smenalar_TuzatishSarlavha')" [tagsarlavha]="til.t('Smenalar_Smena', tafsilot().smena.id)" ikon="edit" ikonRang="qizil">
      <form class="forma-ustun" (ngSubmit)="saqla()" autocomplete="off" novalidate>
        <div class="maydon">
          <label for="td-aparat">{{ til.t('Smenalar_TuzatishQaysi') }}</label>
          <select id="td-aparat" class="kiritish" name="aparat" [ngModel]="aparatId()" (ngModelChange)="aparatTanla(+$event)">
            @for (g of oxirgilar(); track g.aparatId) { <option [value]="g.aparatId">{{ til.t('Aparat_Raqami', g.aparatRaqam) }} · {{ g.yoqilgiNomi }}</option> }
          </select>
        </div>
        @if (tanlangan(); as g) {
          <div class="ikki-ustun plitkalar">
            <div class="plitka"><span class="yorliq">{{ til.t('Smenalar_TuzatishBoshi') }}</span><span class="qiymat">{{ litr(g.boshi) }} <small>L</small></span></div>
            <div class="plitka"><span class="yorliq">{{ til.t('Smenalar_TuzatishJoriy') }}</span><span class="qiymat">{{ litr(g.oxiri) }} <small>L</small></span></div>
          </div>
        }
        <div class="maydon">
          <label for="td-qiymat">{{ til.t('Smenalar_TuzatishYangi') }}</label>
          <son-kiritish [(qiymat)]="qiymat" [kasr]="2" sinf="kiritish katta" inputId="td-qiymat" [avto]="true" />
        </div>
        @if (korinish(); as k) {
          <div class="malumot-blok">
            <ikon nomi="info" [olcham]="16" [qalinlik]="2" />
            <span>{{ til.t('Smenalar_TuzatishNatija', litr(k.litr), pul(k.summa), pul(k.kutilgan), ishoraPul(k.farq)) }}</span>
          </div>
        }
        <div class="maydon">
          <label for="td-sabab">{{ til.t('Smenalar_TuzatishSabab') }}</label>
          <input id="td-sabab" class="kiritish" name="sabab" [(ngModel)]="sabab" [placeholder]="til.t('Smenalar_TuzatishSababPlaceholder')" autocomplete="off" />
        </div>
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
  styles: `
    .plitkalar { grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); }
    .plitka { display: flex; flex-direction: column; gap: 3px; }
    .plitka .qiymat { font-size: 20px; font-weight: 800; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .plitka small { font-size: 13px; font-weight: 600; color: var(--matn-2); }
  `,
})
export class TuzatishDialog {
  protected readonly til = inject(Til);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);

  readonly ochiq = model(false);
  readonly tafsilot = input.required<SmenaTafsilotDto>();
  readonly tuzatildi = output<SmenaTafsilotDto>();

  protected readonly litr = litr;
  protected readonly pul = pul;
  protected readonly ishoraPul = ishoraPul;
  protected readonly aparatId = signal(0);
  protected readonly qiymat = signal<number | null>(null);
  protected sabab = '';
  protected readonly band = signal(false);
  protected readonly xato = signal<string | null>(null);

  /** Har aparatning oxirgi segmenti (tuzatish shu segment oxirini o'zgartiradi). */
  protected readonly oxirgilar = computed<SmenaKorsatkichDto[]>(() => {
    const m = new Map<number, SmenaKorsatkichDto>();
    for (const k of this.tafsilot().korsatkichlar) m.set(k.aparatId, k);
    return [...m.values()].sort((a, b) => a.aparatRaqam - b.aparatRaqam);
  });
  protected readonly tanlangan = computed(() => this.oxirgilar().find((g) => g.aparatId === this.aparatId()) ?? null);
  protected readonly korinish = computed(() => {
    const g = this.tanlangan(); const q = this.qiymat(); const s = this.tafsilot().smena;
    if (!g || q == null || q < g.boshi) return null;
    const yangiLitr = segmentLitri(g.boshi, q);
    const yangiSumma = segmentSummasi(yangiLitr, g.narx);
    const kutilgan = s.kutilgan + (yangiSumma - g.summa);
    return { litr: yangiLitr, summa: yangiSumma, kutilgan, farq: (s.sanalganNaqd ?? 0) - kutilgan };
  });

  constructor() {
    effect(() => {
      if (!this.ochiq()) return;
      untracked(() => {
        const birinchi = this.oxirgilar()[0];
        this.aparatId.set(birinchi?.aparatId ?? 0);
        this.qiymat.set(birinchi?.oxiri ?? null);
        this.sabab = '';
        this.xato.set(null);
      });
    });
  }

  protected aparatTanla(id: number) {
    this.aparatId.set(id);
    this.qiymat.set(this.oxirgilar().find((g) => g.aparatId === id)?.oxiri ?? null);
  }

  async saqla() {
    const g = this.tanlangan();
    const q = this.qiymat();
    if (!g || q == null) return this.xato.set(this.til.t('Smenalar_TuzatishXatoQiymat'));
    if (q < g.boshi) return this.xato.set(this.til.t('Smenalar_TuzatishXatoKichik', litr(g.boshi)));
    if (!this.sabab.trim()) return this.xato.set(this.til.t('Smenalar_TuzatishXatoSabab'));
    this.xato.set(null);
    this.band.set(true);
    try {
      const t = await this.server.korsatkichTuzat(this.tafsilot().smena.id, { aparatId: g.aparatId, qiymat: q, sabab: this.sabab.trim() });
      this.ochiq.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
      this.tuzatildi.emit(t);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
