import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { NgTemplateOutlet } from '@angular/common';
import { api, ol } from '../../api/api';
import { Auth } from '../../core/auth';
import { Til } from '../../core/til';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { jonliYangila } from '../../core/malumot';
import { pul, litr, isoKun } from '../../core/format';
import type { BoshqaruvBugun, TolovTuri } from '../../api/turlar';
import { Ikon } from '../../ui/ikon';
import { SotuvQator } from '../../ui/sotuv-qator';

interface Ustun { sana: string; summa: number; litr: number; foiz: number; bugun: boolean }

@Component({
  selector: 'boshqaruv-sahifa',
  imports: [RouterLink, NgTemplateOutlet, Ikon, SotuvQator],
  templateUrl: './boshqaruv.html',
  styleUrl: './boshqaruv.scss',
})
export class BoshqaruvSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly bildirish = inject(Bildirish);

  protected readonly d = signal<BoshqaruvBugun | null>(null);
  protected readonly xato = signal<string | null>(null);
  protected readonly yuklanmoqda = signal(false);
  protected readonly tanlanganUstun = signal<number | null>(null);
  protected readonly pul = pul;
  protected readonly litr = litr;

  protected readonly tolovTurlari: TolovTuri[] = ['Naqd', 'Plastik', 'Click'];

  /** Kechagiga nisbatan o'zgarish, %. */
  protected readonly ozgarish = computed(() => {
    const k = this.d()?.kpi;
    if (!k?.kechagiSumma) return null;
    return Math.round(((k.bugungiSumma - k.kechagiSumma) / k.kechagiSumma) * 100);
  });

  protected readonly tolovlar = computed(() => {
    const t = this.d()?.tolovUlushlari ?? [];
    const jami = t.reduce((s, x) => s + x.summa, 0);
    return this.tolovTurlari.map((turi) => {
      const summa = t.find((x) => x.turi === turi)?.summa ?? 0;
      return { turi, summa, foiz: jami ? (summa / jami) * 100 : 0 };
    });
  });

  protected readonly yoqilgilar = computed(() => {
    const y = [...(this.d()?.yoqilgiUlushlari ?? [])].sort((a, b) => b.summa - a.summa);
    const max = Math.max(1, ...y.map((x) => x.summa));
    return y.map((x) => ({ ...x, foiz: (x.summa / max) * 100 }));
  });

  protected readonly ustunlar = computed<Ustun[]>(() => {
    const k = this.d()?.oxirgiKunlar ?? [];
    const max = Math.max(1, ...k.map((x) => x.summa));
    const bugun = isoKun();
    return k.map((x) => ({
      sana: x.sana, summa: x.summa, litr: x.litr,
      foiz: (x.summa / max) * 100, bugun: x.sana === bugun,
    }));
  });
  protected readonly ustunMax = computed(() => Math.max(0, ...this.ustunlar().map((x) => x.summa)));

  constructor() {
    this.yukla();
    jonliYangila(() => this.yukla(true));
  }

  async yukla(jim = false) {
    if (!jim) this.yuklanmoqda.set(true);
    try {
      this.d.set(await ol(api.GET('/boshqaruv/bugun')));
      this.xato.set(null);
    } catch (e) {
      const m = xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
      if (this.d()) { if (!jim) this.bildirish.korsat(m, true); } else this.xato.set(m);
    } finally {
      this.yuklanmoqda.set(false);
    }
  }

  ustunTanla(i: number) {
    this.tanlanganUstun.set(this.tanlanganUstun() === i ? null : i);
  }
}
