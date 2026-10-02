import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Location } from '@angular/common';
import { api, ol } from '../../api/api';
import { Auth } from '../../core/auth';
import { Til } from '../../core/til';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { pul, kun, isoKun, oyBoshi, oyQosh, sonOl } from '../../core/format';
import type { HarakatTuri, OperatorHisob } from '../../api/turlar';
import { Ikon } from '../../ui/ikon';

const BADGE: Record<HarakatTuri, string> = { Maosh: 'yashil', Avans: 'sariq', Kamomat: 'qizil', Ortiqcha: 'yashil', Tolov: 'kok' };

/** Operator hisob-varaqasi: /operatorlar/:id (boshliq) yoki /hisobim (operatorning o'zi). */
@Component({
  selector: 'operator-hisob-sahifa',
  imports: [FormsModule, Ikon],
  templateUrl: './operator-hisob.html',
  styles: `
    .oy-nav { display: flex; align-items: center; gap: 6px; }
    .oy-nomi { min-width: 120px; text-align: center; font-weight: 650; text-transform: capitalize; }
    .kor { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
    @media (min-width: 700px) { .kor { grid-template-columns: repeat(5, minmax(0, 1fr)); } }
    .kor > div { display: flex; flex-direction: column; gap: 2px; padding: 10px 12px; border-radius: 16px; background: var(--naycha); }
  `,
})
export class OperatorHisobSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly bildirish = inject(Bildirish);
  private readonly location = inject(Location);

  /** Marshrut parametri; bo'lmasa — joriy foydalanuvchi. */
  readonly id = input<string>();
  protected readonly operatorId = computed(() => +(this.id() ?? this.auth.foydalanuvchi()?.id ?? 0));
  protected readonly ozimmi = computed(() => this.id() === undefined);

  protected readonly oy = signal(oyBoshi(isoKun()));
  protected readonly h = signal<OperatorHisob | null>(null);
  protected readonly xato = signal<string | null>(null);
  protected readonly dialog = signal(false);
  protected readonly dTuri = signal<'Avans' | 'Tolov'>('Avans');
  protected dSumma = '';
  protected dIzoh = '';
  protected readonly band = signal(false);
  protected readonly dXato = signal<string | null>(null);
  protected readonly pul = pul;
  protected readonly kun = kun;
  protected readonly badge = BADGE;

  // Brauzerlarda o'zbek oy nomlari (Intl) ko'pincha yo'q — lug'atdan olamiz.
  protected readonly oyNomi = computed(() =>
    `${this.til.t('OyNomlari').split(',')[+this.oy().slice(5, 7) - 1]} ${this.oy().slice(0, 4)}`);
  protected readonly joriyOymi = computed(() => this.oy() === oyBoshi(isoKun()));
  protected readonly korsatkichlar = computed(() => {
    const r = this.h()?.harakatlar ?? [];
    const yig = (t: HarakatTuri) => r.filter((x) => x.turi === t).reduce((s, x) => s + x.summa, 0);
    return (['Maosh', 'Avans', 'Kamomat', 'Ortiqcha', 'Tolov'] as HarakatTuri[]).map((t) => ({ turi: t, summa: yig(t) }));
  });

  constructor() {
    effect(() => { this.operatorId(); this.oy(); this.yukla(); });
  }

  async yukla() {
    const id = this.operatorId();
    if (!id) return;
    try {
      this.h.set(await ol(api.GET('/operatorlar/{id}/hisob', { params: { path: { id }, query: { oy: this.oy() } } })));
      this.xato.set(null);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    }
  }

  oyAlmashtir(n: number) { this.oy.set(oyQosh(this.oy(), n)); }
  ortga() { this.location.back(); }

  dialogOch() {
    this.dTuri.set('Avans'); this.dSumma = ''; this.dIzoh = ''; this.dXato.set(null);
    this.dialog.set(true);
  }

  async pulBer() {
    const summa = Math.round(sonOl(this.dSumma) ?? 0);
    if (summa <= 0) return this.dXato.set(this.til.t('Xato_Summa'));
    this.band.set(true);
    try {
      await ol(api.POST('/operatorlar/{id}/harakat', { params: { path: { id: this.operatorId() } }, body: { turi: this.dTuri(), summa, izoh: this.dIzoh.trim() } }));
      this.dialog.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
      this.yukla();
    } catch (e) {
      this.dXato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
