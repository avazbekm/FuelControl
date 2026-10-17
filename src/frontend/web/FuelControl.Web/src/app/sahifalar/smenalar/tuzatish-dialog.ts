import { Component, computed, effect, inject, input, model, output, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { ishoraPul, litr, pul } from '../../core/format';
import { bakOgohi, krugKorinish, litrSent, pultJamiga, segmentLitri, segmentSummasi, sentLitr } from '../../core/hisob';
import type { AparatDto, SmenaKorsatkichDto, SmenaTafsilotDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { Oyna } from '../../ui/oyna';
import { SonKiritish } from '../../ui/son-kiritish';
import { BakOgohMatn, BakOgohQatori } from '../../ui/bak-ogoh';
import { KrugIzoh, KrugQatori, PultQiymat } from '../../ui/krug';
import { PultTasdiq } from '../../ui/pult-tasdiq';

/**
 * "Ko'rsatkichni tuzatish" (sotuv tahririning o'rnida): faqat oxirgi yopilgan smena, sabab majburiy.
 * Tuzatishdan keyingi kutilgan naqd va farq jonli ko'rsatiladi (server natijasi bilan bir xil formula: savdo farqi × narx).
 */
@Component({
  selector: 'tuzatish-dialog',
  imports: [FormsModule, Ikon, Oyna, SonKiritish, BakOgohMatn, PultTasdiq, PultQiymat, KrugIzoh],
  template: `
    <oyna [(ochiq)]="ochiq" [sarlavha]="til.t('Smenalar_TuzatishSarlavha')" [tagsarlavha]="til.t('Smenalar_Smena', tafsilot().smena.id)" ikon="edit" ikonRang="qizil">
      <form class="forma-ustun" (ngSubmit)="saqlaBos()" autocomplete="off" novalidate>
        <div class="maydon">
          <label for="td-aparat">{{ til.t('Smenalar_TuzatishQaysi') }}</label>
          <select id="td-aparat" class="kiritish" name="aparat" [ngModel]="aparatId()" (ngModelChange)="aparatTanla(+$event)">
            @for (g of oxirgilar(); track g.aparatId) { <option [value]="g.aparatId">{{ til.t('Aparat_Raqami', g.aparatRaqam) }} · {{ g.yoqilgiNomi }}</option> }
          </select>
        </div>
        @if (tanlangan(); as g) {
          <div class="ikki-ustun plitkalar">
            <div class="plitka"><span class="yorliq">{{ til.t('Smenalar_TuzatishBoshi') }}</span><span class="qiymat"><pult-qiymat [jami]="g.boshi" [krugHajmi]="krugHajmi()" birlik /></span></div>
            <div class="plitka"><span class="yorliq">{{ til.t('Smenalar_TuzatishJoriy') }}</span><span class="qiymat"><pult-qiymat [jami]="g.oxiri" [krugHajmi]="krugHajmi()" birlik /></span></div>
          </div>
        }
        <div class="maydon">
          <label for="td-qiymat">{{ til.t('Smenalar_TuzatishYangi') }}</label>
          <son-kiritish [(qiymat)]="qiymat" [kasr]="2" sinf="kiritish katta" inputId="td-qiymat" [avto]="true" />
          <krug-izoh [natija]="natija()" [krugHajmi]="krugHajmi()" />
          <bak-ogoh-matn [ogoh]="ogoh()" />
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
    <pult-tasdiq [krugQatorlari]="krugQatorlari()" [bakQatorlari]="bakQatorlari()" (davom)="yubor()" />
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
  private readonly tasdiq = viewChild(PultTasdiq);
  /** Bak qoldig'i (ogohlantirish uchun) — dialog ochilganda yangilanadi. */
  private readonly aparatlar = signal<AparatDto[]>([]);

  /** Har aparatning oxirgi segmenti (tuzatish shu segment oxirini o'zgartiradi). */
  protected readonly oxirgilar = computed<SmenaKorsatkichDto[]>(() => {
    const m = new Map<number, SmenaKorsatkichDto>();
    for (const k of this.tafsilot().korsatkichlar) m.set(k.aparatId, k);
    return [...m.values()].sort((a, b) => a.aparatRaqam - b.aparatRaqam);
  });
  protected readonly tanlangan = computed(() => this.oxirgilar().find((g) => g.aparatId === this.aparatId()) ?? null);
  /** Tanlangan aparatning krug hajmi (docs §8.16); aparatlar yuklanguncha null. */
  protected readonly krugHajmi = computed(() => this.aparatlar().find((x) => x.id === this.aparatId())?.krugHajmi ?? null);
  /** Operator yozgan pult qiymati → krug natijasi va API'ga yuboriladigan UMUMIY litr (asos — segmentning `Boshi`). */
  protected readonly natija = computed(() => { const g = this.tanlangan(); const p = this.qiymat(); return g && p != null ? pultJamiga(p, g.boshi, this.krugHajmi()) : null; });
  private readonly jami = computed(() => { const n = this.natija(); return n && !n.xato ? n.jami : null; });
  protected readonly korinish = computed(() => {
    const g = this.tanlangan(); const q = this.jami(); const s = this.tafsilot().smena;
    if (!g || q == null || q < g.boshi) return null;
    const yangiLitr = segmentLitri(g.boshi, q);
    const yangiSumma = segmentSummasi(yangiLitr, g.narx);
    const kutilgan = s.kutilgan + (yangiSumma - g.summa);
    return { litr: yangiLitr, summa: yangiSumma, kutilgan, farq: (s.sanalganNaqd ?? 0) - kutilgan };
  });

  /**
   * Bakdagidan ko'p litr chiqsa (docs §8.15): chiqadi = yangi − segment boshi; bakda bor = `BakQoldiq` + tuzatilayotgan segmentning eski litri
   * (u smena yopilganda bakdan ayrilgan edi).
   */
  protected readonly ogoh = computed(() => {
    const g = this.tanlangan();
    const a = this.aparatlar().find((x) => x.id === g?.aparatId);
    return g && a ? bakOgohi(g.boshi, this.jami(), sentLitr(litrSent(a.bakQoldiq) + litrSent(g.litr))) : null;
  });
  protected readonly bakQatorlari = computed<BakOgohQatori[]>(() => {
    const o = this.ogoh();
    const g = this.tanlangan();
    return o && g ? [{ raqam: g.aparatRaqam, chiqadi: o.chiqadi, bakda: o.bakda }] : [];
  });

  /** Krug oshgan bo'lsa — "Pult krugi oshdi" tasdig'i (docs §8.16). */
  protected readonly krugQatorlari = computed<KrugQatori[]>(() => {
    const n = this.natija(); const g = this.tanlangan(); const j = this.jami();
    return n?.oshdi && g && j != null ? [{ raqam: g.aparatRaqam, krugdan: n.krug! - 1, krugga: n.krug!, sotilgan: segmentLitri(g.boshi, j) }] : [];
  });
  /** Dialog ochilganda maydondagi qiymat: segmentning joriy oxiri (krugli aparatda — krugsiz qismi). */
  private boshQiymat(g: SmenaKorsatkichDto | undefined, aparatlar: AparatDto[]): number | null {
    return g ? krugKorinish(g.oxiri, aparatlar.find((x) => x.id === g.aparatId)?.krugHajmi).pult : null;
  }

  constructor() {
    effect(() => {
      if (!this.ochiq()) return;
      untracked(() => {
        const birinchi = this.oxirgilar()[0];
        this.aparatId.set(birinchi?.aparatId ?? 0);
        this.qiymat.set(null);
        // Krugli aparatda boshlang'ich qiymat pult shaklida — shuning uchun aparatlar (krug hajmi) yuklangach to'ldiriladi.
        this.server.aparatlar().then((a) => { this.aparatlar.set(a); this.qiymat.set(this.boshQiymat(birinchi, a)); }).catch(() => this.qiymat.set(this.boshQiymat(birinchi, [])));
        this.sabab = '';
        this.xato.set(null);
      });
    });
  }

  protected aparatTanla(id: number) {
    this.aparatId.set(id);
    this.qiymat.set(this.boshQiymat(this.oxirgilar().find((g) => g.aparatId === id), this.aparatlar()));
  }

  /** Saqlash tugmasi: avval tekshiruvlar, so'ng (bakdagidan ko'p chiqsa) bak tasdig'i. */
  protected saqlaBos() {
    const g = this.tanlangan();
    const q = this.jami();
    if (!g || this.qiymat() == null) return this.xato.set(this.til.t('Smenalar_TuzatishXatoQiymat'));
    if (this.natija()?.xato) return this.xato.set(this.til.t('Krug_XatoKatta', litr(this.krugHajmi() ?? 0)));
    if (q == null || q < g.boshi) return this.xato.set(this.til.t('Smenalar_TuzatishXatoKichik', litr(g.boshi)));
    if (!this.sabab.trim()) return this.xato.set(this.til.t('Smenalar_TuzatishXatoSabab'));
    this.xato.set(null);
    this.tasdiq()?.boshla();
  }

  protected async yubor() {
    const g = this.tanlangan();
    const q = this.jami();
    if (!g || q == null) return;
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
