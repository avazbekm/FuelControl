import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { api, ol } from '../../api/api';
import { Auth } from '../../core/auth';
import { Til } from '../../core/til';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { Malumot, OperatorElement } from '../../core/malumot';
import { pul, litr, isoKun, kunQosh, oyBoshi, oyOxiri, oyQosh } from '../../core/format';
import type { Hisobot, HisobotGuruhi, HisobotQatori } from '../../api/turlar';
import { Ikon } from '../../ui/ikon';
import { bolimlarga } from './hisobot-model';

/** `faqatJami` — kamomat/avans/bekor faqat guruh jami qatorida keladi; yoqilg'i qatorida bo'sh. */
type Ustun = { kalit: string; maydon: keyof HisobotQatori; tur: 'pul' | 'litr' | 'son'; faqatJami?: boolean };

const USTUNLAR: Ustun[] = [
  { kalit: 'Litr', maydon: 'litr', tur: 'litr' }, { kalit: 'Naqd', maydon: 'naqd', tur: 'pul' },
  { kalit: 'Plastik', maydon: 'plastik', tur: 'pul' }, { kalit: 'Click', maydon: 'click', tur: 'pul' },
  { kalit: 'Summa', maydon: 'summa', tur: 'pul' }, { kalit: 'SotuvSoni', maydon: 'soni', tur: 'son' },
  { kalit: 'Kamomat', maydon: 'kamomat', tur: 'pul', faqatJami: true }, { kalit: 'Avans', maydon: 'avans', tur: 'pul', faqatJami: true },
  { kalit: 'BekorSoni', maydon: 'bekorSoni', tur: 'son', faqatJami: true },
];

type TezDavr = 'bugun' | 'kecha' | 'kun7' | 'oy' | 'otganOy';

@Component({
  selector: 'hisobotlar-sahifa',
  imports: [FormsModule, Ikon],
  templateUrl: './hisobotlar.html',
  styleUrl: './hisobotlar.scss',
})
export class HisobotlarSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly malumot = inject(Malumot);
  private readonly bildirish = inject(Bildirish);

  protected readonly tezlar: { kod: TezDavr; kalit: string }[] = [
    { kod: 'bugun', kalit: 'Bugun' }, { kod: 'kecha', kalit: 'Kecha' }, { kod: 'kun7', kalit: 'Oxirgi7Kun' },
    { kod: 'oy', kalit: 'ShuOy' }, { kod: 'otganOy', kalit: 'OtganOy' },
  ];
  protected readonly guruhlar: { kod: HisobotGuruhi; kalit: string }[] = [
    { kod: 'Operator', kalit: 'OperatorBoyicha' }, { kod: 'Kun', kalit: 'Kunlik' }, { kod: 'Oy', kalit: 'Oylik' },
  ];
  protected readonly dan = signal(oyBoshi(isoKun()));
  protected readonly gacha = signal(isoKun());
  protected readonly tez = signal<TezDavr | null>('oy');
  protected readonly guruh = signal<HisobotGuruhi>('Operator');
  protected readonly operatorId = signal<number | null>(null);
  protected readonly operatorlar = signal<OperatorElement[]>([]);
  protected readonly natija = signal<Hisobot | null>(null);
  protected readonly yuklanmoqda = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected readonly pul = pul;
  protected readonly litr = litr;

  protected readonly bolimlar = computed(() => { const n = this.natija(); return n ? bolimlarga(n) : []; });
  protected readonly ustunlar = USTUNLAR;
  protected readonly yoqilgiBor = computed(() => this.bolimlar().some((b) => b.yoqilgilar.length > 0));
  protected readonly eksportBand = signal(false);

  protected readonly kpilar = computed(() => {
    const j = this.natija()?.jami;
    if (!j) return [];
    return [
      { kalit: 'JamiSavdo', qiymat: pul(j.summa), birlik: 'Som', asosiy: true },
      { kalit: 'Litr', qiymat: litr(j.litr), birlik: 'L' },
      { kalit: 'Naqd', qiymat: pul(j.naqd), birlik: 'Som', rang: 'naqd' },
      { kalit: 'Plastik', qiymat: pul(j.plastik), birlik: 'Som', rang: 'plastik' },
      { kalit: 'Click', qiymat: pul(j.click), birlik: 'Som', rang: 'click' },
      { kalit: 'Kamomat', qiymat: pul(j.kamomat), birlik: 'Som', qizil: j.kamomat > 0 },
      { kalit: 'Avans', qiymat: pul(j.avans), birlik: 'Som' },
      { kalit: 'BekorSoni', qiymat: String(j.bekorSoni), birlik: '' },
    ];
  });

  constructor() {
    this.yukla();
    this.malumot.operatorlar().then((o) => this.operatorlar.set(o)).catch(() => {});
  }

  tezTanla(t: TezDavr) {
    const b = isoKun();
    const [d, g] = {
      bugun: [b, b],
      kecha: [kunQosh(b, -1), kunQosh(b, -1)],
      kun7: [kunQosh(b, -6), b],
      oy: [oyBoshi(b), b],
      otganOy: [oyQosh(b, -1), oyOxiri(oyQosh(b, -1))],
    }[t];
    this.tez.set(t);
    this.dan.set(d);
    this.gacha.set(g);
    this.yukla();
  }

  sanaOzgardi(qaysi: 'dan' | 'gacha', v: string) {
    if (!v) return;
    (qaysi === 'dan' ? this.dan : this.gacha).set(v);
    this.tez.set(null);
    this.yukla();
  }

  guruhTanla(g: HisobotGuruhi) { this.guruh.set(g); this.yukla(); }
  operatorTanla(id: number | null) { this.operatorId.set(id); this.yukla(); }

  async yukla() {
    this.yuklanmoqda.set(true);
    try {
      const r = await ol(api.GET('/hisobot', {
        params: { query: { dan: this.dan(), gacha: this.gacha(), guruh: this.guruh(), operatorId: this.operatorId() ?? undefined } },
      }));
      this.natija.set(r);
      this.xato.set(null);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.yuklanmoqda.set(false);
    }
  }

  qiymat(q: HisobotQatori, u: Ustun): string {
    if (u.faqatJami && !q.jami) return '';
    const v = q[u.maydon] as number;
    return u.tur === 'litr' ? litr(v) : u.tur === 'pul' ? pul(v) : String(v);
  }

  /** Guruh nomi: server til-mustaqil beradi (kun "yyyy-MM-dd", oy "yyyy-MM", operator — ism); nomni lug'atdan chiqaramiz. */
  nomi(q: HisobotQatori): string {
    if (/^\d{4}-\d\d-\d\d$/.test(q.guruh)) return q.guruh.split('-').reverse().join('.');
    if (/^\d{4}-\d\d$/.test(q.guruh)) return `${this.til.t('OyNomlari').split(',')[+q.guruh.slice(5, 7) - 1]} ${q.guruh.slice(0, 4)}`;
    return q.guruh;
  }

  /** Haqiqiy .xlsx (write-excel-file, faqat eksportda yuklanadi): sarlavha qalin, pul formati "# ##0", litr "0.00". */
  async eksport() {
    const n = this.natija();
    if (!n || this.eksportBand()) return;
    this.eksportBand.set(true);
    try {
      const { default: writeXlsxFile } = await import('write-excel-file/browser');
      const t = (k: string) => this.til.t(k);
      const yoqBor = this.yoqilgiBor();
      const PUL = '#,##0', LITR = '#,##0.00';
      type Hujayra = { value?: string | number; type?: StringConstructor | NumberConstructor; format?: string; fontWeight?: 'bold'; backgroundColor?: string };
      const son = (v: number, f = PUL, qalin = false): Hujayra => ({ value: v, type: Number, format: f, ...(qalin ? { fontWeight: 'bold' as const } : {}) });
      const matn = (v: string, qalin = false): Hujayra => ({ value: v, type: String, ...(qalin ? { fontWeight: 'bold' as const } : {}) });
      const sarlavha = [
        t(this.guruh() === 'Operator' ? 'Operator' : this.guruh() === 'Kun' ? 'Sana' : 'Oy'),
        ...(yoqBor ? [t('Yoqilgi')] : []),
        ...USTUNLAR.map((u) => t(u.kalit)),
      ].map((x) => ({ ...matn(x, true), backgroundColor: '#E9F0FF' }));
      const format = { pul: PUL, litr: LITR, son: '0' };
      const qator = (q: HisobotQatori, nom: string, yoq: string | null, qalin: boolean): Hujayra[] => [
        matn(nom, qalin), ...(yoqBor ? [matn(yoq ?? '', qalin)] : []),
        ...USTUNLAR.map((u) => (u.faqatJami && !q.jami ? {} : son(q[u.maydon] as number, format[u.tur], qalin))),
      ];
      const malumot: Hujayra[][] = [sarlavha];
      for (const b of this.bolimlar()) {
        for (const y of b.yoqilgilar) malumot.push(qator(y, this.nomi(b.jami), y.yoqilgi ?? '', false));
        malumot.push(qator(b.jami, this.nomi(b.jami), yoqBor ? t('Jami') : null, yoqBor));
      }
      malumot.push(qator(n.jami, t('Jami'), null, true));
      const kenglik = [22, ...(yoqBor ? [12] : []), ...USTUNLAR.map((u) => (u.tur === 'son' ? 10 : 14))].map((width) => ({ width }));
      await writeXlsxFile(malumot as never, {
        sheet: t('Hisobotlar').slice(0, 31), columns: kenglik, stickyRowsCount: 1,
      } as never).toFile(`fuelcontrol-hisobot-${this.dan()}_${this.gacha()}.xlsx`);
      this.bildirish.korsat(t('FaylSaqlandi'));
    } catch (e) {
      this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    } finally {
      this.eksportBand.set(false);
    }
  }
}
