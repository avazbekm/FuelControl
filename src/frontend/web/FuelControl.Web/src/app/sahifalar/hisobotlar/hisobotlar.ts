import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Auth } from '../../core/auth';
import { Til } from '../../core/til';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { Malumot, OperatorElement, jonliYangila } from '../../core/malumot';
import { isoKun, ishoraPul, kunOy, kunQosh, kunToliq, litr, litrQisqa, oyBoshi, oyOxiri, oyQosh, pul } from '../../core/format';
import type { HisobotAparatDto, HisobotDto, HisobotGuruhi, HisobotQatoriDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { YoqilgiPill } from '../../ui/belgilar';
import { hisobotExcel } from './hisobot-excel';

type TezDavr = 'kecha' | '7kun' | 'shuoy' | 'otganoy';

interface Kpi { kalit: string; qiymat: string; izoh: string; rang?: string; qizil?: boolean }

const HODISALAR = ['SmenaOzgardi', 'NasiyaOzgardi', 'XarajatOzgardi', 'AparatOzgardi'];

/**
 * Hisobotlar (docs/dizayn/Hisobotlar): davr/operator/guruh filtri, KPI plitkalari, guruh jadvali (smena | kun | oy | operator)
 * va "Aparatlar va baklar" jadvali (operator filtriga bog'liq emas — butun shoxobcha bo'yicha). Excel — ikki varaq.
 */
@Component({
  selector: 'hisobotlar-sahifa',
  imports: [Ikon, YoqilgiPill],
  templateUrl: './hisobotlar.html',
  styleUrl: './hisobotlar.scss',
})
export class HisobotlarSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly server = inject(Server);
  private readonly malumot = inject(Malumot);
  private readonly bildirish = inject(Bildirish);

  /** `?tez=kecha|7kun|shuoy|otganoy` — Boshqaruvdagi "Oylik hisobot" tugmasi shu bilan keladi. */
  readonly tezParam = input<string | undefined>(undefined, { alias: 'tez' });

  protected readonly tezlar: { kod: TezDavr; kalit: string }[] = [
    { kod: 'kecha', kalit: 'Hisobot_TezKecha' }, { kod: '7kun', kalit: 'Hisobot_Tez7Kun' },
    { kod: 'shuoy', kalit: 'Hisobot_TezShuOy' }, { kod: 'otganoy', kalit: 'Hisobot_TezOtganOy' },
  ];
  protected readonly guruhlar: { kod: HisobotGuruhi; kalit: string }[] = [
    { kod: 'Smena', kalit: 'Hisobot_GuruhSmena' }, { kod: 'Kun', kalit: 'Hisobot_GuruhKun' },
    { kod: 'Oy', kalit: 'Hisobot_GuruhOy' }, { kod: 'Operator', kalit: 'Hisobot_GuruhOperator' },
  ];

  protected readonly dan = signal(oyBoshi(isoKun()));
  protected readonly gacha = signal(isoKun());
  protected readonly tez = signal<TezDavr | null>('shuoy');
  protected readonly guruh = signal<HisobotGuruhi>('Smena');
  protected readonly operatorId = signal<number | null>(null);
  protected readonly operatorlar = signal<OperatorElement[]>([]);
  protected readonly natija = signal<HisobotDto | null>(null);
  protected readonly ranglar = signal<Record<string, string>>({});
  protected readonly yuklandi = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected readonly eksportBand = signal(false);

  protected readonly pul = pul;
  protected readonly litr = litr;
  protected readonly litrQisqa = litrQisqa;
  protected readonly ishoraPul = ishoraPul;
  protected readonly kunToliq = kunToliq;

  protected readonly davrIzoh = computed(() =>
    this.til.t('Hisobot_Davr', kunToliq(this.dan()), kunToliq(this.gacha()), this.natija()?.jami.smenaSoni ?? 0));

  protected readonly kpilar = computed<Kpi[]>(() => {
    const j = this.natija()?.jami;
    if (!j) return [];
    const T = (k: string, ...a: unknown[]) => this.til.t(k, ...a);
    const foiz = (v: number) => (j.savdo > 0 ? `${Math.round((v / j.savdo) * 100)}%` : '0%');
    const r: Kpi[] = [
      { kalit: 'Hisobot_KpiJamiSavdo', qiymat: pul(j.savdo), izoh: T('Hisobot_KpiSomSmena', j.smenaSoni) },
      { kalit: 'Hisobot_KpiLitr', qiymat: litr(j.litr), izoh: T('Hisobot_KpiBarchaAparatlar') },
      { kalit: 'Hisobot_KpiNaqd', qiymat: pul(j.naqdSavdo), izoh: foiz(j.naqdSavdo), rang: 'var(--q-naqd)' },
      { kalit: 'Hisobot_KpiPlastik', qiymat: pul(j.plastik), izoh: foiz(j.plastik), rang: 'var(--q-plastik)' },
      { kalit: 'Hisobot_KpiDepozit', qiymat: pul(j.depozit), izoh: foiz(j.depozit), rang: 'var(--q-depozit)' },
      { kalit: 'Hisobot_KpiNasiya', qiymat: pul(j.nasiya), izoh: T('Hisobot_KpiQaytgan', pul(j.qaytganNasiya)), rang: 'var(--q-nasiya)' },
      { kalit: 'Hisobot_KpiXarajat', qiymat: pul(j.xarajat), izoh: T('Hisobot_KpiXarajatYozuv', j.xarajatSoni) },
      { kalit: 'Hisobot_KpiKamomat', qiymat: pul(j.kamomat), izoh: T('Hisobot_KpiOrtiqcha', pul(j.ortiqcha)), qizil: j.kamomat > 0 },
    ];
    const avans = this.natija()?.avans ?? 0;
    if (avans > 0) r.push({ kalit: 'Hisobot_KpiAvans', qiymat: pul(avans), izoh: T('Hisobot_KpiAvansIzoh') });
    return r;
  });

  /** Bak jadvalining Jami qatori (2 xona aniqlikda yig'iladi). */
  protected readonly bakJami = computed(() => {
    const a = this.natija()?.aparatlar ?? [];
    const y = (f: (x: HisobotAparatDto) => number) => Math.round(a.reduce((s, x) => s + f(x), 0) * 100) / 100;
    return { boshida: y((x) => x.bakBoshida), kirim: y((x) => x.kirim), sotildi: y((x) => x.sotildi), oxirida: y((x) => x.bakOxirida), savdo: a.reduce((s, x) => s + x.savdo, 0) };
  });

  constructor() {
    this.malumot.operatorlar().then((o) => this.operatorlar.set(o)).catch(() => undefined);
    this.server.yoqilgilar().then((r) => this.ranglar.set(Object.fromEntries(r.map((y) => [y.nomi, y.rang])))).catch(() => undefined);
    // ?tez= bilan ochilsa shu davr
    effect(() => {
      const t = this.tezParam();
      if (t === 'kecha' || t === '7kun' || t === 'shuoy' || t === 'otganoy') untracked(() => this.tezTanla(t));
    });
    effect(() => { this.dan(); this.gacha(); this.guruh(); this.operatorId(); untracked(() => this.yukla()); });
    jonliYangila(() => this.yukla(), (h) => HODISALAR.includes(h.turi));
  }

  protected tezTanla(t: TezDavr) {
    const b = isoKun();
    const [d, g] = ({
      kecha: [kunQosh(b, -1), kunQosh(b, -1)],
      '7kun': [kunQosh(b, -6), b],
      shuoy: [oyBoshi(b), b],
      otganoy: [oyQosh(b, -1), oyOxiri(oyQosh(b, -1))],
    } as Record<TezDavr, [string, string]>)[t];
    this.tez.set(t);
    this.dan.set(d);
    this.gacha.set(g);
  }

  protected sanaOzgardi(qaysi: 'dan' | 'gacha', v: string) {
    if (!v) return;
    (qaysi === 'dan' ? this.dan : this.gacha).set(v);
    this.tez.set(null);
  }
  protected operatorTanla(v: string) { this.operatorId.set(v ? Number(v) : null); }

  private async yukla() {
    try {
      this.natija.set(await this.server.hisobot(this.dan(), this.gacha(), this.operatorId() ?? undefined, this.guruh()));
      this.xato.set(null);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.yuklandi.set(true);
    }
  }

  protected async qayta() { await this.yukla(); }

  // ---- Ko'rinish yordamchilari
  /** Guruh nomi: smena — "#39"; kun — "01.10.2026"; oy — "Oktabr 2026"; operator — ism (server til-mustaqil beradi). */
  protected nomi(q: HisobotQatoriDto): string {
    switch (this.guruh()) {
      case 'Smena': return '#' + q.guruh.replace(/^#/, '');
      case 'Kun': return /^\d{4}-\d\d-\d\d$/.test(q.sana ?? q.guruh) ? kunToliq(q.sana ?? q.guruh) : q.guruh;
      case 'Oy': return /^\d{4}-\d\d$/.test(q.guruh) ? `${this.til.t('OyNomlari').split(',')[+q.guruh.slice(5, 7) - 1]} ${q.guruh.slice(0, 4)}` : q.guruh;
      default: return q.guruh;
    }
  }
  protected kichikSana(q: HisobotQatoriDto): string { return this.guruh() === 'Smena' && q.sana ? kunOy(q.sana) : ''; }
  protected ikkinchi(q: HisobotQatoriDto): string {
    return this.guruh() === 'Smena' ? (q.operatorIsmi ?? '') : this.til.t('Hisobot_SmenaSoni', q.smenaSoni);
  }
  protected farq(q: HisobotQatoriDto): number { return q.ortiqcha - q.kamomat; }
  protected farqMatn(f: number): string { return f === 0 ? '0' : ishoraPul(f); }
  protected farqSinf(f: number): string { return f === 0 ? 'nol' : f < 0 ? 'manfiy' : 'musbat'; }
  protected birinchiYorliq(): string {
    return this.til.t(({ Smena: 'Hisobot_ColSmena', Kun: 'Hisobot_ColKun', Oy: 'Hisobot_ColOy', Operator: 'Operator' } as const)[this.guruh()]);
  }
  protected ikkinchiYorliq(): string { return this.til.t(this.guruh() === 'Smena' ? 'Hisobot_ColOperator' : 'Hisobot_ColSmenalar'); }
  protected rang(nomi: string): string { return this.ranglar()[nomi] ?? '#2563EB'; }
  protected kirimMatn(v: number): string { return v > 0 ? '+' + litrQisqa(v) : '0'; }

  protected async excel() {
    const n = this.natija();
    if (!n || this.eksportBand()) return;
    this.eksportBand.set(true);
    try {
      await hisobotExcel(n, {
        T: (k, ...a) => this.til.t(k, ...a), guruh: this.guruh(), nomi: (q) => this.nomi(q), ikkinchi: (q) => this.ikkinchi(q),
        fayl: `hisobot_${this.dan()}_${this.gacha()}.xlsx`,
      });
      this.bildirish.korsat(this.til.t('FaylSaqlandi'));
    } catch (e) {
      this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    } finally {
      this.eksportBand.set(false);
    }
  }
}
