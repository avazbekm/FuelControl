import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Auth } from '../../core/auth';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { jonliYangila } from '../../core/malumot';
import { davomiylikSD, harflar, ishoraPul, kunQisqa, litr, litrQisqa, pul, sana, soat } from '../../core/format';
import { telefonFormat } from '../../core/telefon';
import type { AparatDto, NasiyaDto, NasiyalarXulosaDto, SmenaDto, SmenaTafsilotDto, YoqilgiTuriDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { YoqilgiPill, MashinaRaqami } from '../../ui/belgilar';
import { SonKiritish } from '../../ui/son-kiritish';
import { EnterKeyingi } from '../../ui/enter-keyingi';
import { NasiyaDialog } from '../../ui/dialoglar/nasiya-dialog';
import { QarzQaytdiDialog } from '../../ui/dialoglar/qarz-qaytdi-dialog';
import { XarajatDialog } from '../../ui/dialoglar/xarajat-dialog';
import { BakKirimDialog } from '../../ui/dialoglar/bak-kirim-dialog';

/**
 * Savdo (docs/dizayn/Main, SmenaOchish): ochiq smenada — boshlang'ich qoldiqlar, joriy smena, aparatlar, nasiya/xarajat/muddati o'tgan qarzlar;
 * yopiq bo'lsa — smenani ochish formasi (3 ta qo'lda qoldiq), oxirgi smena va aparatlar holati. Smenani yopish alohida sahifada (/savdo/yopish).
 */
@Component({
  selector: 'savdo-sahifa',
  imports: [RouterLink, FormsModule, Ikon, YoqilgiPill, MashinaRaqami, SonKiritish, EnterKeyingi, NasiyaDialog, QarzQaytdiDialog, XarajatDialog, BakKirimDialog],
  templateUrl: './savdo.html',
  styleUrl: './savdo.scss',
})
export class SavdoSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);

  protected readonly pul = pul;
  protected readonly tel = telefonFormat;
  protected readonly litr = litr;
  protected readonly litrQisqa = litrQisqa;
  protected readonly harflar = harflar;
  protected readonly ishora = ishoraPul;
  protected readonly kunOy = kunQisqa;

  protected readonly yuklandi = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected readonly joriy = signal<SmenaTafsilotDto | null>(null);
  protected readonly oxirgi = signal<SmenaTafsilotDto | null>(null);
  protected readonly aparatlar = signal<AparatDto[]>([]);
  private readonly yoqilgilar = signal<YoqilgiTuriDto[]>([]);
  protected readonly muddatiOtgan = signal<NasiyaDto[]>([]);
  /** Jami qarzdorlik qatori uchun (NasiyalarXulosaDto: faolQarz, faolSoni) — muddati o'tganlar ro'yxatidan mustaqil. */
  protected readonly qarzXulosa = signal<NasiyalarXulosaDto | null>(null);
  protected readonly muddatiOtganJami = computed(() => this.muddatiOtgan().reduce((a, n) => a + n.qoldiq, 0));

  protected readonly nasiyaOchiq = signal(false);
  protected readonly qaytishOchiq = signal(false);
  protected readonly xarajatOchiq = signal(false);
  protected readonly bakOchiq = signal(false);

  // Ochish formasi
  protected readonly qaytim = signal<number | null>(null);
  protected readonly terminal = signal<number | null>(null);
  protected readonly depozit = signal<number | null>(null);
  protected readonly band = signal(false);
  protected readonly ochishXato = signal<string | null>(null);
  protected readonly tayyorOchish = computed(() => this.qaytim() != null && this.terminal() != null && this.depozit() != null);

  /** Davomiylik/soat har 30 soniyada yangilanadi. */
  private readonly tik = signal(Date.now());
  protected readonly hozirSoat = computed(() => { this.tik(); return soat(new Date()); });

  constructor() {
    this.yukla();
    jonliYangila(() => this.yukla(true));
    const t = setInterval(() => this.tik.set(Date.now()), 30000);
    inject(DestroyRef).onDestroy(() => clearInterval(t));
  }

  async yukla(jim = false) {
    if (!jim) this.yuklandi.set(false);
    try {
      const [j, ap, yo, otgan, ox] = await Promise.all([
        this.server.joriySmena(), this.server.aparatlar(), this.server.yoqilgilar(), this.server.nasiyalar('otgan'), this.server.oxirgiYopilgan(),
      ]);
      this.joriy.set(j); this.aparatlar.set(ap); this.yoqilgilar.set(yo); this.oxirgi.set(ox);
      this.muddatiOtgan.set(otgan.royxat.filter((n) => n.holati === 'MuddatiOtgan').sort((a, b) => a.muddatgachaKun - b.muddatgachaKun || a.id - b.id)); // eng ko'p o'tgani birinchi
      this.qarzXulosa.set(otgan.xulosa);
      this.xato.set(null);
    } catch (e) {
      if (!jim) this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.yuklandi.set(true);
    }
  }

  protected rang(id: number): string { return this.yoqilgilar().find((y) => y.id === id)?.rang ?? '#2563EB'; }
  protected narx(id: number): number { return this.yoqilgilar().find((y) => y.id === id)?.narx ?? 0; }
  protected oxirgiKirim(a: AparatDto): string {
    return a.oxirgiKirimVaqti ? this.til.t('Bak_OxirgiKirim', kunQisqa(a.oxirgiKirimVaqti), litrQisqa(a.oxirgiKirimLitr ?? 0)) : this.til.t('Bak_KirimYoq');
  }
  protected oxirgiYopilganVaqt(): string {
    const o = this.oxirgi()?.smena.tugadi;
    return o ? `${kunQisqa(o)} ${soat(o)}` : '—';
  }

  protected soatMin(iso: string): string { return soat(iso); }
  protected kunSoatQisqa(iso: string): string { return `${kunQisqa(iso)} ${soat(iso)}`; }
  protected davomiylik(boshi: string): string {
    this.tik();
    const [h, m] = davomiylikSD(boshi, null);
    return this.til.t('Savdo_SoatDaqiqa', h, m);
  }
  protected davomiylikSoat(s: SmenaDto): number {
    return s.tugadi ? Math.round((sana(s.tugadi).getTime() - sana(s.boshlandi).getTime()) / 3600000) : 0;
  }

  protected muddatRang(n: NasiyaDto): string {
    return n.holati === 'MuddatiOtgan' ? 'qizil' : n.muddatgachaKun <= 3 ? 'sariq' : 'kok';
  }

  /** O'chirish: o'z yozuvi yoki `Smenalar` ruxsati bor foydalanuvchi (docs §7.4). */
  protected ochirish(muallifId: number): boolean {
    return this.auth.bor('Smenalar') || muallifId === this.auth.foydalanuvchi()?.id;
  }
  protected async nasiyaOchir(n: NasiyaDto) {
    if (!confirm(this.til.t('Savdo_OchirishTasdiq', n.mijozIsmi))) return;
    await this.amal(() => this.server.nasiyaOchir(n.id));
  }
  protected async qaytishOchir(id: number) {
    if (!confirm(this.til.t('Savdo_OchirishTasdiq', ''))) return;
    await this.amal(() => this.server.qaytishOchir(id));
  }
  protected async xarajatOchir(id: number) {
    if (!confirm(this.til.t('Savdo_OchirishTasdiq', ''))) return;
    await this.amal(() => this.server.xarajatOchir(id));
  }
  private async amal(f: () => Promise<void>) {
    try {
      await f();
      this.bildirish.korsat(this.til.t('Saqlandi'));
      await this.yukla(true);
    } catch (e) {
      this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    }
  }

  async smenaOch() {
    if (!this.tayyorOchish()) return;
    this.ochishXato.set(null);
    this.band.set(true);
    try {
      await this.server.smenaOch({ qaytim: this.qaytim()!, terminal: this.terminal()!, depozit: this.depozit()! });
      this.qaytim.set(null); this.terminal.set(null); this.depozit.set(null);
      this.bildirish.korsat(this.til.t('Savdo_SmenaOchildi'));
      await this.yukla(true);
    } catch (e) {
      this.ochishXato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
