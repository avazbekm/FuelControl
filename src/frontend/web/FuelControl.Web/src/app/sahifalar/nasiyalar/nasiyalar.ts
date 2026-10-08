import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { Til } from '../../core/til';
import { Auth } from '../../core/auth';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { jonliYangila } from '../../core/malumot';
import { kunQisqa, kunToliq, pul } from '../../core/format';
import { telefonFormat } from '../../core/telefon';
import { qidiruvMos } from '../../core/qidiruv';
import type { NasiyaDto, NasiyalarXulosaDto, SmenaDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { MashinaRaqami } from '../../ui/belgilar';
import { NasiyaDialog } from '../../ui/dialoglar/nasiya-dialog';
import { QarzQaytdiDialog } from '../../ui/dialoglar/qarz-qaytdi-dialog';
import { KpiKarta } from '../boshqaruv/kpi-karta';
import { NasiyaTafsilotDialog } from './nasiya-tafsilot-dialog';

type Filtr = 'hammasi' | 'faol' | 'otgan' | 'yopilgan';

/** "Alisher Karimov" → "Alisher K." (jadvalda qisqa muallif). */
function qisqaIsm(ism: string): string {
  const q = ism.trim().split(/\s+/);
  return q.length > 1 ? `${q[0]} ${q[1][0]}.` : q[0] ?? '';
}

/** Nasiyalar (docs/dizayn/Nasiyalar): qarz xulosasi, filtr chiplari + qidiruv, jadval (telefonda kartalar), qaytish va tafsilot dialoglari. */
@Component({
  selector: 'nasiyalar-sahifa',
  imports: [Ikon, MashinaRaqami, KpiKarta, NasiyaDialog, QarzQaytdiDialog, NasiyaTafsilotDialog],
  templateUrl: './nasiyalar.html',
  styleUrl: './nasiyalar.scss',
})
export class NasiyalarSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);

  protected readonly pul = pul;
  protected readonly tel = telefonFormat;
  protected readonly kunOy = (iso: string) => kunQisqa(iso);
  protected readonly kunToliq = kunToliq;
  protected readonly qisqaIsm = qisqaIsm;

  protected readonly hammasi = signal<NasiyaDto[]>([]);
  protected readonly xulosa = signal<NasiyalarXulosaDto | null>(null);
  protected readonly joriy = signal<SmenaDto | null>(null);
  protected readonly yuklandi = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected readonly filtr = signal<Filtr>('hammasi');
  protected readonly qidiruv = signal('');
  /** Tor ekranda (telefon) qidiruv maslahati qisqa — sig'adi. */
  protected readonly tor = signal(matchMedia('(max-width: 520px)').matches);

  protected readonly nasiyaOchiq = signal(false);
  protected readonly qaytishOchiq = signal(false);
  protected readonly qaytishId = signal<number | null>(null);
  protected readonly tafsilotOchiq = signal(false);
  protected readonly tafsilotId = signal<number | null>(null);

  /** Faol = qarzi bor hammasi (muddati o'tganlar ham), Muddati o'tgan = faqat o'tganlar, Yopilgan = qoldiq 0 (docs §7.10). */
  protected readonly filtrlar = computed(() => {
    const h = this.hammasi();
    return [
      { k: 'hammasi' as Filtr, kalit: 'Nasiyalar_Hammasi', soni: h.length },
      { k: 'faol' as Filtr, kalit: 'Nasiyalar_Faol', soni: h.filter((n) => n.holati !== 'Yopilgan').length },
      { k: 'otgan' as Filtr, kalit: 'Nasiyalar_MuddatiOtgan', soni: h.filter((n) => n.holati === 'MuddatiOtgan').length },
      { k: 'yopilgan' as Filtr, kalit: 'Nasiyalar_Yopilgan', soni: h.filter((n) => n.holati === 'Yopilgan').length },
    ];
  });

  protected readonly royxat = computed(() => {
    const f = this.filtr();
    const q = this.qidiruv();
    const guruh = (n: NasiyaDto) => (n.holati === 'MuddatiOtgan' ? 0 : n.holati === 'Faol' ? 1 : 2);
    return this.hammasi()
      .filter((n) => f === 'hammasi' || (f === 'faol' && n.holati !== 'Yopilgan') || (f === 'otgan' && n.holati === 'MuddatiOtgan') || (f === 'yopilgan' && n.holati === 'Yopilgan'))
      .filter((n) => qidiruvMos(n, q))
      .sort((a, b) => guruh(a) - guruh(b) || a.muddat.localeCompare(b.muddat) || a.id - b.id);
  });

  /** Nasiya yozish — ochiq smena kerak; qaytish — ochiq smena yoki boshliq (smenadan tashqari). */
  protected readonly yozishMumkin = computed(() => this.auth.bor('NasiyaYozish') && !!this.joriy());
  protected readonly qaytishMumkin = computed(() => this.auth.bor('QarzQaytdi') && (!!this.joriy() || this.auth.bor('Smenalar')));
  protected readonly smenaKerak = computed(() => this.yuklandi() && !this.joriy() && (this.auth.bor('NasiyaYozish') || this.auth.bor('QarzQaytdi')));

  constructor() {
    const mq = matchMedia('(max-width: 520px)');
    const fn = () => this.tor.set(mq.matches);
    mq.addEventListener('change', fn);
    inject(DestroyRef).onDestroy(() => mq.removeEventListener('change', fn));
    this.yukla();
    jonliYangila(() => this.yukla(true), (h) => h.turi === 'NasiyaOzgardi' || h.turi === 'SmenaOzgardi');
  }

  protected async yukla(jim = false) {
    try {
      const [r, j] = await Promise.all([this.server.nasiyalar(), this.server.joriySmena()]);
      this.hammasi.set(r.royxat);
      this.xulosa.set(r.xulosa);
      this.joriy.set(j?.smena ?? null);
      this.xato.set(null);
    } catch (e) {
      if (!jim) this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.yuklandi.set(true);
    }
  }

  protected qaytishOch(id: number | null) {
    this.qaytishId.set(id);
    this.qaytishOchiq.set(true);
  }

  protected tafsilotOch(id: number) {
    this.tafsilotId.set(id);
    this.tafsilotOchiq.set(true);
  }

  /** Tafsilot dialogidagi "Qarz qaytdi": tafsilot yopilib, qaytish dialogi shu nasiya bilan ochiladi. */
  protected tafsilotdanQaytish(id: number) {
    this.tafsilotOchiq.set(false);
    this.qaytishOch(id);
  }

  protected belgiRang(n: NasiyaDto): string {
    return n.holati === 'Yopilgan' ? 'yashil' : n.holati === 'MuddatiOtgan' ? 'qizil' : n.muddatgachaKun <= 3 ? 'sariq' : 'kok';
  }

  protected belgiMatn(n: NasiyaDto): string {
    if (n.holati === 'Yopilgan') return this.til.t('Nasiyalar_YopilganSana', n.yopildi ? kunQisqa(n.yopildi) : '');
    if (n.holati === 'MuddatiOtgan') return this.til.t('Nasiya_KunOtdi', Math.abs(n.muddatgachaKun));
    if (n.muddatgachaKun === 0) return this.til.t('Nasiya_Bugun');
    if (n.muddatgachaKun === 1) return this.til.t('Nasiya_Ertaga');
    return this.til.t('Nasiya_KunQoldi', n.muddatgachaKun);
  }

  protected yozilgan(n: NasiyaDto): string {
    return kunQisqa(n.yozildi);
  }
}
