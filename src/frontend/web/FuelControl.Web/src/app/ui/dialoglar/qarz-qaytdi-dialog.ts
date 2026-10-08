import { Component, computed, effect, inject, input, model, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Auth } from '../../core/auth';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { harflar, kunOy, pul } from '../../core/format';
import { telefonFormat } from '../../core/telefon';
import type { NasiyaDto, SmenaDto, TolovTuri } from '../../api/model';
import { Ikon } from '../ikon';
import { Oyna } from '../oyna';
import { EnterKeyingi } from '../enter-keyingi';
import { SonKiritish } from '../son-kiritish';
import { MashinaRaqami } from '../belgilar';

const USULLAR: TolovTuri[] = ['Naqd', 'Plastik', 'Depozit'];

/** "Qarz qaytdi" dialogi (docs/dizayn/NasiyaQaytdi): qarzdorni topadi, to'liq yoki qisman qaytishni yozadi. */
@Component({
  selector: 'qarz-qaytdi-dialog',
  imports: [FormsModule, Ikon, Oyna, SonKiritish, MashinaRaqami, EnterKeyingi],
  template: `
    <oyna [(ochiq)]="ochiq" [sarlavha]="til.t('Nasiya_QarzQaytdi')" [tagsarlavha]="smenaMatn()" ikon="undo" ikonRang="yashil">
      <form class="forma-ustun" enterKeyingi (ngSubmit)="saqla()" autocomplete="off" novalidate>
        <div class="maydon">
          <label for="qq-qidir">{{ til.t('Nasiya_QarzdorniToping') }}</label>
          <div class="qidiruv-oram">
            <span class="qidiruv-ikon"><ikon nomi="search" [olcham]="17" [qalinlik]="2" /></span>
            <input id="qq-qidir" class="kiritish qidiruv" name="q" type="search" [ngModel]="qidiruv()" (ngModelChange)="qidirish($event)" [placeholder]="til.t('Nasiya_Qidiruv')" autocomplete="off" data-avto />
          </div>
        </div>

        @if (tanlangan(); as n) {
          <div class="qarzdor-karta" [class.otgan]="n.holati === 'MuddatiOtgan'">
            <div class="qator">
              <span class="avatar katta">{{ harflar(n.mijozIsmi) }}</span>
              <div class="bosh-joy qarzdor-matn">
                <span class="ism">{{ n.mijozIsmi }}</span>
                <span class="qator orala kichik-matn ikkilamchi tel-qator">{{ tel(n.telefon) }} <mashina-raqami [qiymat]="n.mashinaRaqami" /></span>
              </div>
              <span class="pill katta" [class]="muddatRang(n)">{{ muddatMatn(n) }}</span>
            </div>
            <div class="to-rtlik">
              <div class="kichik-plitka"><span class="kap">{{ til.t('Nasiya_Qarz') }}</span><span class="qiymat">{{ pul(n.summa) }}</span></div>
              <div class="kichik-plitka"><span class="kap">{{ til.t('Nasiya_Qaytgan') }}</span><span class="qiymat">{{ pul(n.qaytgan) }}</span></div>
              <div class="kichik-plitka"><span class="kap">{{ til.t('Nasiya_Qoldiq') }}</span><span class="qiymat">{{ pul(n.qoldiq) }}</span></div>
              <div class="kichik-plitka" [class.otgan]="n.holati === 'MuddatiOtgan'"><span class="kap">{{ til.t('Nasiya_MuddatYorliq') }}</span><span class="qiymat" [class.qizil]="n.holati === 'MuddatiOtgan'">{{ kunOy(n.muddat) }}</span></div>
            </div>
            <span class="ikkilamchi kichik-matn">{{ yozilganMatn(n) }}</span>
          </div>

          <div class="maydon">
            <label for="qq-summa">{{ til.t('Nasiya_QaytarilganSumma') }}</label>
            <div class="qator orala summa-qator">
              <son-kiritish [(qiymat)]="summa" sinf="kiritish katta" inputId="qq-summa" />
              <button type="button" class="tugma" (click)="summa.set(n.qoldiq)">{{ til.t('Nasiya_Toliq', pul(n.qoldiq)) }}</button>
            </div>
          </div>
          <div class="maydon">
            <span class="yorliq" id="qq-usul-l">{{ til.t('Nasiya_QandayTolandi') }}</span>
            <div class="segment" role="group" aria-labelledby="qq-usul-l">
              @for (u of usullar; track u) {
                <button type="button" [class.tanlangan]="usul() === u" [attr.aria-pressed]="usul() === u" (click)="usul.set(u)">{{ til.t('Tolov_' + u) }}</button>
              }
            </div>
          </div>
          <div class="qoladi" aria-live="polite" [class.xato]="oshiq()">
            <span class="matnlar">
              <span class="sarlavha">{{ til.t('Nasiya_QoladiganQarz') }}</span>
              <span class="izoh">{{ qoldiqIzoh() }}</span>
            </span>
            <span class="summa">{{ qoldiq() }}</span>
          </div>
          @if (smenaTanlash()) {
            <label class="belgi"><input type="checkbox" name="sh" [(ngModel)]="smenaHisobiga" /> {{ til.t('Nasiya_SmenaHisobiga') }}</label>
          }
        } @else {
          <div class="royxat qarzdorlar">
            @for (n of royxat(); track n.id) {
              <button type="button" class="element" (click)="tanla(n)">
                <span class="avatar">{{ harflar(n.mijozIsmi) }}</span>
                <span class="matnlar">
                  <span class="asosiy-matn">{{ n.mijozIsmi }}</span>
                  <span class="ikkinchi">{{ tel(n.telefon) }} · {{ n.mashinaRaqami }}</span>
                </span>
                <span class="ong"><b>{{ pul(n.qoldiq) }}</b><span class="pill" [class]="muddatRang(n)">{{ muddatMatn(n) }}</span></span>
              </button>
            } @empty {
              <div class="bosh">{{ yuklandi() ? til.t('Nasiya_QarzdorYoq') : til.t('Yuklanmoqda') }}</div>
            }
          </div>
        }

        <div class="malumot-blok"><ikon nomi="info" [olcham]="16" [qalinlik]="2" /><span>{{ til.t('Nasiya_QaytishIzoh') }}</span></div>
        @if (xato()) { <div class="xato-matn" role="alert">{{ xato() }}</div> }
        <div class="amallar">
          <button type="button" class="tugma" (click)="ochiq.set(false)">{{ til.t('BekorQilish') }}</button>
          <button type="submit" class="tugma asosiy" [disabled]="band() || !tanlangan()">
            @if (band()) { <span class="aylanma"></span> } @else { <ikon nomi="check" [olcham]="16" [qalinlik]="2.4" /> } {{ til.t('Saqlash') }}
          </button>
        </div>
      </form>
    </oyna>
  `,
  styles: `
    .qidiruv-oram { position: relative; display: flex; align-items: center; }
    .qidiruv-ikon { position: absolute; left: 16px; display: inline-flex; color: var(--matn-2); pointer-events: none; }
    .kiritish.qidiruv { padding-left: 44px; }
    .qarzdor-karta { display: flex; flex-direction: column; gap: 12px; padding: 16px; border-radius: 22px; background: #fff; border: 2px solid #2F6BFF; box-shadow: 0 10px 26px rgba(47, 107, 255, 0.14); color: #0B1530; }
    .avatar.katta { width: 40px; height: 40px; }
    .qarzdor-matn { display: flex; flex-direction: column; gap: 5px; min-width: 0; }
    .qarzdor-matn .ism { font-size: 15px; font-weight: 700; }
    .tel-qator { gap: 6px; font-size: 12.5px; }
    .to-rtlik { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 8px; }
    .kichik-plitka { padding: 10px 12px; border-radius: 14px; background: #F2F6FF; display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .kichik-plitka.otgan { background: #FFF0F0; }
    .kichik-plitka .kap { font-size: 10.5px; font-weight: 700; letter-spacing: 0.8px; text-transform: uppercase; color: #4F607D; }
    .kichik-plitka .qiymat { font-size: 15px; font-weight: 800; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .qarzdor-karta .ikkilamchi { color: #4F607D; }
    .summa-qator { gap: 10px; flex-wrap: wrap; }
    :host ::ng-deep .summa-qator .kiritish { flex: 1 1 260px; width: auto; min-width: 0; }
    .qoladi { display: flex; align-items: center; justify-content: space-between; gap: 12px; padding: 14px 16px; border-radius: 18px; background: rgba(217, 245, 230, 0.7); border: 1px solid #9FE3BF; }
    .qoladi.xato { background: var(--kamomat-fon); border-color: var(--kamomat-chegara); }
    .qoladi .matnlar { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .qoladi .sarlavha { font-size: 13.5px; font-weight: 700; }
    .qoladi .izoh { font-size: 12.5px; color: var(--matn-3); }
    .qoladi .summa { font-size: 22px; font-weight: 800; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .qarzdorlar { max-height: 280px; overflow-y: auto; }
    .qarzdorlar .element { color: inherit; }
    @media (max-width: 480px) { .to-rtlik { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
  `,
})
export class QarzQaytdiDialog {
  protected readonly til = inject(Til);
  private readonly auth = inject(Auth);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);

  readonly ochiq = model(false);
  /** Oldindan tanlangan nasiya (Nasiyalar jadvalidagi "Qaytdi"). */
  readonly nasiyaId = input<number | null>(null);
  readonly saqlandi = output<void>();

  protected readonly pul = pul;
  protected readonly tel = telefonFormat;
  protected readonly kunOy = kunOy;
  protected readonly harflar = harflar;
  protected readonly usullar = USULLAR;
  protected readonly joriy = signal<SmenaDto | null>(null);
  protected readonly smenaMatn = computed(() => (this.joriy() ? this.til.t('Smena_Operator', this.joriy()!.id, this.joriy()!.operatorIsmi) : ''));
  protected readonly qidiruv = signal('');
  protected readonly royxat = signal<NasiyaDto[]>([]);
  protected readonly yuklandi = signal(false);
  protected readonly tanlangan = signal<NasiyaDto | null>(null);
  protected readonly summa = signal<number | null>(null);
  protected readonly usul = signal<TolovTuri>('Naqd');
  protected smenaHisobiga = true;
  protected readonly band = signal(false);
  protected readonly xato = signal<string | null>(null);
  private qidirTaymer: ReturnType<typeof setTimeout> | undefined;

  /** Smena hisobiga yozish tanlovi faqat boshliqqa (Smenalar ruxsati) va ochiq smena bo'lsa ko'rinadi; operator uchun doim smena hisobiga. */
  protected readonly smenaTanlash = computed(() => !!this.joriy() && this.auth.bor('Smenalar'));
  protected readonly oshiq = computed(() => { const n = this.tanlangan(); const s = this.summa(); return !!n && s != null && s > n.qoldiq; });
  protected readonly qoldiq = computed(() => {
    const n = this.tanlangan(); const s = this.summa();
    return n && s != null && s > 0 && s <= n.qoldiq ? pul(n.qoldiq - s) : '—';
  });
  protected readonly qoldiqIzoh = computed(() => {
    const n = this.tanlangan(); const s = this.summa();
    if (!n || s == null || s <= 0) return this.til.t('Nasiya_SummaniYozing');
    if (s > n.qoldiq) return this.til.t('Nasiya_QarzdanKop');
    if (s === n.qoldiq) return this.til.t('Nasiya_ToliqYopiladi');
    return n.holati === 'MuddatiOtgan' ? this.til.t('Nasiya_MuddatiOtganQoladi', kunOy(n.muddat)) : this.til.t('Nasiya_MuddatigaQoladi', kunOy(n.muddat));
  });

  constructor() {
    effect(() => {
      if (!this.ochiq()) return;
      const id = this.nasiyaId();
      untracked(() => this.boshlash(id));
    });
  }

  private async boshlash(id: number | null) {
    this.qidiruv.set(''); this.summa.set(null); this.usul.set('Naqd'); this.xato.set(null); this.tanlangan.set(null); this.royxat.set([]); this.yuklandi.set(false);
    this.joriy.set(null);
    this.server.joriySmena().then((t) => { this.joriy.set(t?.smena ?? null); this.smenaHisobiga = !!t; }).catch(() => undefined);
    try {
      if (id != null) {
        const t = await this.server.nasiya(id);
        this.tanla(t.nasiya);
        this.qidiruv.set(t.nasiya.mijozIsmi);
      } else {
        await this.yukla('');
      }
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    }
  }

  private async yukla(q: string) {
    const r = await this.server.nasiyalar('faol', q);
    const faol = r.royxat.filter((n) => n.qoldiq > 0);
    // Qidiruv bo'lsa — server tartibi (aynan/boshidan mos kelganlar avval); bo'sh bo'lsa — eng ko'p o'tgani birinchi.
    this.royxat.set((q.trim() ? faol : faol.sort((a, b) => a.muddatgachaKun - b.muddatgachaKun || a.id - b.id)).slice(0, 12));
    this.yuklandi.set(true);
  }

  protected qidirish(q: string) {
    this.qidiruv.set(q);
    this.tanlangan.set(null);
    clearTimeout(this.qidirTaymer);
    this.qidirTaymer = setTimeout(() => this.yukla(q).catch(() => undefined), 200);
  }

  protected tanla(n: NasiyaDto) {
    this.tanlangan.set(n);
    this.summa.set(n.qoldiq);
    this.xato.set(null);
  }

  protected muddatRang(n: NasiyaDto): string {
    return n.holati === 'MuddatiOtgan' ? 'qizil' : n.muddatgachaKun <= 1 ? 'sariq' : 'kok';
  }
  protected muddatMatn(n: NasiyaDto): string {
    if (n.holati === 'MuddatiOtgan') return this.til.t('Nasiya_KunOtdi', Math.abs(n.muddatgachaKun));
    if (n.muddatgachaKun === 0) return this.til.t('Nasiya_Bugun');
    if (n.muddatgachaKun === 1) return this.til.t('Nasiya_Ertaga');
    return this.til.t('Nasiya_KunQoldi', n.muddatgachaKun);
  }
  protected yozilganMatn(n: NasiyaDto): string {
    const s = this.til.t('Nasiya_YozilganSatr', kunOy(n.yozildi.slice(0, 10)), n.smenaId, n.operatorIsmi);
    return n.izoh ? `${s} · “${n.izoh}”` : s;
  }

  async saqla() {
    const n = this.tanlangan();
    const s = this.summa();
    if (!n) return;
    if (!s || s <= 0) return this.xato.set(this.til.t('Nasiya_XatoSumma'));
    if (s > n.qoldiq) return this.xato.set(this.til.t('Nasiya_QarzdanKop'));
    this.xato.set(null);
    this.band.set(true);
    try {
      await this.server.nasiyaQaytishi(n.id, { summa: s, usul: this.usul(), smenaHisobiga: !!this.joriy() && (!this.smenaTanlash() || this.smenaHisobiga), izoh: null });
      this.ochiq.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
      this.saqlandi.emit();
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
