import { Component, DestroyRef, ElementRef, computed, effect, inject, signal, viewChild } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { Til } from '../../core/til';
import { Auth } from '../../core/auth';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { jonliYangila } from '../../core/malumot';
import { davomiylikSD, harflar, isoKun, ishoraPul, kunQisqa, litr, litrQisqa, pul, soat } from '../../core/format';
import type { AparatDto, BoshqaruvDto, SmenaDto, SmenaTafsilotDto, YoqilgiTuriDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { YoqilgiPill } from '../../ui/belgilar';
import { BakKirimDialog } from '../../ui/dialoglar/bak-kirim-dialog';
import { KpiKarta } from './kpi-karta';

/** Boshqaruv paneli (docs/dizayn/Boshqaruv): KPI, joriy smena, baklar, oxirgi 14 smena grafigi, to'lov turlari, oxirgi yopilgan smenalar. */
@Component({
  selector: 'boshqaruv-sahifa',
  imports: [RouterLink, Ikon, YoqilgiPill, KpiKarta, BakKirimDialog],
  templateUrl: './boshqaruv.html',
  styleUrl: './boshqaruv.scss',
})
export class BoshqaruvSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly server = inject(Server);
  private readonly router = inject(Router);
  private readonly bildirish = inject(Bildirish);

  protected readonly pul = pul;
  protected readonly litr = litr;
  protected readonly litrQisqa = litrQisqa;
  protected readonly harflar = harflar;
  protected readonly ishoraPul = ishoraPul;
  protected readonly kunQisqa = kunQisqa;
  protected readonly soat = soat;

  protected readonly data = signal<BoshqaruvDto | null>(null);
  protected readonly joriyTafsilot = signal<SmenaTafsilotDto | null>(null);
  protected readonly yoqilgilar = signal<YoqilgiTuriDto[]>([]);
  protected readonly yuklandi = signal(false);
  protected readonly yangilanmoqda = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected readonly bakOchiq = signal(false);
  private readonly grafikOram = viewChild<ElementRef<HTMLElement>>('grafikOram');
  /** Davomiylik va sana tirik turishi uchun har yarim daqiqada yangilanadi. */
  private readonly hozir = signal(Date.now());

  /** "Yakshanba, 4-oktabr 2026" — hafta kuni va oy nomlari lug'atdan (3 til). */
  protected readonly sanaMatn = computed(() => {
    this.hozir();
    const d = new Date(isoKun() + 'T00:00:00Z');
    const hk = this.til.t('Boshqaruv_HaftaKunlari').split(',');
    const oy = this.til.t('Boshqaruv_OyNomlari').split(',');
    return this.til.t('Boshqaruv_SanaShakli', hk[d.getUTCDay()], d.getUTCDate(), oy[d.getUTCMonth()], d.getUTCFullYear());
  });

  protected readonly joriy = computed<SmenaDto | null>(() => this.data()?.joriySmena ?? null);
  protected readonly oxirgi = computed<SmenaDto | null>(() => this.data()?.oxirgiYopilgan ?? null);

  /** Oyda ortiqcha chiqqan smenalar soni (oxirgi smenalar ro'yxatidan, joriy oy). */
  protected readonly ortiqchaSmenalar = computed(() => {
    const oy = isoKun().slice(0, 7);
    return (this.data()?.oxirgiSmenalar ?? []).filter((s) => s.sana.startsWith(oy) && s.farq > 0).length;
  });

  protected readonly davomiylik = computed(() => {
    this.hozir();
    const j = this.joriy();
    if (!j) return '';
    const [h, m] = davomiylikSD(j.boshlandi, null);
    return this.davomiylikMatn(h, m);
  });

  /** Oxirgi 14 smena grafigi: balandlik — eng kichik qiymatdan oshib borishi (past chegara = min − 0.85 × oraliq). */
  protected readonly ustunlar = computed(() => {
    const r = [...(this.data()?.oxirgiSmenalar ?? [])].sort((a, b) => a.sana.localeCompare(b.sana) || a.id - b.id).slice(-14);
    if (!r.length) return [];
    const q = r.map((s) => s.savdo / 1e6);
    const eng = Math.max(...q);
    const kam = Math.min(...q);
    const past = Math.max(0, kam - (eng - kam) * 0.85);
    return r.map((s, i) => ({
      qiymat: q[i].toFixed(1),
      sana: kunQisqa(s.sana),
      balandlik: eng > past ? Math.round(30 + ((q[i] - past) / (eng - past)) * 120) : 150,
      oxirgi: i === r.length - 1,
    }));
  });

  /** To'lov turlari (shu oy), qiymat bo'yicha kamayish tartibida; foiz — jami ichidagi ulush. */
  protected readonly tolovlar = computed(() => {
    const t = this.data()?.oyTolovlar;
    if (!t) return [];
    const royxat = [
      { k: 'Plastik', summa: t.plastik, rang: 'var(--q-plastik)' },
      { k: 'Naqd', summa: t.naqd, rang: 'var(--q-naqd)' },
      { k: 'Depozit', summa: t.depozit, rang: 'var(--q-depozit)' },
      { k: 'Nasiya', summa: t.nasiya, rang: 'var(--q-nasiya)' },
    ];
    const jami = royxat.reduce((a, x) => a + Math.max(0, x.summa), 0);
    return royxat.sort((a, b) => b.summa - a.summa).map((x) => ({ ...x, foiz: jami > 0 ? Math.round((Math.max(0, x.summa) / jami) * 100) : 0 }));
  });

  /** Aparat baklari: yoqilg'i rangi bilan. */
  protected readonly baklar = computed(() => {
    const rang = new Map(this.yoqilgilar().map((y) => [y.id, y.rang]));
    return [...(this.data()?.aparatlar ?? [])].sort((a, b) => a.raqam - b.raqam).map((a: AparatDto) => ({ a, rang: rang.get(a.yoqilgiTuriId) ?? '#2563EB' }));
  });

  protected readonly bakVaqti = computed(() => {
    const t = this.oxirgi()?.tugadi;
    return t ? `${kunQisqa(t)} ${soat(t)}` : `${kunQisqa(new Date())} ${soat(new Date())}`;
  });

  constructor() {
    this.yukla();
    // Tor ekranda grafik gorizontal siljiydi — eng yangi smena ko'rinib tursin.
    effect(() => {
      this.ustunlar();
      const el = this.grafikOram()?.nativeElement;
      if (el) queueMicrotask(() => setTimeout(() => (el.scrollLeft = el.scrollWidth)));
    });
    jonliYangila(() => this.yukla(true));
    const taymer = setInterval(() => this.hozir.set(Date.now()), 30000);
    inject(DestroyRef).onDestroy(() => clearInterval(taymer));
  }

  protected async yukla(jim = false) {
    if (!jim) this.yangilanmoqda.set(true);
    try {
      const [d, j, y] = await Promise.all([this.server.boshqaruv(), this.server.joriySmena(), this.server.yoqilgilar()]);
      this.data.set(d);
      this.joriyTafsilot.set(j);
      this.yoqilgilar.set(y);
      this.xato.set(null);
    } catch (e) {
      if (!jim) {
        const m = xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
        if (this.data()) this.bildirish.korsat(m, true); else this.xato.set(m);
      }
    } finally {
      this.yuklandi.set(true);
      this.yangilanmoqda.set(false);
    }
  }

  protected davomiylikMatn(h: number, m: number): string {
    if (h <= 0) return this.til.t('Boshqaruv_Daqiqa', m);
    return m > 0 ? this.til.t('Boshqaruv_SoatDaqiqa', h, m) : this.til.t('Boshqaruv_Soat', h);
  }

  /** Yopilgan smenaning vaqti: "03.10 · 24 soat". */
  protected smenaVaqti(s: SmenaDto): string {
    const [h, m] = davomiylikSD(s.boshlandi, s.tugadi);
    return `${kunQisqa(s.boshlandi)} · ${this.til.t('Boshqaruv_Soat', h + (m >= 30 ? 1 : 0))}`;
  }

  protected farqMatn(f: number): string {
    return f === 0 ? this.til.t('Boshqaruv_FarqYoq') : `${this.til.t(f < 0 ? 'Kamomat' : 'Ortiqcha')} ${ishoraPul(f)}`;
  }
  protected farqRang(f: number): string { return f === 0 ? 'yashil' : f < 0 ? 'qizil' : 'kok'; }

  protected smenaOch(id: number) {
    if (this.auth.bor('Smenalar')) this.router.navigate(['/smenalar'], { queryParams: { smena: id } });
  }
}
