import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Auth } from '../../core/auth';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { jonliYangila } from '../../core/malumot';
import { davomiylikSD, ishoraPul, kunQisqa, litr, pul, soat } from '../../core/format';
import { segmentLitri, segmentSummasi, smenaHisobla } from '../../core/hisob';
import type { AparatDto, SmenaKorsatkichDto, SmenaTafsilotDto, YoqilgiTuriDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { Oyna } from '../../ui/oyna';
import { YoqilgiPill } from '../../ui/belgilar';
import { SonKiritish } from '../../ui/son-kiritish';
import { EnterKeyingi } from '../../ui/enter-keyingi';
import { NasiyaDialog } from '../../ui/dialoglar/nasiya-dialog';
import { QarzQaytdiDialog } from '../../ui/dialoglar/qarz-qaytdi-dialog';
import { XarajatDialog } from '../../ui/dialoglar/xarajat-dialog';

interface Qator {
  aparat: AparatDto;
  rang: string;
  narx: number;
  /** "Oldingi": ochiq smenada qayd etilgan oxirgi segment `oxiri`, aks holda `AparatDto.totalLitr` (docs §7.1). */
  oldingi: number;
  /** Narx o'zgarishida qayd etilgan (eski narxdagi) segmentlar. */
  segmentlar: SmenaKorsatkichDto[];
  yangi: number | null;
  xato: boolean;
  sotilgan: number | null;
  summa: number | null;
}

/**
 * Smenani yopish (docs/dizayn/SmenaYopish): har aparat uchun yangi pult ko'rsatkichi, yopishdagi terminal va depozit, sanalgan naqd.
 * Kutilgan naqd va kamomat jonli hisoblanadi (core/hisob.ts — server formulasi bilan bir xil); telefonda har aparat — alohida karta.
 */
@Component({
  selector: 'smena-yopish-sahifa',
  imports: [RouterLink, FormsModule, Ikon, Oyna, YoqilgiPill, SonKiritish, EnterKeyingi, NasiyaDialog, QarzQaytdiDialog, XarajatDialog],
  templateUrl: './smena-yopish.html',
  styleUrl: './smena-yopish.scss',
})
export class SmenaYopishSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);
  private readonly router = inject(Router);

  protected readonly pul = pul;
  protected readonly litr = litr;
  protected readonly ishora = ishoraPul;

  protected readonly yuklandi = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected readonly tafsilot = signal<SmenaTafsilotDto | null>(null);
  private readonly aparatlar = signal<AparatDto[]>([]);
  private readonly yoqilgilar = signal<YoqilgiTuriDto[]>([]);

  // Kiritiladigan qiymatlar
  protected readonly yangi = signal<Record<number, number | null>>({});
  protected readonly terminal = signal<number | null>(null);
  protected readonly depozit = signal<number | null>(null);
  protected readonly naqd = signal<number | null>(null);
  protected izoh = '';

  protected readonly nasiyaOchiq = signal(false);
  protected readonly qaytishOchiq = signal(false);
  protected readonly xarajatOchiq = signal(false);
  protected readonly tasdiqOchiq = signal(false);
  protected readonly band = signal(false);
  protected readonly yopishXato = signal<string | null>(null);

  private readonly tik = signal(Date.now());

  protected readonly qatorlar = computed<Qator[]>(() => {
    const t = this.tafsilot();
    if (!t) return [];
    const yangi = this.yangi();
    return [...this.aparatlar()].sort((a, b) => a.raqam - b.raqam).map((a) => {
      const yoq = this.yoqilgilar().find((y) => y.id === a.yoqilgiTuriId);
      const segmentlar = t.korsatkichlar.filter((k) => k.aparatId === a.id && k.narxOzgarishida);
      const oldingi = segmentlar.length ? segmentlar[segmentlar.length - 1].oxiri : a.totalLitr;
      const narx = yoq?.narx ?? 0;
      const y = yangi[a.id] ?? null;
      const xato = y != null && y < oldingi;
      const sotilgan = y != null && !xato ? segmentLitri(oldingi, y) : null;
      return { aparat: a, rang: yoq?.rang ?? '#2563EB', narx, oldingi, segmentlar, yangi: y, xato, sotilgan, summa: sotilgan != null ? segmentSummasi(sotilgan, narx) : null };
    });
  });
  protected readonly tayyorAparatlar = computed(() => this.qatorlar().length > 0 && this.qatorlar().every((r) => r.sotilgan != null));
  protected readonly jamiLitr = computed(() => Math.round(this.qatorlar().reduce((s, r) => s + r.segmentlar.reduce((x, g) => x + Math.round(g.litr * 100), 0) + Math.round((r.sotilgan ?? 0) * 100), 0)) / 100);
  protected readonly jamiSavdo = computed(() => this.qatorlar().reduce((s, r) => s + r.segmentlar.reduce((x, g) => x + g.summa, 0) + (r.summa ?? 0), 0));
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
      this.tafsilot.set(t); this.aparatlar.set(ap); this.yoqilgilar.set(yo);
      this.xato.set(null);
    } catch (e) {
      if (!jim) this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.yuklandi.set(true);
    }
  }

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
  protected ayirma(n: number): string { return n === 0 ? '0' : n > 0 ? '−' + pul(n) : '+' + pul(-n); }

  async yop() {
    const t = this.tafsilot();
    const h = this.hisob();
    if (!t || !h || !this.yopsaBoladi()) return;
    this.yopishXato.set(null);
    this.band.set(true);
    try {
      await this.server.smenaYop(t.smena.id, {
        korsatkichlar: this.qatorlar().map((r) => ({ aparatId: r.aparat.id, qiymat: r.yangi! })),
        terminal: this.terminal()!, depozit: this.depozit()!, sanalganNaqd: this.naqd()!, izoh: this.izoh.trim() || null,
      });
      this.tasdiqOchiq.set(false);
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
