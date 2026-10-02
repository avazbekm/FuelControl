import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { api, ol } from '../../api/api';
import { Auth } from '../../core/auth';
import { Til } from '../../core/til';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { Malumot, Yoqilgi, jonliYangila } from '../../core/malumot';
import { Navbat, NavbatSotuvi } from '../../core/navbat';
import { pul, litr, soat, isoKun, sonOl, davomiylik } from '../../core/format';
import type { Aparat, Smena, Sotuv } from '../../api/turlar';
import { Ikon } from '../../ui/ikon';
import { SotuvQator } from '../../ui/sotuv-qator';
import { SotuvTahrir } from '../../ui/sotuv-tahrir';
import { TolovKiritish, TolovTanlovi, Qismlar, BOSH_QISMLAR, tolovlarYasa } from '../../ui/tolov-kiritish';

@Component({
  selector: 'sotuv-sahifa',
  imports: [FormsModule, Ikon, SotuvQator, SotuvTahrir, TolovKiritish],
  templateUrl: './sotuv.html',
  styleUrl: './sotuv.scss',
})
export class SotuvSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  protected readonly navbat = inject(Navbat);
  private readonly malumot = inject(Malumot);
  private readonly bildirish = inject(Bildirish);
  private readonly router = inject(Router);

  protected readonly aparatlar = signal<Aparat[]>(this.malumot.keshdan<Aparat>('aparat'));
  private readonly yoqilgilar = signal<Yoqilgi[]>(this.malumot.keshdan<Yoqilgi>('yoqilgi'));
  protected readonly aparatId = signal<number | null>(null);
  protected readonly rejim = signal<'summa' | 'litr'>('summa');
  protected readonly qiymat = signal('');
  protected readonly turi = signal<TolovTanlovi>('Naqd');
  protected readonly qismlar = signal<Qismlar>(BOSH_QISMLAR());
  protected readonly band = signal(false);
  protected readonly xato = signal<string | null>(null);

  /** undefined — hali yuklanmagan, null — ochiq smena yo'q. */
  protected readonly smena = signal<Smena | null | undefined>(undefined);
  protected readonly sotuvlar = signal<Sotuv[]>([]);
  protected readonly tahrir = signal<{ s: Sotuv; rejim: 'tahrir' | 'bekor' } | null>(null);

  protected readonly pul = pul;
  protected readonly litr = litr;
  protected readonly soat = soat;

  protected readonly narxlar = computed(() => new Map(this.yoqilgilar().map((y) => [y.id, y])));
  protected readonly aparat = computed(() => this.aparatlar().find((a) => a.id === this.aparatId()) ?? null);
  protected readonly narx = computed(() => {
    const a = this.aparat();
    return a ? this.narxlar().get(a.yoqilgiTuriId)?.narx ?? 0 : 0;
  });
  /** Server bilan bir xil qoida: summa → litr (2 xona), litr → summa (so'mgacha). */
  protected readonly hisob = computed(() => {
    const n = sonOl(this.qiymat()) ?? 0;
    const narx = this.narx();
    if (n <= 0) return { summa: 0, litr: 0 };
    if (this.rejim() === 'summa') {
      const summa = Math.round(n);
      return { summa, litr: narx ? Math.round((summa / narx) * 100) / 100 : 0 };
    }
    const l = Math.round(n * 100) / 100;
    return { summa: Math.round(l * narx), litr: l };
  });
  protected readonly tezlar = computed(() =>
    this.rejim() === 'summa' ? [50000, 100000, 150000, 200000, 300000, 500000] : [5, 10, 20, 30, 40, 50]);
  protected readonly bugungiJami = computed(() =>
    this.sotuvlar().filter((s) => s.holati === 'Faol').reduce((s, x) => s + x.summa, 0));
  protected readonly navbatdagilar = computed(() => [...this.navbat.royxat()].reverse());

  constructor() {
    this.yukla();
    jonliYangila(() => this.royxatniYukla());
    this.navbat.yuborildi$.pipe(takeUntilDestroyed()).subscribe(() => this.royxatniYukla());
  }

  async yukla() {
    try {
      const [a, y] = await Promise.all([this.malumot.aparatlar(), this.malumot.yoqilgilar()]);
      this.aparatlar.set(a);
      this.yoqilgilar.set(y);
    } catch (e) {
      if (!this.aparatlar().length) this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    }
    await this.royxatniYukla();
  }

  async royxatniYukla() {
    const id = this.auth.foydalanuvchi()?.id;
    const bugun = isoKun();
    try {
      const [smena, sotuvlar] = await Promise.all([
        ol(api.GET('/smenalar/joriy')),
        ol(api.GET('/sotuvlar', { params: { query: { dan: bugun, gacha: bugun, operatorId: id } } })),
      ]);
      this.smena.set(smena ?? null);
      this.sotuvlar.set([...sotuvlar].sort((a, b) => b.vaqt.localeCompare(a.vaqt)));
    } catch { /* aloqa yo'q — oxirgi holat qoladi, banner ko'rsatiladi */ }
  }

  rejimTanla(r: 'summa' | 'litr') {
    if (r === this.rejim()) return;
    // Kiritilgan qiymatni yangi birlikka o'tkazamiz.
    const h = this.hisob();
    this.rejim.set(r);
    this.qiymat.set(h.summa ? String(r === 'summa' ? h.summa : h.litr) : '');
  }

  tez(n: number) {
    this.qiymat.set(String(n));
  }

  tozala() {
    this.qiymat.set('');
    this.turi.set('Naqd');
    this.qismlar.set(BOSH_QISMLAR());
    this.xato.set(null);
  }

  async saqla() {
    this.xato.set(null);
    const a = this.aparat();
    if (!a) return this.xato.set(this.til.t('AvvalAparat'));
    const h = this.hisob();
    if (h.summa <= 0) return this.xato.set(this.til.t('Xato_Summa'));
    const tolovlar = tolovlarYasa(this.turi(), this.qismlar(), h.summa);
    if (!tolovlar) return this.xato.set(this.til.t('Xato_AralashYigindi'));

    // Har sotuvga yangi IdempotencyKey — navbatdan qayta yuborilsa ham server bitta marta yozadi.
    const kalit = crypto.randomUUID();
    const y: NavbatSotuvi = {
      idempotencyKey: kalit,
      foydalanuvchiId: this.auth.foydalanuvchi()!.id,
      sorov: {
        aparatId: a.id,
        summa: this.rejim() === 'summa' ? h.summa : null,
        litr: this.rejim() === 'litr' ? h.litr : null,
        tolovlar,
        idempotencyKey: kalit,
      },
      yaratildi: new Date().toISOString(),
      aparatRaqami: a.raqam, yoqilgiNomi: a.yoqilgiNomi, summa: h.summa, litr: h.litr,
    };
    this.band.set(true);
    try {
      const s = await this.navbat.qosh(y);
      if (s) {
        this.bildirish.korsat(`${this.til.t('Saqlandi')}: ${pul(s.summa)} ${this.til.t('Som')} · ${litr(s.litr)} ${this.til.t('L')}`);
        this.sotuvlar.set([s, ...this.sotuvlar().filter((x) => x.id !== s.id)]);
        this.royxatniYukla();
        this.malumot.aparatlar().then((r) => this.aparatlar.set(r)).catch(() => {});
      } else {
        this.bildirish.korsat(this.til.t('NavbatgaQoyildi'));
      }
      this.tozala();
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }

  async smenaOch() {
    try {
      const s = await ol(api.POST('/smenalar/och'));
      this.smena.set(s);
      this.bildirish.korsat(this.til.t('SmenaOchish') + ' — ' + this.til.t('Muvaffaqiyatli'));
    } catch (e) {
      this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    }
  }

  smenaYop() {
    const s = this.smena();
    if (s) this.router.navigate(['/smenalar', s.id], { queryParams: { yop: 1 } });
  }

  smenaMatni(s: Smena): string {
    const jami = s.kutilganNaqd + s.kutilganPlastik + s.kutilganClick;
    return this.til.t('SmenaHolati', s.id, soat(s.boshlandi), s.sotuvSoni, `${pul(jami)} ${this.til.t('Som')}`)
      + ' · ' + davomiylik(s.boshlandi, null, this.til.t('Soat'), this.til.t('Min'));
  }

  tahrirYopildi(saqlandi: boolean) {
    this.tahrir.set(null);
    if (saqlandi) this.royxatniYukla();
  }
}
