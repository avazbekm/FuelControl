import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Location } from '@angular/common';
import { api, ol } from '../../api/api';
import { Auth } from '../../core/auth';
import { Til } from '../../core/til';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { Malumot, jonliYangila } from '../../core/malumot';
import { pul, kunSoat, davomiylik, sonOl } from '../../core/format';
import type { Aparat, Smena, Sotuv, TolovTuri } from '../../api/turlar';
import { Ikon } from '../../ui/ikon';
import { SotuvQator } from '../../ui/sotuv-qator';
import { SotuvTahrir } from '../../ui/sotuv-tahrir';
import { smenaJami } from './smenalar';
import { orqagaBogla } from '../../core/orqaga';

const TURLAR: TolovTuri[] = ['Naqd', 'Plastik', 'Click'];

@Component({
  selector: 'smena-tafsilot-sahifa',
  imports: [FormsModule, Ikon, SotuvQator, SotuvTahrir],
  templateUrl: './smena-tafsilot.html',
  styles: `
    .solishtir td:first-child { font-weight: 600; }
    .farq-panel { padding: 12px 16px; border-radius: 16px; font-weight: 650; display: flex; gap: 8px; align-items: center; }
    .farq-panel.kamomat { background: var(--kamomat-fon); border: 1px solid var(--kamomat-chegara); color: var(--qizil); }
    .farq-panel.ortiqcha { background: var(--ortiqcha-fon); border: 1px solid var(--ortiqcha-chegara); color: var(--yashil); }
    .farq-panel.aniq { background: var(--b-kok-fon); color: var(--b-kok); }
    .yop-maydonlar { display: grid; grid-template-columns: minmax(0, 1fr); gap: 10px; }
    .yop-qator { display: grid; grid-template-columns: 84px minmax(0, 1fr) 96px; gap: 10px; align-items: center; }
    .yop-qator .kutilgan { font-size: 12px; color: var(--matn-2); }
    .yop-qator .farq { text-align: right; font-weight: 650; font-size: 14px; }
  `,
})
export class SmenaTafsilotSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly bildirish = inject(Bildirish);
  private readonly malumot = inject(Malumot);
  private readonly location = inject(Location);

  readonly id = input.required<string>();
  readonly yop = input<string>();

  protected readonly smena = signal<Smena | null>(null);
  protected readonly sotuvlar = signal<Sotuv[]>([]);
  protected readonly aparatlar = signal<Aparat[]>([]);
  protected readonly xato = signal<string | null>(null);
  protected readonly yopishOchiq = signal(false);
  protected readonly topshirilgan = signal<Record<TolovTuri, string>>({ Naqd: '', Plastik: '', Click: '' });
  protected izoh = '';
  protected readonly band = signal(false);
  protected readonly tahrir = signal<{ s: Sotuv; rejim: 'tahrir' | 'bekor' } | null>(null);
  private readonly _tahrirOrqaga = orqagaBogla(this.tahrir, null);
  protected readonly turlar = TURLAR;
  protected readonly pul = pul;
  protected readonly kunSoat = kunSoat;
  protected readonly jami = smenaJami;

  protected readonly yopaOladi = computed(() => {
    const s = this.smena();
    return !!s && !s.tugadi && this.auth.bor('SmenaYopish')
      && (s.operatorId === this.auth.foydalanuvchi()?.id || this.auth.bor('Smenalar'));
  });

  /** Har to'lov turi: kutilgan, topshirilgan, farq (yopilgan smena yoki yopish formasidan jonli). */
  protected readonly qatorlar = computed(() => {
    const s = this.smena();
    if (!s) return [];
    const forma = this.yopishOchiq();
    return TURLAR.map((t) => {
      const kutilgan = s[`kutilgan${t}`];
      const kiritilgan = this.topshirilgan()[t].trim();
      const top = forma ? (kiritilgan === '' ? null : Math.round(sonOl(kiritilgan) ?? 0)) : s[`topshirilgan${t}`];
      return { turi: t, kutilgan, topshirilgan: top, farq: top == null ? null : top - kutilgan };
    });
  });
  protected readonly jamiFarq = computed(() => this.qatorlar().reduce((s, q) => s + (q.farq ?? 0), 0));
  /** Yopish formasida hamma maydon to'ldirilganmi (farq paneli shundan keyin ko'rsatiladi). */
  protected readonly toliqKiritildi = computed(() => this.qatorlar().every((q) => q.topshirilgan != null));

  constructor() {
    effect(() => { this.id(); this.yukla(); });
    jonliYangila(() => this.yukla(), (h) =>
      (h.turi === 'SmenaOzgardi' && h.smena.id === +this.id()) || ((h.turi === 'SotuvQoshildi' || h.turi === 'SotuvOzgardi') && h.sotuv.smenaId === +this.id()));
    this.malumot.aparatlar().then((a) => this.aparatlar.set(a)).catch(() => {});
  }

  async yukla() {
    const id = +this.id();
    try {
      const { smena: s, sotuvlar } = await ol(api.GET('/smenalar/{id}', { params: { path: { id } } }));
      this.smena.set(s);
      this.sotuvlar.set([...sotuvlar].sort((a, b) => b.vaqt.localeCompare(a.vaqt)));
      this.xato.set(s ? null : this.til.t('MalumotYoq'));
      if (this.yop() && this.yopaOladi() && !this.yopishOchiq()) this.yopishOchiq.set(true);
    } catch (e) {
      if (!this.smena()) this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    }
  }

  ortga() { this.location.back(); }

  topshirilganOzgardi(t: TolovTuri, v: string) {
    this.topshirilgan.set({ ...this.topshirilgan(), [t]: v });
  }

  /** Kutilgan summani topshirilganga ko'chirish (farq yo'q bo'lsa tez to'ldirish). */
  kutilgandekTold() {
    const s = this.smena();
    if (s) this.topshirilgan.set({ Naqd: String(s.kutilganNaqd), Plastik: String(s.kutilganPlastik), Click: String(s.kutilganClick) });
  }

  async yopish() {
    const s = this.smena();
    if (!s) return;
    const q = this.topshirilgan();
    const n = (t: TolovTuri) => Math.round(sonOl(q[t]) ?? 0);
    if (TURLAR.some((t) => q[t].trim() === '' || n(t) < 0)) return this.bildirish.korsat(this.til.t('Xato_Maydon'), true);
    this.band.set(true);
    try {
      const yangi = await ol(api.POST('/smenalar/{id}/yop', {
        params: { path: { id: s.id } },
        body: { naqd: n('Naqd'), plastik: n('Plastik'), click: n('Click'), izoh: this.izoh.trim() || null },
      }));
      this.smena.set(yangi);
      this.yopishOchiq.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
    } catch (e) {
      this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    } finally {
      this.band.set(false);
    }
  }

  dav(s: Smena) {
    return davomiylik(s.boshlandi, s.tugadi, this.til.t('Soat'), this.til.t('Min'));
  }

  tahrirYopildi(saqlandi: boolean) {
    this.tahrir.set(null);
    if (saqlandi) this.yukla();
  }
}
