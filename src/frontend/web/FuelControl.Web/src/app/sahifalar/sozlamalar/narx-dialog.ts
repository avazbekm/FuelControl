import { Component, computed, effect, inject, input, model, output, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { litr, pul } from '../../core/format';
import { BakOgohi, PultNatijasi, bakOgohi, joriyKorsatkich, krugKorinish, litrSent, narxSegmentlari, pultJamiga, segmentLitri, segmentSummasi, sentLitr } from '../../core/hisob';
import type { AparatDto, AparatKorsatkichDto, YoqilgiTuriDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { Oyna } from '../../ui/oyna';
import { SonKiritish } from '../../ui/son-kiritish';
import { EnterKeyingi } from '../../ui/enter-keyingi';
import { BakOgohMatn, BakOgohQatori } from '../../ui/bak-ogoh';
import { PultTasdiq } from '../../ui/pult-tasdiq';
import { KrugIzoh, KrugQatori, PultQiymat } from '../../ui/krug';
import { SozlamalarXizmati } from './sozlamalar-xizmati';

interface AparatQatori {
  aparat: AparatDto;
  /** Oldingi ko'rsatkich: ochiq smenada oxirgi qayd etilgan segmentning `oxiri`, yo'q bo'lsa aparat `TotalLitr` (§7.1). */
  oldingi: number;
  segmentBor: boolean;
  /** Operator yozgan pult qiymati (krugli aparatda krugsiz qism). */
  qiymat: number | null;
  /** Umumiy litr (API'ga yuboriladi) — krugli aparatda pultdan o'giriladi (docs §8.16). */
  jami: number | null;
  krug: PultNatijasi | null;
  holat: 'bosh' | 'xato' | 'ok';
  litr: number;
  summa: number;
  /** Bakdagidan ko'p litr chiqsa (docs §8.15): shu smenadagi jami litr (qayd etilgan segmentlar + shu qator) > `BakQoldiq`. */
  ogoh: BakOgohi | null;
}

/**
 * "Narxni o'zgartirish" dialogi (docs/dizayn/NarxOzgarishi). Ochiq smena bo'lsa — shu yoqilg'i aparatlarining hozirgi pult ko'rsatkichi majburiy:
 * shu paytgacha sotilgan litr eski narxda alohida segment bo'ladi (§1.10). Smena yopiq bo'lsa — faqat yangi narx.
 */
@Component({
  selector: 'narx-dialog',
  imports: [FormsModule, Ikon, Oyna, SonKiritish, EnterKeyingi, BakOgohMatn, PultTasdiq, PultQiymat, KrugIzoh],
  template: `
    <oyna [(ochiq)]="ochiq" [sarlavha]="til.t('Narx_Sarlavha')" [tagsarlavha]="til.t('Narx_Kontekst', yoqilgi()?.nomi ?? '')" ikon="tag" [kenglik]="620">
      <form class="forma-ustun" enterKeyingi (ngSubmit)="saqlaBos()" autocomplete="off" novalidate>
        <div class="ikki-ustun narx-juft">
          <div class="maydon">
            <span class="yorliq">{{ til.t('Narx_Hozirgi') }}</span>
            <span class="hozirgi son">{{ pul(yoqilgi()?.narx ?? 0) }}</span>
          </div>
          <div class="maydon">
            <label for="nd-narx">{{ til.t('Narx_YangiNarx') }}</label>
            <son-kiritish [(qiymat)]="yangiNarx" sinf="kiritish pul" inputId="nd-narx" [avto]="true" />
          </div>
        </div>

        @if (smenaId(); as sid) {
          <div class="ogohlik-blok katta-blok">
            <ikon nomi="warning" [olcham]="18" [qalinlik]="2" />
            <span class="ogohlik-matn">
              <b>{{ til.t('Narx_SmenaOchiq', sid) }}</b>
              <span>{{ til.t('Narx_SmenaOchiqIzoh', yoqilgi()?.nomi ?? '') }}</span>
            </span>
          </div>
          <div class="aparat-royxat">
            @for (q of qatorlar(); track q.aparat.id) {
              <div class="aparat-qator">
                <div class="aparat-ustki">
                  <span class="nom"><b>{{ til.t('Aparat_Raqami', q.aparat.raqam) }}</b>
                    <span class="yoq-pill" [style.--r]="yoqilgi()?.rang">{{ q.aparat.yoqilgiNomi }}</span>
                  </span>
                  <span class="oldingi">{{ til.t(q.segmentBor ? 'Narx_OxirgiQayd' : 'Narx_SmenaBoshida') }}<b class="son"><pult-qiymat [jami]="q.oldingi" [krugHajmi]="q.aparat.krugHajmi" /></b></span>
                  <son-kiritish [qiymat]="q.qiymat" (qiymatChange)="qiymatYoz(q.aparat.id, $event)" [kasr]="2" [sinf]="q.holat === 'xato' ? 'kiritish korsatkich xato' : 'kiritish korsatkich'"
                                [ariaLabel]="til.t('Narx_KorsatkichLabel', til.t('Aparat_Raqami', q.aparat.raqam))" [placeholder]="pultMatni(q.oldingi, q.aparat.krugHajmi)" />
                </div>
                @if (!q.krug?.xato) { <span class="natija son" [class.xato-matn]="q.holat === 'xato'">{{ natijaMatni(q) }}</span> }
                <krug-izoh [natija]="q.krug" [krugHajmi]="q.aparat.krugHajmi" />
                <bak-ogoh-matn [ogoh]="q.ogoh" />
              </div>
            } @empty { <div class="bosh">{{ til.t('Narx_AparatYoq') }}</div> }
          </div>
          <div class="jami" aria-live="polite">
            <span class="jami-matn">
              <b>{{ til.t('Narx_EskiNarxdaSotilgan') }}</b>
              <span class="son">{{ jamiMatn() }}</span>
            </span>
            <span class="jami-summa son">{{ hammasiTayyor() ? pul(jamiSumma()) : '—' }}</span>
          </div>
          <p class="yordam-matn">{{ til.t('Narx_IkkiQism', pul(yoqilgi()?.narx ?? 0)) }}</p>
        } @else {
          <div class="malumot-blok"><ikon nomi="info" [olcham]="16" [qalinlik]="2" /><span>{{ til.t('Narx_SmenaYopiqIzoh') }}</span></div>
        }

        @if (xato()) { <div class="xato-matn" role="alert">{{ xato() }}</div> }
        <div class="amallar">
          <button type="button" class="tugma" (click)="ochiq.set(false)">{{ til.t('BekorQilish') }}</button>
          <button type="submit" class="tugma asosiy" [disabled]="band() || !saqlash()">
            @if (band()) { <span class="aylanma"></span> } @else { <ikon nomi="check" [olcham]="16" [qalinlik]="2.4" /> } {{ til.t('Narx_Saqlash') }}
          </button>
        </div>
      </form>
    </oyna>
    <pult-tasdiq [krugQatorlari]="krugQatorlari()" [bakQatorlari]="bakQatorlari()" (davom)="saqla()" />
  `,
  styles: `
    .narx-juft { grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 12px; }
    .hozirgi {
      min-height: 52px; padding: 0 20px; border-radius: 26px; display: flex; align-items: center; justify-content: flex-end;
      font-size: 20px; font-weight: 800; color: var(--matn-2); background: var(--naycha); border: 1px dashed var(--kiritish-chegara);
    }
    .katta-blok { padding: 14px 16px; border-radius: 18px; gap: 12px; }
    .ogohlik-matn { display: flex; flex-direction: column; gap: 3px; font-size: 13px; line-height: 1.45; }
    .ogohlik-matn b { font-size: 14px; font-weight: 800; }
    .aparat-royxat { display: flex; flex-direction: column; gap: 10px; }
    .aparat-qator { display: flex; flex-direction: column; gap: 8px; padding: 14px 16px; border-radius: 20px; background: light-dark(#fff, var(--plitka-fon)); border: 1px solid var(--chiziq); }
    .aparat-ustki { display: flex; flex-wrap: wrap; align-items: center; gap: 10px 14px; }
    .nom { display: flex; align-items: center; gap: 8px; min-width: 150px; font-size: 15px; }
    .nom b { font-weight: 800; }
    .yoq-pill {
      display: inline-flex; align-items: center; gap: 6px; padding: 4px 11px; border-radius: 999px; font-size: 12px; font-weight: 700;
      color: color-mix(in srgb, var(--r, #2563EB) 72%, light-dark(#0B1530, #fff)); background: color-mix(in srgb, var(--r, #2563EB) 16%, light-dark(#fff, #0C1120));
    }
    .yoq-pill::before { content: ""; width: 7px; height: 7px; border-radius: 999px; background: var(--r, #2563EB); }
    .oldingi { display: flex; flex-direction: column; gap: 2px; font-size: 12.5px; color: var(--matn-2); }
    .oldingi b { font-size: 14px; font-weight: 700; color: var(--matn-3); }
    .natija { font-size: 13px; color: var(--matn-3); }
    :host ::ng-deep .kiritish.korsatkich { flex: 1 1 170px; min-width: 0; width: auto; min-height: 46px; padding: 0 18px; border-radius: 23px; font-size: 16px; font-weight: 700; text-align: right; font-variant-numeric: tabular-nums; }
    :host ::ng-deep .kiritish.korsatkich.xato { background: #FFF5F5; border: 1.5px solid #D7262B; }
    .jami { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 14px 16px; border-radius: 18px; background: var(--malumot-fon); }
    .jami-matn { display: flex; flex-direction: column; gap: 2px; }
    .jami-matn b { font-size: 13.5px; font-weight: 700; }
    .jami-matn span { font-size: 12.5px; color: var(--matn-3); }
    .jami-summa { font-size: 22px; font-weight: 800; white-space: nowrap; }
  `,
})
export class NarxDialog {
  protected readonly til = inject(Til);
  private readonly x = inject(SozlamalarXizmati);
  private readonly bildirish = inject(Bildirish);

  readonly ochiq = model(false);
  readonly yoqilgi = input<YoqilgiTuriDto | null>(null);
  readonly saqlandi = output<void>();

  protected readonly pul = pul;
  protected readonly litr = litr;
  protected readonly yangiNarx = signal<number | null>(null);
  private readonly qiymatlar = signal<Record<number, number | null>>({});
  protected readonly band = signal(false);
  private readonly tasdiq = viewChild(PultTasdiq);
  protected readonly xato = signal<string | null>(null);

  /** Ochiq smena (dialog ochilganda yangilanadi). */
  protected readonly smenaId = computed(() => this.x.joriySmena()?.smena.id ?? null);

  protected readonly qatorlar = computed<AparatQatori[]>(() => {
    const y = this.yoqilgi();
    const smena = this.x.joriySmena();
    if (!y || !smena) return [];
    const q = this.qiymatlar();
    return this.x.aparatlar().filter((a) => a.yoqilgiTuriId === y.id).map((a): AparatQatori => {
      // Oldingi ko'rsatkich — Savdo kartasi va yopishdagi bilan bir manba (§8.13)
      const { qiymat: oldingi, segment } = joriyKorsatkich(a, smena.korsatkichlar);
      const qiymat = q[a.id] ?? null;
      const krug = qiymat != null ? pultJamiga(qiymat, oldingi, a.krugHajmi) : null;
      const jami = krug && !krug.xato ? krug.jami : null;
      const asos = { aparat: a, oldingi, segmentBor: !!segment, qiymat, jami, krug };
      if (qiymat == null) return { ...asos, holat: 'bosh', litr: 0, summa: 0, ogoh: null };
      // Yangi ko'rsatkich oldingisidan kichik (krugli aparatda — pult `0 ≤ p < H` dan tashqarida) bo'lsa — rad; teng bo'lsa (0 L sotilgan) — xato emas.
      if (krug!.xato || (jami != null && litrSent(jami) < litrSent(oldingi))) return { ...asos, holat: 'xato', litr: 0, summa: 0, ogoh: null };
      const l = segmentLitri(oldingi, jami!);
      return { ...asos, holat: 'ok', litr: l, summa: segmentSummasi(l, y.narx), ogoh: bakOgohi(oldingi, jami, a.bakQoldiq, narxSegmentlari(smena.korsatkichlar, a.id)) };
    });
  });
  protected readonly hammasiTayyor = computed(() => this.qatorlar().length > 0 && this.qatorlar().every((q) => q.holat === 'ok'));
  protected readonly jamiLitr = computed(() => sentLitr(this.qatorlar().reduce((s, q) => s + litrSent(q.litr), 0)));
  protected readonly jamiSumma = computed(() => this.qatorlar().reduce((s, q) => s + q.summa, 0));
  protected readonly jamiMatn = computed(() =>
    this.hammasiTayyor() ? this.til.t('Narx_JamiLitr', litr(this.jamiLitr()), pul(this.yoqilgi()?.narx ?? 0)) : this.til.t('Narx_KorsatkichlarniTogri'));

  /** Narx haqiqatan o'zgargan va (ochiq smenada) hamma ko'rsatkich to'g'ri. */
  /** Krug oshgan aparatlar — "Pult krugi oshdi" tasdig'i (docs §8.16). */
  protected readonly krugQatorlari = computed<KrugQatori[]>(() =>
    this.qatorlar().filter((q) => q.krug?.oshdi && q.holat === 'ok').map((q) => ({ raqam: q.aparat.raqam, krugdan: q.krug!.krug! - 1, krugga: q.krug!.krug!, sotilgan: q.litr })));
  protected readonly pultMatni = (jami: number, h: number | null | undefined) => litr(krugKorinish(jami, h).pult);

  /** Bakdagidan ko'p litr chiqayotgan aparatlar — "Baribir saqlash" tasdig'i uchun. */
  protected readonly bakQatorlari = computed<BakOgohQatori[]>(() =>
    this.qatorlar().filter((q) => q.ogoh).map((q) => ({ raqam: q.aparat.raqam, chiqadi: q.ogoh!.chiqadi, bakda: q.ogoh!.bakda })));

  protected readonly saqlash = computed(() => {
    const y = this.yoqilgi();
    const n = this.yangiNarx();
    if (!y || !n || n <= 0 || n === y.narx) return false;
    return !this.smenaId() || this.qatorlar().length === 0 || this.hammasiTayyor();
  });

  constructor() {
    effect(() => {
      if (!this.ochiq()) return;
      untracked(() => {
        this.yangiNarx.set(null);
        this.qiymatlar.set({});
        this.xato.set(null);
        this.x.smenaniYukla().catch(() => undefined);
      });
    });
  }

  protected qiymatYoz(aparatId: number, v: number | null) {
    this.qiymatlar.update((o) => ({ ...o, [aparatId]: v }));
  }

  protected natijaMatni(q: AparatQatori): string {
    if (q.holat === 'bosh') return this.til.t('Narx_KorsatkichniYozing');
    if (q.holat === 'xato') return this.til.t(q.segmentBor ? 'Narx_OxirgidanKichik' : 'Narx_SmenaBoshidanKichik');
    return this.til.t('Narx_Sotilgan', litr(q.litr), pul(this.yoqilgi()?.narx ?? 0), pul(q.summa));
  }

  /** Saqlash tugmasi: tasdiqlar zanjiri (bak ogohlantirishi, bloklamaydi), so'ng saqlash. */
  protected saqlaBos() {
    if (!this.saqlash()) return;
    this.tasdiq()?.boshla();
  }

  protected async saqla() {
    const y = this.yoqilgi();
    const n = this.yangiNarx();
    if (!y || !n || !this.saqlash()) return;
    const korsatkichlar: AparatKorsatkichDto[] = this.smenaId() ? this.qatorlar().map((q) => ({ aparatId: q.aparat.id, qiymat: q.jami as number })) : [];
    this.xato.set(null);
    this.band.set(true);
    try {
      await this.x.yoqilgiTahrirla(y.id, y.nomi, n, y.rang, korsatkichlar);
      this.ochiq.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
      this.saqlandi.emit();
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
