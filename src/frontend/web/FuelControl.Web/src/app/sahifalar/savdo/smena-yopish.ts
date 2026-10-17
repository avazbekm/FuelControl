import { Component, DestroyRef, computed, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Auth } from '../../core/auth';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { jonliYangila } from '../../core/malumot';
import { YopishQoralama } from '../../core/yopish-qoralama';
import { davomiylikSD, ishoraPul, kunQisqa, litr, pul, soat } from '../../core/format';
import { BakOgohi, PultNatijasi, bakOgohi, berilganNasiyalar, joriyKorsatkich, krugKorinish, narxSegmentlari, pultJamiga, segmentLitri, segmentSummasi, smenaHisobla } from '../../core/hisob';
import type { AparatDto, SmenaKorsatkichDto, SmenaTafsilotDto, YoqilgiTuriDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { Oyna } from '../../ui/oyna';
import { YoqilgiPill } from '../../ui/belgilar';
import { SonKiritish } from '../../ui/son-kiritish';
import { EnterKeyingi } from '../../ui/enter-keyingi';
import { BakOgohMatn, BakOgohQatori } from '../../ui/bak-ogoh';
import { PultTasdiq } from '../../ui/pult-tasdiq';
import { KrugIzoh, KrugQatori, PultQiymat } from '../../ui/krug';
import { Sigdir } from '../../ui/sigdir';
import { NasiyaDialog } from '../../ui/dialoglar/nasiya-dialog';
import { QarzQaytdiDialog } from '../../ui/dialoglar/qarz-qaytdi-dialog';
import { NarxKorsatkichDialog } from '../../ui/dialoglar/narx-korsatkich-dialog';
import { XarajatDialog } from '../../ui/dialoglar/xarajat-dialog';

interface Qator {
  aparat: AparatDto;
  rang: string;
  narx: number;
  /** "Oldingi": ochiq smenada qayd etilgan oxirgi segment `oxiri`, aks holda `AparatDto.totalLitr` (docs §7.1, §8.13 — Savdo kartasi bilan bir manba). */
  oldingi: number;
  /** Narx o'zgarishida qayd etilgan (eski narxdagi) segmentlar. */
  segmentlar: SmenaKorsatkichDto[];
  /** Yangi ko'rsatkich — UMUMIY litr (API qiymati); pult krugli aparatda kiritilgan pult qiymatidan o'giriladi (docs §8.16). */
  yangi: number | null;
  /** Kiritilgan pult qiymatining krug natijasi (krugsiz aparatda ham; `oshdi` faqat krugli). */
  krug: PultNatijasi | null;
  xato: boolean;
  /** Bakdagidan ko'p litr chiqayotgan bo'lsa (bloklamaydi): shu smenadagi jami litr (segmentlar + shu qator) > `BakQoldiq` (docs §8.15). */
  ogoh: BakOgohi | null;
  sotilgan: number | null;
  summa: number | null;
}

/**
 * Smenani yopish (docs/dizayn/SmenaYopish): har aparat uchun yangi pult ko'rsatkichi, yopishdagi terminal va depozit, sanalgan naqd.
 * Kutilgan naqd va kamomat jonli hisoblanadi (core/hisob.ts — server formulasi bilan bir xil); telefonda har aparat — alohida karta.
 */
@Component({
  selector: 'smena-yopish-sahifa',
  imports: [RouterLink, FormsModule, Ikon, Oyna, YoqilgiPill, SonKiritish, EnterKeyingi, NasiyaDialog, QarzQaytdiDialog, XarajatDialog, BakOgohMatn, PultTasdiq, NarxKorsatkichDialog, PultQiymat, KrugIzoh, Sigdir],
  templateUrl: './smena-yopish.html',
  styleUrl: './smena-yopish.scss',
})
export class SmenaYopishSahifa {
  /** "Nasiya berildi" ro'yxati: eski qarz nasiyalarisiz (docs §8.11). */
  protected readonly oddiy = berilganNasiyalar;
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);
  private readonly router = inject(Router);
  private readonly qoralama = inject(YopishQoralama);

  protected readonly pul = pul;
  protected readonly litr = litr;
  protected readonly ishora = ishoraPul;

  protected readonly yuklandi = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected readonly tafsilot = signal<SmenaTafsilotDto | null>(null);
  private readonly aparatlar = signal<AparatDto[]>([]);
  private readonly yoqilgilar = signal<YoqilgiTuriDto[]>([]);

  // Kiritiladigan qiymatlar — qoralama xizmatida (sahifadan chiqib-qaytganda saqlanadi, smena almashganda tozalanadi)
  protected readonly yangi = this.qoralama.yangi;
  protected readonly plastik = this.qoralama.plastik;
  protected readonly depozit = this.qoralama.depozit;
  protected readonly naqd = this.qoralama.naqd;
  protected readonly izoh = this.qoralama.izoh;

  protected readonly nasiyaOchiq = signal(false);
  protected readonly qaytishOchiq = signal(false);
  protected readonly xarajatOchiq = signal(false);
  protected readonly tasdiqOchiq = signal(false);
  private readonly tasdiq = viewChild(PultTasdiq);
  /** "Tuzatish" havolasi bosilgan aparat (narx o'zgarishidagi ko'rsatkichni tuzatish, 14-band). */
  protected readonly tuzatishOchiq = signal(false);
  private readonly tuzatishId = signal<number | null>(null);
  protected readonly tuzatishAparat = computed(() => this.aparatlar().find((a) => a.id === this.tuzatishId()) ?? null);
  protected tuzatishOch(a: AparatDto) { this.tuzatishId.set(a.id); this.tuzatishOchiq.set(true); }
  protected readonly band = signal(false);
  protected readonly yopishXato = signal<string | null>(null);

  private readonly tik = signal(Date.now());

  protected readonly qatorlar = computed<Qator[]>(() => {
    const t = this.tafsilot();
    if (!t) return [];
    const yangi = this.yangi();
    return [...this.aparatlar()].sort((a, b) => a.raqam - b.raqam).map((a) => {
      const yoq = this.yoqilgilar().find((y) => y.id === a.yoqilgiTuriId);
      const segmentlar = narxSegmentlari(t.korsatkichlar, a.id);
      const oldingi = joriyKorsatkich(a, t.korsatkichlar).qiymat;
      const narx = yoq?.narx ?? 0;
      const p = yangi[a.id] ?? null;   // operator yozgan pult qiymati
      const krug = p != null ? pultJamiga(p, oldingi, a.krugHajmi) : null;
      const jami = krug && !krug.xato ? krug.jami : null;
      const xato = p != null && (!!krug?.xato || (jami != null && jami < oldingi));
      const y = xato ? null : jami;
      const sotilgan = y != null ? segmentLitri(oldingi, y) : null;
      return { aparat: a, rang: yoq?.rang ?? '#2563EB', narx, oldingi, segmentlar, yangi: y, krug, xato, ogoh: bakOgohi(oldingi, y, a.bakQoldiq, segmentlar), sotilgan, summa: sotilgan != null ? segmentSummasi(sotilgan, narx) : null };
    });
  });
  /** Bakdagidan ko'p litr chiqayotgan aparatlar — yopishdan oldingi tasdiq ro'yxati. */
  protected readonly bakQatorlari = computed<BakOgohQatori[]>(() =>
    this.qatorlar().filter((r) => r.ogoh).map((r) => ({ raqam: r.aparat.raqam, chiqadi: r.ogoh!.chiqadi, bakda: r.ogoh!.bakda })));
  /** Krug oshgan aparatlar — yopishdan oldingi "Pult krugi oshdi" tasdig'i (docs §8.16). */
  protected readonly krugQatorlari = computed<KrugQatori[]>(() =>
    this.qatorlar().filter((r) => r.krug?.oshdi && r.sotilgan != null).map((r) => ({ raqam: r.aparat.raqam, krugdan: r.krug!.krug! - 1, krugga: r.krug!.krug!, sotilgan: r.sotilgan! })));
  /** Pult qiymati (krugli aparatda krugsiz qism) — placeholder uchun. */
  protected readonly pultMatni = (jami: number, h: number | null | undefined) => litr(krugKorinish(jami, h).pult);
  protected readonly tayyorAparatlar = computed(() => this.qatorlar().length > 0 && this.qatorlar().every((r) => r.sotilgan != null));
  protected readonly jamiLitr = computed(() => Math.round(this.qatorlar().reduce((s, r) => s + r.segmentlar.reduce((x, g) => x + Math.round(g.litr * 100), 0) + Math.round((r.sotilgan ?? 0) * 100), 0)) / 100);
  protected readonly jamiSavdo = computed(() => this.qatorlar().reduce((s, r) => s + r.segmentlar.reduce((x, g) => x + g.summa, 0) + (r.summa ?? 0), 0));
  /** Server cheklovi: bitta smenada 20 tagacha plastik summa. */
  protected readonly PLASTIK_MAX = 20;
  /** Qiymatli qatorlar (0 ham qiymat); bo'sh qatorlar hisobga olinmaydi. */
  protected readonly plastikQiymatlari = computed(() => this.plastik().filter((x): x is number => x != null));
  /** Yopishdagi terminal = plastik qatorlari yig'indisi; hech bo'lmasa bitta qiymat kerak, aks holda `null`. */
  protected readonly terminal = computed<number | null>(() => {
    const q = this.plastikQiymatlari();
    return q.length ? q.reduce((a, x) => a + x, 0) : null;
  });
  protected readonly plastikSmena = computed(() => (this.terminal() != null && this.tafsilot() ? this.terminal()! - this.tafsilot()!.smena.ochishTerminal : null));
  protected readonly depozitFarqi = computed(() => (this.depozit() != null && this.tafsilot() ? this.depozit()! - this.tafsilot()!.smena.ochishDepozit : null));
  protected readonly plastikKam = computed(() => (this.plastikSmena() ?? 0) < 0);
  protected readonly depozitManfiy = computed(() => (this.depozitFarqi() ?? 0) < 0);

  /** Server formulasi bilan bir xil hisob; aparatlar, terminal va depozit to'liq bo'lmaguncha `null`. */
  protected readonly hisob = computed(() => {
    const t = this.tafsilot();
    if (!t || !this.tayyorAparatlar() || this.terminal() == null || this.depozit() == null) return null;
    const segmentlar = this.qatorlar().flatMap((r) => [
      ...r.segmentlar.map((g) => ({ boshi: g.boshi, oxiri: g.oxiri, narx: g.narx })),
      { boshi: r.oldingi, oxiri: r.yangi!, narx: r.narx },
    ]);
    return smenaHisobla({
      qaytim: t.smena.ochishQaytim, ochishTerminal: t.smena.ochishTerminal, ochishDepozit: t.smena.ochishDepozit, segmentlar,
      yopishTerminal: this.terminal()!, yopishDepozit: this.depozit()!, nasiyaJami: t.smena.nasiyaJami, qaytganNasiya: t.smena.qaytganNasiya,
      xarajatJami: t.smena.xarajatJami, sanalganNaqd: this.naqd(),
    });
  });
  /** Hisob chiqmasa — nima yetishmaydi (desktop bilan bir xil): har noto'g'ri/bo'sh aparat qatori, terminal va depozit alohida. */
  protected readonly sabablar = computed<string[]>(() => {
    if (!this.tafsilot() || this.hisob()) return [];
    const l: string[] = [];
    for (const r of this.qatorlar()) {
      const nomi = this.til.t('Aparat_Raqami', r.aparat.raqam);
      if (r.krug?.xato) l.push(`${nomi}: ${this.til.t('Krug_XatoKatta', litr(r.aparat.krugHajmi ?? 0))}`);
      else if (r.xato) l.push(this.til.t('Yopish_SababKichik', nomi, litr(r.oldingi)));
      else if (r.yangi == null) l.push(this.til.t('Yopish_SababBosh', nomi));
    }
    if (this.terminal() == null) l.push(this.til.t('Yopish_SababTerminal'));
    if (this.depozit() == null) l.push(this.til.t('Yopish_SababDepozit'));
    return l;
  });

  /**
   * Faqat ayrim aparat qatorlari yetishmasa (terminal va depozit kiritilgan): to'g'ri kiritilgan qatorlar bo'yicha taxminiy "kassada bo'lishi kerak"
   * va qaysi aparatlar hisobga olinmagani. Yopish baribir to'liq hisobni talab qiladi.
   */
  protected readonly taxminiy = computed<string | null>(() => {
    const t = this.tafsilot();
    if (!t || this.hisob() || this.terminal() == null || this.depozit() == null) return null;
    const qatorlar = this.qatorlar();
    const tayyor = qatorlar.filter((r) => r.sotilgan != null);
    if (!tayyor.length) return null;
    const h = smenaHisobla({
      qaytim: t.smena.ochishQaytim, ochishTerminal: t.smena.ochishTerminal, ochishDepozit: t.smena.ochishDepozit,
      segmentlar: tayyor.flatMap((r) => [...r.segmentlar.map((g) => ({ boshi: g.boshi, oxiri: g.oxiri, narx: g.narx })), { boshi: r.oldingi, oxiri: r.yangi!, narx: r.narx }]),
      yopishTerminal: this.terminal()!, yopishDepozit: this.depozit()!, nasiyaJami: t.smena.nasiyaJami, qaytganNasiya: t.smena.qaytganNasiya,
      xarajatJami: t.smena.xarajatJami, sanalganNaqd: null,
    });
    const qolgan = qatorlar.filter((r) => r.sotilgan == null).map((r) => this.til.t('Aparat_Raqami', r.aparat.raqam)).join(', ');
    return this.til.t('Yopish_Taxminiy', pul(h.kutilgan), qolgan);
  });

  protected readonly holat = computed<'toliqEmas' | 'naqd' | 'kamomat' | 'ortiqcha' | 'teng'>(() => {
    const h = this.hisob();
    if (!h) return 'toliqEmas';
    if (h.farq == null) return 'naqd';
    return h.farq < 0 ? 'kamomat' : h.farq > 0 ? 'ortiqcha' : 'teng';
  });
  protected readonly yopsaBoladi = computed(() => this.holat() === 'kamomat' || this.holat() === 'ortiqcha' || this.holat() === 'teng');

  constructor() {
    this.yukla();
    jonliYangila(() => this.yukla(true));
    const t = setInterval(() => this.tik.set(Date.now()), 30000);
    inject(DestroyRef).onDestroy(() => clearInterval(t));
  }

  async yukla(jim = false) {
    if (!jim) this.yuklandi.set(false);
    try {
      const [t, ap, yo] = await Promise.all([this.server.joriySmena(), this.server.aparatlar(), this.server.yoqilgilar()]);
      if (!t) { this.router.navigateByUrl('/savdo'); return; }
      // Smena Id o'zgargan (yangi smena, boshqa qurilmada ochilgan ham): forma, xato va tasdiq oynasi to'liq tozalanadi.
      if (this.qoralama.smenaga(t.smena.id)) { this.yopishXato.set(null); this.tasdiqOchiq.set(false); }
      this.tafsilot.set(t); this.aparatlar.set(ap); this.yoqilgilar.set(yo);
      this.xato.set(null);
    } catch (e) {
      if (!jim) this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.yuklandi.set(true);
    }
  }

  protected plastikId(i: number): string { return i === 0 ? 'plastik-k' : `plastik-k-${i}`; }
  protected plastikOzgar(i: number, v: number | null) { this.plastik.update((a) => a.map((x, k) => (k === i ? v : x))); }
  /** Yangi qator qo'shadi va unga fokus beradi. */
  protected plastikQosh() {
    if (this.plastik().length >= this.PLASTIK_MAX) return;
    this.plastik.update((a) => [...a, null]);
    const i = this.plastik().length - 1;
    setTimeout(() => document.getElementById(this.plastikId(i))?.focus(), 60);
  }
  /** Qatorni olib tashlaydi; oxirgi qator qolsa — uni bo'shatadi. */
  protected plastikOchir(i: number) { this.plastik.update((a) => (a.length <= 1 ? [null] : a.filter((_, k) => k !== i))); }

  protected yangiOzgar(aparatId: number, v: number | null) {
    this.yangi.update((m) => ({ ...m, [aparatId]: v }));
  }

  protected kunSoatQisqa(iso: string): string { return `${kunQisqa(iso)} ${soat(iso)}`; }
  protected davomiylik(boshi: string): string {
    this.tik();
    const [h, m] = davomiylikSD(boshi, null);
    return this.til.t('Savdo_SoatDaqiqa', h, m);
  }
  /** Kassa hisobi: chiqim "−", kirim "+" (dizayndagidek). */
  protected ayirma(n: number): string { const r = Math.round(n); return r === 0 ? '0' : r > 0 ? '−' + pul(r) : '+' + pul(-r); }
  /** Kirim: "+1 000"; nol (yoki yaxlitlanganda nol) — ishorasiz "0". */
  protected qosh(n: number): string { const r = Math.round(n); return r === 0 ? '0' : r > 0 ? '+' + pul(r) : pul(r); }

  /**
   * "Smenani yopish" tugmasi: avval tasdiqlar zanjiri (bakdagidan ko'p litr chiqsa bak tasdig'i), keyin odatdagi "Smenani yopasizmi?".
   */
  protected yopishBos() { this.tasdiq()?.boshla(); }

  async yop() {
    const t = this.tafsilot();
    const h = this.hisob();
    if (!t || !h || !this.yopsaBoladi()) return;
    this.yopishXato.set(null);
    this.band.set(true);
    try {
      await this.server.smenaYop(t.smena.id, {
        korsatkichlar: this.qatorlar().map((r) => ({ aparatId: r.aparat.id, qiymat: r.yangi! })),
        terminal: this.terminal()!, plastikSummalari: this.plastikQiymatlari(), depozit: this.depozit()!, sanalganNaqd: this.naqd()!, izoh: this.izoh().trim() || null,
      });
      this.tasdiqOchiq.set(false);
      this.qoralama.smenaga(null);
      this.bildirish.korsat(this.til.t('Yopish_Yopildi'));
      this.router.navigateByUrl('/savdo');
    } catch (e) {
      this.tasdiqOchiq.set(false);
      this.yopishXato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
