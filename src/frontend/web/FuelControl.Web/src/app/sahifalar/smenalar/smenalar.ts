import { Component, DestroyRef, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';
import { Auth } from '../../core/auth';
import { Til } from '../../core/til';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { Malumot, OperatorElement, jonliYangila } from '../../core/malumot';
import { davomiylikSD, isoKun, ishoraPul, kunQisqa, kunQosh, litr, oyBoshi, oyOxiri, oyQosh, pul, sana, soat } from '../../core/format';
import type { NasiyaDto, NasiyaQaytishiDto, SmenaDto, SmenaKorsatkichDto, SmenaTafsilotDto, XarajatDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { smenaExcel } from './smena-excel';
import { TuzatishDialog } from './tuzatish-dialog';

type Davr = '7kun' | 'shuoy' | 'otganoy';

/** Aparat bo'yicha guruhlangan ko'rsatkichlar (narx o'zgarganda aparatda bir nechta segment bo'ladi). */
interface AparatQatori { aparatId: number; raqam: number; yoqilgi: string; segmentlar: SmenaKorsatkichDto[]; litr: number; summa: number }
interface HisobSatri { yorliq: string; qiymat: string; qalin?: boolean; ustChiziq?: boolean }

const HODISALAR = ['SmenaOzgardi', 'NasiyaOzgardi', 'XarajatOzgardi', 'AparatOzgardi'];

/**
 * Smenalar (docs/dizayn/Smenalar): davr/operator filtri, KPI, smenalar ro'yxati va tanlangan smena tafsiloti
 * (aparat ko'rsatkichlari, pul hisobi, nasiya/xarajat yozuvlari, ko'rsatkichni tuzatish, Excel).
 * Telefonda ro'yxat va tafsilot alohida ko'rinish (`?smena=ID`), keng ekranda yonma-yon.
 */
@Component({
  selector: 'smenalar-sahifa',
  imports: [Ikon, TuzatishDialog],
  templateUrl: './smenalar.html',
  styleUrl: './smenalar.scss',
})
export class SmenalarSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly server = inject(Server);
  private readonly router = inject(Router);
  private readonly bildirish = inject(Bildirish);
  private readonly malumot = inject(Malumot);

  /** `?smena=41` (component input binding). */
  readonly smena = input<string | undefined>();

  protected readonly davrlar: { kod: Davr; kalit: string }[] = [
    { kod: '7kun', kalit: 'Smenalar_Davr7Kun' }, { kod: 'shuoy', kalit: 'Smenalar_DavrShuOy' }, { kod: 'otganoy', kalit: 'Smenalar_DavrOtganOy' },
  ];
  protected readonly davr = signal<Davr>('shuoy');
  protected readonly operatorId = signal<number | null>(null);
  protected readonly operatorlar = signal<OperatorElement[]>([]);
  protected readonly smenalar = signal<SmenaDto[]>([]);
  protected readonly joriy = signal<SmenaTafsilotDto | null>(null);
  protected readonly oxirgiYopilganId = signal<number | null>(null);
  protected readonly tafsilot = signal<SmenaTafsilotDto | null>(null);
  protected readonly yuklandi = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected readonly tafsilotYuklanmoqda = signal(false);
  protected readonly tuzatishOchiq = signal(false);
  protected readonly eksportBand = signal(false);
  /** O'chirishni tasdiqlash: "nasiya:5" | "xarajat:3" | "qaytish:2". */
  protected readonly ochirTasdiq = signal<string | null>(null);

  protected readonly pul = pul;
  protected readonly litr = litr;
  protected readonly ishoraPul = ishoraPul;

  /** 700px dan tor ekran: ro'yxat va tafsilot alohida ko'rinish. */
  private readonly mq = matchMedia('(max-width: 699px)');
  protected readonly tor = signal(this.mq.matches);

  protected readonly tanlanganId = computed<number | null>(() => {
    const p = Number(this.smena());
    if (Number.isInteger(p) && p > 0) return p;
    if (this.tor()) return null;
    const royxat = this.smenalar();
    return (royxat.find((s) => s.tugadi) ?? royxat[0])?.id ?? null;
  });
  /** Telefonda tafsilot ochiq: ro'yxat yashiriladi. */
  protected readonly faqatTafsilot = computed(() => this.tor() && this.tanlanganId() != null);

  protected readonly oraliq = computed<[string, string]>(() => {
    const b = isoKun();
    switch (this.davr()) {
      case '7kun': return [kunQosh(b, -6), b];
      case 'otganoy': { const o = oyQosh(b, -1); return [o, oyOxiri(o)]; }
      default: return [oyBoshi(b), b];
    }
  });
  protected readonly oyNomi = computed(() => {
    const [dan] = this.oraliq();
    return this.til.t('OyNomlari').split(',')[+dan.slice(5, 7) - 1] ?? '';
  });
  protected readonly royxatSarlavha = computed(() =>
    this.davr() === '7kun' ? this.til.t('Smenalar_Royxat7Kun') : this.til.t('Smenalar_RoyxatOy', this.oyNomi()));

  protected readonly joriySarlavhaIzoh = computed(() => {
    const j = this.joriy()?.smena;
    return j ? this.til.t('Smenalar_Joriy', j.id, j.operatorIsmi, this.kunSoat(j.boshlandi)) : this.til.t('Smenalar_JoriyYoq');
  });
  protected readonly ochiqIzoh = computed(() => {
    const j = this.joriy()?.smena;
    return j ? `${j.operatorIsmi} · ${this.davomiylik(j.boshlandi, null)}` : this.til.t('Smenalar_OchiqYoq');
  });
  protected readonly yopilganlar = computed(() => this.smenalar().filter((s) => s.tugadi));
  protected readonly ochiqSoni = computed(() => this.smenalar().filter((s) => !s.tugadi).length);
  protected readonly kamomat = computed(() => this.yopilganlar().filter((s) => s.farq < 0).reduce((a, s) => a - s.farq, 0));
  protected readonly ortiqcha = computed(() => this.yopilganlar().filter((s) => s.farq > 0).reduce((a, s) => a + s.farq, 0));
  protected readonly kpiSoniKalit = computed(() => ({ '7kun': 'Smenalar_KpiSoni7Kun', shuoy: 'Smenalar_KpiSoniShuOy', otganoy: 'Smenalar_KpiSoniOtganOy' })[this.davr()]);
  protected readonly kpiKamomatKalit = computed(() => ({ '7kun': 'Smenalar_KpiKamomat7Kun', shuoy: 'Smenalar_KpiKamomatShuOy', otganoy: 'Smenalar_KpiKamomatOtganOy' })[this.davr()]);

  // ---- Tafsilot hisoblari
  protected readonly s = computed(() => this.tafsilot()?.smena ?? null);
  protected readonly ochiq = computed(() => { const s = this.s(); return !!s && !s.tugadi; });
  protected readonly aparatQatorlari = computed<AparatQatori[]>(() => {
    const t = this.tafsilot();
    if (!t) return [];
    const m = new Map<number, AparatQatori>();
    for (const k of t.korsatkichlar) {
      let q = m.get(k.aparatId);
      if (!q) { q = { aparatId: k.aparatId, raqam: k.aparatRaqam, yoqilgi: k.yoqilgiNomi, segmentlar: [], litr: 0, summa: 0 }; m.set(k.aparatId, q); }
      q.segmentlar.push(k);
      q.litr = Math.round((q.litr + k.litr) * 100) / 100;
      q.summa += k.summa;
    }
    return [...m.values()].sort((a, b) => a.raqam - b.raqam);
  });
  protected readonly tuzatishMumkin = computed(() => {
    const s = this.s();
    return !!s && !!s.tugadi && this.auth.bor('KorsatkichTuzatish') && s.id === this.oxirgiYopilganId();
  });
  protected readonly hisobSatrlari = computed<HisobSatri[]>(() => {
    const t = this.tafsilot();
    const s = t?.smena;
    if (!t || !s || !s.tugadi) return [];
    const T = (k: string, ...a: unknown[]) => this.til.t(k, ...a);
    const r: HisobSatri[] = [
      { yorliq: T('Smenalar_Qaytim'), qiymat: ishoraPul(s.ochishQaytim) },
      { yorliq: T('Smenalar_SavdoAparatlar'), qiymat: ishoraPul(s.savdo) },
    ];
    if (s.qaytganNasiya > 0) r.push({ yorliq: T('Smenalar_QaytganNasiya'), qiymat: ishoraPul(s.qaytganNasiya) });
    r.push({ yorliq: T('Smenalar_PlastikSatr', pul(s.yopishTerminal), pul(s.ochishTerminal)), qiymat: ishoraPul(-s.plastik) });
    r.push({ yorliq: T('Smenalar_DepozitSatr', pul(s.ochishDepozit), pul(s.yopishDepozit)), qiymat: ishoraPul(-s.depozitFarqi) });
    if (s.nasiyaJami > 0) r.push({ yorliq: T('Smenalar_NasiyaSatr', this.nomlar(t.nasiyalar.map((n) => n.mijozIsmi))), qiymat: ishoraPul(-s.nasiyaJami) });
    if (s.xarajatJami > 0) r.push({ yorliq: T('Smenalar_XarajatSatr', this.nomlar(t.xarajatlar.map((x) => this.kichik(x.sabab)))), qiymat: ishoraPul(-s.xarajatJami) });
    r.push({ yorliq: T('Smenalar_KassadaKerak'), qiymat: pul(s.kutilgan), qalin: true, ustChiziq: true });
    r.push({ yorliq: T('Smenalar_SanalganNaqd'), qiymat: pul(s.sanalganNaqd ?? 0), qalin: true });
    return r;
  });
  /** Ochiq smenadagi yozuvlarni o'chirish — Smenalar ruxsati bor (boshliq) foydalanuvchiga. */
  protected readonly ochirishMumkin = computed(() => this.ochiq() && this.auth.bor('Smenalar'));
  protected readonly yozuvBor = computed(() => {
    const t = this.tafsilot();
    return !!t && (t.nasiyalar.length > 0 || t.qaytishlar.length > 0 || t.xarajatlar.length > 0);
  });

  constructor() {
    const eshit = () => this.tor.set(this.mq.matches);
    this.mq.addEventListener('change', eshit);
    inject(DestroyRef).onDestroy(() => this.mq.removeEventListener('change', eshit));

    this.malumot.operatorlar().then((o) => this.operatorlar.set(o)).catch(() => undefined);
    effect(() => { this.davr(); this.operatorId(); untracked(() => this.royxatniYukla()); });
    effect(() => { const id = this.tanlanganId(); untracked(() => this.tafsilotYukla(id)); });
    jonliYangila(() => this.yangila(), (h) => HODISALAR.includes(h.turi));
  }

  // ---- Yuklash
  private async royxatniYukla() {
    const [dan, gacha] = this.oraliq();
    try {
      const [royxat, joriy, oxirgi] = await Promise.all([
        this.server.smenalar(dan, gacha, this.operatorId() ?? undefined),
        this.server.joriySmena(),
        this.server.oxirgiYopilgan(),
      ]);
      this.smenalar.set(royxat);
      this.joriy.set(joriy);
      this.oxirgiYopilganId.set(oxirgi?.smena.id ?? null);
      this.xato.set(null);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.yuklandi.set(true);
    }
  }

  private async tafsilotYukla(id: number | null, jim = false) {
    if (id == null) { this.tafsilot.set(null); return; }
    if (!jim) this.tafsilotYuklanmoqda.set(true);
    try {
      this.tafsilot.set(await this.server.smena(id));
    } catch (e) {
      this.tafsilot.set(null);
      if (!jim) this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    } finally {
      this.tafsilotYuklanmoqda.set(false);
    }
  }

  protected async yangila() {
    await this.royxatniYukla();
    await this.tafsilotYukla(this.tanlanganId(), true);
  }

  // ---- Boshqaruv
  protected davrTanla(d: Davr) { this.davr.set(d); }
  protected operatorTanla(v: string) { this.operatorId.set(v ? Number(v) : null); }

  protected tanla(id: number) {
    this.ochirTasdiq.set(null);
    this.router.navigate([], { queryParams: { smena: id }, queryParamsHandling: 'merge', replaceUrl: !this.tor() });
  }
  protected orqaga() {
    this.router.navigate([], { queryParams: { smena: null }, queryParamsHandling: 'merge' });
  }

  protected tuzatildi(t: SmenaTafsilotDto) {
    this.tafsilot.set(t);
    this.royxatniYukla();
  }

  protected async excel() {
    const t = this.tafsilot();
    if (!t || this.eksportBand()) return;
    this.eksportBand.set(true);
    try {
      await smenaExcel(t, (k, ...a) => this.til.t(k, ...a));
      this.bildirish.korsat(this.til.t('FaylSaqlandi'));
      void this.server.auditEksport('Smena', `#${t.smena.id} — fuelcontrol-smena-${t.smena.id}.xlsx`);
    } catch (e) {
      this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    } finally {
      this.eksportBand.set(false);
    }
  }

  protected ochirSoragan(tur: 'nasiya' | 'xarajat' | 'qaytish', id: number) {
    const k = `${tur}:${id}`;
    if (this.ochirTasdiq() !== k) { this.ochirTasdiq.set(k); return; }
    this.ochir(tur, id);
  }

  private async ochir(tur: 'nasiya' | 'xarajat' | 'qaytish', id: number) {
    try {
      if (tur === 'nasiya') await this.server.nasiyaOchir(id);
      else if (tur === 'xarajat') await this.server.xarajatOchir(id);
      else await this.server.qaytishOchir(id);
      this.ochirTasdiq.set(null);
      this.bildirish.korsat(this.til.t('Smenalar_Ochirildi'));
      await this.yangila();
    } catch (e) {
      this.ochirTasdiq.set(null);
      this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    }
  }

  // ---- Ko'rinish yordamchilari
  protected kunSoat(s: string): string { return `${kunQisqa(s)} ${soat(s)}`; }
  protected davomiylik(boshi: string, oxiri: string | null): string {
    const [h, m] = davomiylikSD(boshi, oxiri);
    return this.til.t('Smenalar_Davomiylik', h, m);
  }
  protected sarlavhaIzoh(s: SmenaDto): string {
    const b = this.kunSoat(s.boshlandi);
    return s.tugadi
      ? this.til.t('Smenalar_DavomiYopilgan', s.operatorIsmi, b, this.kunSoat(s.tugadi))
      : this.til.t('Smenalar_DavomiOchiq', s.operatorIsmi, b);
  }
  protected farqMatn(f: number): string { return f === 0 ? '0' : ishoraPul(f); }
  protected farqSinf(f: number): string { return f === 0 ? 'nol' : f < 0 ? 'manfiy' : 'musbat'; }
  /** "Jasur To'xtayev, Farhod Ismoilov" — ko'pi bilan 2 ta nom, qolgani "+N". */
  private nomlar(r: string[]): string {
    return r.length <= 2 ? r.join(', ') : `${r.slice(0, 2).join(', ')} +${r.length - 2}`;
  }
  private kichik(m: string): string { return m ? m.charAt(0).toLowerCase() + m.slice(1) : m; }
  protected tolovNomi(n: NasiyaQaytishiDto): string { return this.til.t('Tolov_' + n.usul); }
  protected xarajatManba(x: XarajatDto): string { return this.til.t(x.manba === 'Kassa' ? 'Smenalar_ManbaKassa' : 'Smenalar_ManbaDepozit'); }
  protected nasiyaMuddat(n: NasiyaDto): string { return this.til.t('Smenalar_MuddatGacha', kunQisqa(n.muddat)); }
  protected yozuvVaqti(s: string): string { return soat(sana(s)); }
}
