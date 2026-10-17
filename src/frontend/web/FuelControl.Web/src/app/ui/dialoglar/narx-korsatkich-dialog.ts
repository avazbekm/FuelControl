import { Component, computed, effect, inject, input, model, output, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { litr, pul } from '../../core/format';
import { bakOgohi, litrSent, narxSegmentlari, pultJamiga, segmentLitri, segmentSummasi } from '../../core/hisob';
import type { AparatDto, SmenaTafsilotDto } from '../../api/model';
import { Ikon } from '../ikon';
import { Oyna } from '../oyna';
import { SonKiritish } from '../son-kiritish';
import { EnterKeyingi } from '../enter-keyingi';
import { BakOgohMatn, BakOgohQatori } from '../bak-ogoh';
import { KrugIzoh, KrugQatori, PultQiymat } from '../krug';
import { PultTasdiq } from '../pult-tasdiq';

/**
 * Ochiq smenada narx o'zgarishida yozilgan pult ko'rsatkichini tuzatish (docs §8.14): aparatning shu smenadagi OXIRGI narx o'zgarishi segmenti.
 * `PUT /smenalar/{id}/narx-korsatkichi`; `Qiymat == Boshi` bo'lsa server segmentni o'chiradi (narx o'zgarguncha savdo bo'lmagan).
 * Bak ogohlantirishi (§8.15): chiqyapti = shu smenadagi BOSHQA narx segmentlari litri + tuzatilayotgan segmentning yangi litri; bor = `BakQoldiq`.
 */
@Component({
  selector: 'narx-korsatkich-dialog',
  imports: [FormsModule, Ikon, Oyna, SonKiritish, EnterKeyingi, BakOgohMatn, PultTasdiq, PultQiymat, KrugIzoh],
  template: `
    <oyna [(ochiq)]="ochiq" [sarlavha]="aparat() ? til.t('NarxQayd_Sarlavha', til.t('Aparat_Raqami', aparat()!.raqam)) : ''" [tagsarlavha]="tafsilot() ? til.t('Smenalar_Smena', tafsilot()!.smena.id) : ''" ikon="edit" ikonRang="qizil">
      <form class="forma-ustun" enterKeyingi (ngSubmit)="saqlaBos()" autocomplete="off" novalidate data-test="nk-forma">
        @if (segment(); as g) {
          <div class="ikki-ustun plitkalar">
            <div class="plitka"><span class="yorliq">{{ til.t('Yopish_Oldingi') }}</span><span class="qiymat" data-test="nk-boshi"><pult-qiymat [jami]="g.boshi" [krugHajmi]="krugHajmi()" /></span></div>
            <div class="plitka"><span class="yorliq">{{ til.t('Smenalar_TuzatishJoriy') }}</span><span class="qiymat" data-test="nk-joriy"><pult-qiymat [jami]="g.oxiri" [krugHajmi]="krugHajmi()" birlik /></span></div>
          </div>
        }
        <div class="maydon">
          <label for="nk-qiymat">{{ til.t('NarxQayd_Togri') }}</label>
          <son-kiritish [(qiymat)]="qiymat" [kasr]="2" sinf="kiritish katta" inputId="nk-qiymat" [avto]="true" />
          <krug-izoh [natija]="natija()" [krugHajmi]="krugHajmi()" />
          <bak-ogoh-matn [ogoh]="ogoh()" />
        </div>
        @if (korinish(); as k) {
          <div class="malumot-blok" data-test="nk-hisob">
            <ikon nomi="info" [olcham]="16" [qalinlik]="2" />
            <span>{{ k.ochiriladi ? til.t('NarxQayd_Ochiriladi') : til.t('Yopish_EskiNarxda', til.t('Aparat_Raqami', aparat()!.raqam), litr(k.litr), pul(k.narx), pul(k.summa)) }}</span>
          </div>
        }
        <div class="maydon">
          <label for="nk-sabab">{{ til.t('Smenalar_TuzatishSabab') }}</label>
          <input id="nk-sabab" class="kiritish" name="sabab" [(ngModel)]="sabab" [placeholder]="til.t('Smenalar_TuzatishSababPlaceholder')" autocomplete="off" />
        </div>
        <div class="malumot-blok"><ikon nomi="info" [olcham]="16" [qalinlik]="2" /><span>{{ til.t('NarxQayd_TuzatishIzoh') }}</span></div>
        @if (xato()) { <div class="xato-matn" role="alert" data-test="nk-xato">{{ xato() }}</div> }
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
export class NarxKorsatkichDialog {
  protected readonly til = inject(Til);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);

  readonly ochiq = model(false);
  /** Tuzatiladigan aparat (ro'yxatdagi joriy nusxa: bak qoldig'i yangilanib turadi). */
  readonly aparat = input<AparatDto | null>(null);
  /** Ochiq smena tafsiloti (`korsatkichlar` — segmentlar). */
  readonly tafsilot = input<SmenaTafsilotDto | null>(null);
  readonly tuzatildi = output<SmenaTafsilotDto>();

  protected readonly litr = litr;
  protected readonly pul = pul;
  protected readonly qiymat = signal<number | null>(null);
  protected sabab = '';
  protected readonly band = signal(false);
  private readonly tasdiq = viewChild(PultTasdiq);
  protected readonly xato = signal<string | null>(null);

  /** Aparatning shu smenadagi narx o'zgarishi segmentlari; tuzatiladigani — oxirgisi. */
  private readonly segmentlar = computed(() => narxSegmentlari(this.tafsilot()?.korsatkichlar, this.aparat()?.id ?? 0));
  protected readonly segment = computed(() => { const s = this.segmentlar(); return s.length ? s[s.length - 1] : null; });

  protected readonly krugHajmi = computed(() => this.aparat()?.krugHajmi ?? null);
  /** Operator yozgan pult qiymati → krug natijasi va API'ga yuboriladigan UMUMIY litr (docs §8.16; asos — segmentning `Boshi`). */
  protected readonly natija = computed(() => { const g = this.segment(); const p = this.qiymat(); return g && p != null ? pultJamiga(p, g.boshi, this.krugHajmi()) : null; });
  private readonly jami = computed(() => { const n = this.natija(); return n && !n.xato ? n.jami : null; });

  /** Jonli hisob: narx o'zgarguncha `Boshi → q` eski narxda; `q == Boshi` bo'lsa qator o'chiriladi. */
  protected readonly korinish = computed(() => {
    const g = this.segment(); const q = this.jami();
    if (!g || q == null || litrSent(q) < litrSent(g.boshi)) return null;
    const l = segmentLitri(g.boshi, q);
    return { ochiriladi: litrSent(q) === litrSent(g.boshi), litr: l, narx: g.narx, summa: segmentSummasi(l, g.narx) };
  });

  protected readonly ogoh = computed(() => {
    const g = this.segment(); const a = this.aparat();
    return g && a ? bakOgohi(g.boshi, this.jami(), a.bakQoldiq, this.segmentlar().slice(0, -1)) : null;
  });
  protected readonly bakQatorlari = computed<BakOgohQatori[]>(() => {
    const o = this.ogoh(); const a = this.aparat();
    return o && a ? [{ raqam: a.raqam, chiqadi: o.chiqadi, bakda: o.bakda }] : [];
  });

  /** Krug oshgan bo'lsa — "Pult krugi oshdi" tasdig'i (docs §8.16). */
  protected readonly krugQatorlari = computed<KrugQatori[]>(() => {
    const n = this.natija(); const g = this.segment(); const a = this.aparat(); const j = this.jami();
    return n?.oshdi && g && a && j != null ? [{ raqam: a.raqam, krugdan: n.krug! - 1, krugga: n.krug!, sotilgan: segmentLitri(g.boshi, j) }] : [];
  });

  constructor() {
    effect(() => {
      if (!this.ochiq()) return;
      untracked(() => { this.qiymat.set(null); this.sabab = ''; this.xato.set(null); });
    });
  }

  /** Saqlash tugmasi: tekshiruvlar (server bilan bir xil matnlar), so'ng bakdagidan ko'p litr chiqsa bak tasdig'i. */
  protected saqlaBos() {
    const g = this.segment(); const q = this.jami();
    if (!g || this.qiymat() == null) return this.xato.set(this.til.t('Smenalar_TuzatishXatoQiymat'));
    if (this.natija()?.xato) return this.xato.set(this.til.t('Krug_XatoKatta', litr(this.krugHajmi() ?? 0)));
    if (q == null || litrSent(q) < litrSent(g.boshi)) return this.xato.set(this.til.t('Smenalar_TuzatishXatoKichik', litr(g.boshi)));
    if (litrSent(q) === litrSent(g.oxiri)) return this.xato.set(this.til.t('NarxQayd_XatoTeng'));
    if (!this.sabab.trim()) return this.xato.set(this.til.t('Smenalar_TuzatishXatoSabab'));
    this.xato.set(null);
    this.tasdiq()?.boshla();
  }

  protected async yubor() {
    const t = this.tafsilot(); const a = this.aparat(); const q = this.jami();
    if (!t || !a || q == null) return;
    this.band.set(true);
    try {
      const yangi = await this.server.narxKorsatkichTuzat(t.smena.id, { aparatId: a.id, qiymat: q, sabab: this.sabab.trim() });
      this.ochiq.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
      this.tuzatildi.emit(yangi);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
