import { Component, ElementRef, computed, effect, inject, input, signal, untracked, viewChild } from '@angular/core';
import { Auth } from '../../core/auth';
import { Til } from '../../core/til';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { jonliYangila } from '../../core/malumot';
import { harflar, isoKun, ishoraPul, kun, oyBoshi, oyQosh, pul } from '../../core/format';
import type { FoydalanuvchiDto, HarakatTuri, HisobHarakatiDto, OperatorHisobDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { Oyna } from '../../ui/oyna';
import { SonKiritish } from '../../ui/son-kiritish';
import { FormsModule } from '@angular/forms';

const PILL: Record<HarakatTuri, string> = { Maosh: 'yashil', Avans: 'sariq', Kamomat: 'qizil', Ortiqcha: 'kok', Tolov: 'kul' };

/**
 * Operatorlar hisobi (docs/dizayn/Operatorlar): chapda operator kartalari, o'ngda tanlangani — oy ko'rsatkichlari va hisob harakatlari.
 * `/operatorlar` — Operatorlar ruxsati bilan (ro'yxat); `/hisobim` — ruxsatsiz operator faqat o'zinikini ko'radi. Tanlash: `?id=`.
 */
@Component({
  selector: 'operatorlar-sahifa',
  imports: [FormsModule, Ikon, Oyna, SonKiritish],
  template: `
    <div class="sahifa">
      <header class="sahifa-bosh">
        <div class="sarlavha">
          <h1>{{ til.t(royxatKorinadi() ? 'OperatorlarHisobi' : 'MeningHisobim') }}</h1>
          <span class="sarlavha-izoh">{{ til.t('Operator_Izoh') }}</span>
        </div>
      </header>

      @if (xato(); as x) {
        <div class="shisha karta bosh">{{ x }}<br /><br />
          <button type="button" class="tugma" (click)="boshla()"><ikon nomi="refresh" [olcham]="16" /> {{ til.t('Yangilash') }}</button>
        </div>
      } @else if (!yuklandi()) {
        <div class="qator-panjara">
          <div class="op-royxat"><div class="shisha skelet" style="height: 120px"></div><div class="shisha skelet" style="height: 120px"></div></div>
          <div class="op-tafsilot"><div class="shisha skelet" style="height: 320px"></div></div>
        </div>
      } @else {
        <div class="qator-panjara">
          @if (royxatKorinadi()) {
            <div class="op-royxat">
              @for (o of operatorlar(); track o.id) {
                <button type="button" class="op-karta shisha" [class.tanlangan]="o.id === tanlanganId()" [attr.aria-pressed]="o.id === tanlanganId()" (click)="tanla(o.id)">
                  <span class="qator op-bosh">
                    <span class="avatar katta">{{ harflar(o.toliqIsm) }}</span>
                    <span class="op-matn">
                      <span class="ism">{{ o.toliqIsm }}</span>
                      <span class="maosh">{{ til.t('Operator_Maosh', pul(o.oylikMaosh)) }}</span>
                    </span>
                    @if (smenadagi() === o.id) { <span class="pill yashil">{{ til.t('Operator_Smenada') }}</span> }
                  </span>
                  <span class="chiziq"></span>
                  <span class="qator ora qoldiq-qator">
                    <span class="maosh">{{ til.t('JoriyQoldiq') }}</span>
                    <span class="qoldiq son" [class.manfiy]="(qoldiqlar()[o.id] ?? 0) < 0">{{ pul(qoldiqlar()[o.id] ?? 0) }}</span>
                  </span>
                </button>
              } @empty { <div class="shisha karta bosh">{{ til.t('MalumotYoq') }}</div> }
            </div>
          }

          <div class="op-tafsilot" #tafsilotEl>
            @if (h(); as h) {
              <div class="tafsilot-bosh">
                <div class="tafsilot-matn">
                  <h2>{{ h.operatorIsmi }}</h2>
                  <span class="oy-qator">
                    <button type="button" class="tugma ikonli kichik shaffof" (click)="oyAlmashtir(-1)" [attr.aria-label]="til.t('Operator_OldingiOy')"><ikon nomi="chevronLeft" [olcham]="16" [qalinlik]="2.2" /></button>
                    <span class="oy-nomi">{{ til.t('Operator_OyKorsatkichlari', oyNomi()) }}</span>
                    <button type="button" class="tugma ikonli kichik shaffof" (click)="oyAlmashtir(1)" [disabled]="joriyOymi()" [attr.aria-label]="til.t('Operator_KeyingiOy')"><ikon nomi="chevronRight" [olcham]="16" [qalinlik]="2.2" /></button>
                  </span>
                </div>
                <div class="amallar-qator">
                  @if (auth.bor('Eksport')) {
                    <button type="button" class="tugma" [disabled]="eksportBand()" (click)="eksport()"><ikon nomi="file" [olcham]="16" /> {{ til.t('HisobVaraqa') }}</button>
                  }
                  @if (auth.bor('AvansBerish') && royxatKorinadi()) {
                    <button type="button" class="tugma asosiy" (click)="dialogOch()"><ikon nomi="plus" [olcham]="16" [qalinlik]="2.4" /> {{ til.t('Operator_AvansTolov') }}</button>
                  }
                </div>
              </div>

              <div class="kpi-panjara">
                <div class="kpi-karta shisha">
                  <span class="yorliq">{{ til.t('Smenalar') }}</span>
                  <span class="kpi-qiymat">{{ h.oySmenalar }}</span>
                  <span class="kpi-izoh">{{ til.t('Operator_YopilganOchiq', yopilganSoni(), ochiqSoni()) }}</span>
                </div>
                <div class="kpi-karta shisha">
                  <span class="yorliq">{{ til.t('Savdo') }}</span>
                  <span class="kpi-qiymat son">{{ pul(h.oySavdo) }}</span>
                  <span class="kpi-izoh">{{ til.t('Operator_YopilganSmenalarda') }}</span>
                </div>
                <div class="kpi-karta shisha">
                  <span class="yorliq">{{ til.t('Avans') }}</span>
                  <span class="kpi-qiymat son sariq">{{ pul(avans().summa) }}</span>
                  <span class="kpi-izoh">{{ til.t('Operator_Marta', avans().soni) }}</span>
                </div>
                <div class="kpi-karta shisha">
                  <span class="yorliq">{{ til.t('Kamomat') }}</span>
                  <span class="kpi-qiymat son kamomat">{{ pul(kamomat().summa) }}</span>
                  <span class="kpi-izoh">{{ kamomat().smenalar }}</span>
                </div>
                <div class="kpi-karta shisha">
                  <span class="yorliq">{{ til.t('Ortiqcha') }}</span>
                  <span class="kpi-qiymat son yashil">{{ pul(ortiqcha()) }}</span>
                  <span class="kpi-izoh">{{ til.t('Operator_ShuOy') }}</span>
                </div>
              </div>

              <section class="shisha harakatlar" aria-labelledby="op-harakat">
                <h2 id="op-harakat">{{ til.t('Operator_HisobHarakatlari') }}</h2>
                <!-- Keng ekran: jadval -->
                <div class="faqat-keng jadval-oram">
                  <div class="jadval-ich">
                    <div class="satr bosh-satr">
                      <span>{{ til.t('Sana') }}</span><span>{{ til.t('Turi') }}</span><span>{{ til.t('Izoh') }}</span><span>{{ til.t('KimYozdi') }}</span><span class="o">{{ til.t('Summa') }}</span>
                    </div>
                    @for (r of harakatlar(); track r.id) {
                      <div class="satr">
                        <span>{{ kun(r.sana) }}</span>
                        <span><span class="pill" [class]="pill[r.turi]">{{ til.t(r.turi) }}</span></span>
                        <span>{{ r.izoh }}</span>
                        <span class="ikkilamchi">{{ r.kimYozdi }}</span>
                        <span class="o summa" [class.qizil-summa]="r.summa < 0" [class.yashil-summa]="r.summa >= 0">{{ ishora(r.summa) }}</span>
                      </div>
                    } @empty { <div class="bosh">{{ til.t('MalumotYoq') }}</div> }
                  </div>
                </div>
                <!-- Telefon: ro'yxat -->
                <div class="faqat-tor">
                  @for (r of harakatlar(); track r.id) {
                    <div class="tel-satr">
                      <div class="qator ora">
                        <span class="pill" [class]="pill[r.turi]">{{ til.t(r.turi) }}</span>
                        <span class="summa" [class.qizil-summa]="r.summa < 0" [class.yashil-summa]="r.summa >= 0">{{ ishora(r.summa) }}</span>
                      </div>
                      <span class="tel-izoh">{{ r.izoh }}</span>
                      <span class="ikkilamchi kichik-matn">{{ kun(r.sana) }} · {{ r.kimYozdi }}</span>
                    </div>
                  } @empty { <div class="bosh">{{ til.t('MalumotYoq') }}</div> }
                </div>
              </section>
            } @else {
              <div class="shisha skelet" style="height: 320px"></div>
            }
          </div>
        </div>
      }
    </div>

    <oyna [(ochiq)]="dialog" [sarlavha]="til.t('Operator_AvansTolov')" [tagsarlavha]="h()?.operatorIsmi ?? ''" ikon="cash" ikonRang="sariq">
      <form class="forma-ustun" (ngSubmit)="pulBer()" autocomplete="off" novalidate>
        <div class="maydon">
          <span class="yorliq" id="op-tur-l">{{ til.t('Turi') }}</span>
          <div class="segment" role="group" aria-labelledby="op-tur-l">
            <button type="button" [class.tanlangan]="dTuri() === 'Avans'" [attr.aria-pressed]="dTuri() === 'Avans'" (click)="dTuri.set('Avans')">{{ til.t('Avans') }}</button>
            <button type="button" [class.tanlangan]="dTuri() === 'Tolov'" [attr.aria-pressed]="dTuri() === 'Tolov'" (click)="dTuri.set('Tolov')">{{ til.t('Tolov') }}</button>
          </div>
        </div>
        <div class="maydon">
          <label for="op-summa">{{ til.t('Operator_SummaSom') }}</label>
          <son-kiritish [(qiymat)]="dSumma" sinf="kiritish katta" inputId="op-summa" [avto]="true" />
        </div>
        <div class="maydon">
          <label for="op-izoh">{{ til.t('Izoh') }}</label>
          <input id="op-izoh" class="kiritish" name="izoh" [(ngModel)]="dIzoh" [placeholder]="til.t('Operator_IzohPlaceholder')" autocomplete="off" />
        </div>
        <div class="malumot-blok"><ikon nomi="info" [olcham]="16" [qalinlik]="2" /><span>{{ til.t('Operator_AvansIzoh') }}</span></div>
        @if (dXato()) { <div class="xato-matn" role="alert">{{ dXato() }}</div> }
        <div class="amallar">
          <button type="button" class="tugma" (click)="dialog.set(false)">{{ til.t('BekorQilish') }}</button>
          <button type="submit" class="tugma asosiy" [disabled]="band()">
            @if (band()) { <span class="aylanma"></span> } @else { <ikon nomi="check" [olcham]="16" [qalinlik]="2.4" /> } {{ til.t('Saqlash') }}
          </button>
        </div>
      </form>
    </oyna>
  `,
  styles: `
    :host { display: block; }
    .op-royxat { flex: 1 1 320px; max-width: 100%; min-width: 0; display: flex; flex-direction: column; gap: 14px; }
    .op-tafsilot { flex: 999 1 560px; min-width: 0; display: flex; flex-direction: column; gap: 16px; scroll-margin-top: 12px; }
    .op-karta {
      text-align: left; font: inherit; color: var(--matn); cursor: pointer; padding: 16px; display: flex; flex-direction: column; gap: 12px; width: 100%;
      box-shadow: 0 18px 48px rgba(28, 54, 110, 0.10), inset 0 1px 0 rgba(255, 255, 255, 0.9);
    }
    .op-karta.tanlangan { border: 2px solid #2F6BFF; box-shadow: 0 12px 28px rgba(47, 107, 255, 0.18), inset 0 1px 0 rgba(255, 255, 255, 0.9); }
    .op-bosh { width: 100%; gap: 12px; }
    .avatar.katta { width: 44px; height: 44px; font-size: 14px; }
    .op-matn { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 2px; }
    .ism { font-size: 14.5px; font-weight: 700; }
    .maosh { font-size: 12.5px; color: var(--matn-2); }
    .chiziq { height: 1px; width: 100%; background: var(--chiziq); }
    .qoldiq-qator { width: 100%; align-items: baseline; }
    .qoldiq { font-size: 18px; font-weight: 800; color: var(--yashil); }
    .qoldiq.manfiy { color: var(--qizil); }

    .tafsilot-bosh { display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 12px; }
    .tafsilot-matn { display: flex; flex-direction: column; gap: 3px; min-width: 0; }
    .tafsilot-matn h2 { font-size: 19px; font-weight: 800; }
    .oy-qator { display: inline-flex; align-items: center; gap: 4px; font-size: 13px; color: var(--matn-2); }
    .oy-qator .tugma.ikonli { width: 26px; height: 26px; min-height: 26px; }
    @media (pointer: coarse) { .oy-qator .tugma.ikonli { width: 44px; height: 44px; min-height: 44px; } }
    .oy-nomi { min-width: 0; }
    .amallar-qator { display: flex; flex-wrap: wrap; gap: 10px; }

    .kpi-panjara { display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 12px; }
    .kpi-karta { padding: 14px 16px; display: flex; flex-direction: column; gap: 4px; border-radius: 22px; box-shadow: 0 14px 36px rgba(28, 54, 110, 0.10), inset 0 1px 0 rgba(255, 255, 255, 0.9); min-width: 0; }
    .kpi-qiymat { font-size: 20px; font-weight: 800; }
    .kpi-qiymat.sariq { color: var(--sariq); }
    .kpi-qiymat.kamomat { color: light-dark(#C42127, var(--qizil)); }
    .kpi-qiymat.yashil { color: var(--yashil); }
    .kpi-izoh { font-size: 12px; color: var(--matn-2); }

    .harakatlar { padding: 18px 16px 8px; display: flex; flex-direction: column; gap: 8px; }
    .harakatlar h2 { margin: 0 4px 6px; }
    .jadval-ich { min-width: 640px; display: flex; flex-direction: column; font-variant-numeric: tabular-nums; }
    .satr { display: grid; grid-template-columns: 96px 110px minmax(200px, 2fr) minmax(110px, 1fr) minmax(110px, 1fr); gap: 12px; align-items: center; padding: 12px 10px; border-bottom: 1px solid var(--chiziq); font-size: 13.5px; }
    .satr:last-child { border-bottom: 0; }
    .satr.bosh-satr { padding: 0 10px 8px; border-bottom: 1px solid var(--chiziq); font-size: 11px; font-weight: 700; letter-spacing: 0.8px; text-transform: uppercase; color: var(--matn-2); }
    .satr .o { text-align: right; }
    .summa { font-weight: 800; font-variant-numeric: tabular-nums; }
    .yashil-summa { color: var(--yashil); }
    .qizil-summa { color: var(--b-qizil); }
    .tel-satr { display: flex; flex-direction: column; gap: 6px; padding: 12px 4px; border-bottom: 1px solid var(--chiziq); }
    .tel-satr:last-child { border-bottom: 0; }
    .tel-izoh { font-size: 14px; line-height: 1.4; }
    @media (max-width: 699px) { .amallar-qator { width: 100%; } .amallar-qator .tugma { flex: 1 1 auto; } }
  `,
})
export class OperatorlarSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);

  /** `?id=` — tanlangan operator (faqat ro'yxatli rejimda). */
  readonly id = input<string>();
  private readonly tafsilotEl = viewChild<ElementRef<HTMLElement>>('tafsilotEl');

  protected readonly pul = pul;
  protected readonly kun = kun;
  protected readonly harflar = harflar;
  protected readonly pill = PILL;

  /** Ro'yxat faqat Operatorlar ruxsati bilan; aks holda faqat o'z hisobi. */
  protected readonly royxatKorinadi = computed(() => this.auth.bor('Operatorlar'));
  protected readonly operatorlar = signal<FoydalanuvchiDto[]>([]);
  protected readonly qoldiqlar = signal<Record<number, number>>({});
  protected readonly smenadagi = signal<number | null>(null);
  protected readonly yuklandi = signal(false);
  protected readonly xato = signal<string | null>(null);
  private readonly tanlangan = signal<number | null>(null);
  protected readonly tanlanganId = computed(() => (this.royxatKorinadi() ? this.tanlangan() ?? this.operatorlar()[0]?.id ?? null : this.auth.foydalanuvchi()?.id ?? null));

  protected readonly oy = signal(oyBoshi(isoKun()));
  protected readonly h = signal<OperatorHisobDto | null>(null);
  protected readonly eksportBand = signal(false);

  protected readonly dialog = signal(false);
  protected readonly dTuri = signal<'Avans' | 'Tolov'>('Avans');
  protected readonly dSumma = signal<number | null>(null);
  protected dIzoh = '';
  protected readonly band = signal(false);
  protected readonly dXato = signal<string | null>(null);

  // Brauzerlarda o'zbek oy nomlari (Intl) ko'pincha yo'q — lug'atdan olamiz.
  protected readonly oyNomi = computed(() => `${this.til.t('OyNomlari').split(',')[+this.oy().slice(5, 7) - 1]} ${this.oy().slice(0, 4)}`);
  protected readonly joriyOymi = computed(() => this.oy() === oyBoshi(isoKun()));
  protected readonly harakatlar = computed<HisobHarakatiDto[]>(() => [...(this.h()?.harakatlar ?? [])].sort((a, b) => a.sana.localeCompare(b.sana) || a.id - b.id));
  protected readonly avans = computed(() => {
    const r = this.harakatlar().filter((x) => x.turi === 'Avans');
    return { summa: -r.reduce((s, x) => s + x.summa, 0), soni: r.length };
  });
  protected readonly kamomat = computed(() => {
    const r = this.harakatlar().filter((x) => x.turi === 'Kamomat');
    const raqamlar = r.map((x) => /#(\d+)/.exec(x.izoh)?.[1]).filter((x): x is string => !!x);
    return {
      summa: -r.reduce((s, x) => s + x.summa, 0),
      smenalar: raqamlar.length ? this.til.t('Operator_SmenaRaqami', [...new Set(raqamlar)].map((x) => '#' + x).join(', ')) : this.til.t('Operator_ShuOy'),
    };
  });
  protected readonly ortiqcha = computed(() => this.harakatlar().filter((x) => x.turi === 'Ortiqcha').reduce((s, x) => s + x.summa, 0));
  protected readonly ochiqSoni = signal(0);
  protected readonly yopilganSoni = computed(() => Math.max(0, (this.h()?.oySmenalar ?? 0) - this.ochiqSoni()));

  constructor() {
    this.boshla();
    effect(() => {
      const q = this.id();
      if (q && this.royxatKorinadi()) untracked(() => this.tanlangan.set(+q));
    });
    effect(() => { this.tanlanganId(); this.oy(); untracked(() => this.hisobYukla()); });
    jonliYangila(() => { this.hisobYukla(); this.royxatYukla(); }, (e) => e.turi === 'SmenaOzgardi' || e.turi === 'AparatOzgardi');
  }

  async boshla() {
    this.xato.set(null);
    await this.royxatYukla();
    this.yuklandi.set(true);
  }

  private async royxatYukla() {
    try {
      const joriy = await this.server.joriySmena();
      this.smenadagi.set(joriy?.smena.operatorId ?? null);
      if (this.royxatKorinadi()) {
        const ops = await this.server.operatorlar();
        this.operatorlar.set(ops);
        const q: Record<number, number> = {};
        await Promise.all(ops.map(async (o) => { q[o.id] = (await this.server.operatorHisob(o.id, this.oy())).qoldiq; }));
        this.qoldiqlar.set(q);
      }
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    }
  }

  private async hisobYukla() {
    const id = this.tanlanganId();
    if (!id) return;
    try {
      const r = await this.server.operatorHisob(id, this.oy());
      this.h.set(r);
      this.ochiqSoni.set(this.smenadagi() === id && this.oy() === oyBoshi(isoKun()) ? 1 : 0);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    }
  }

  protected tanla(id: number) {
    this.tanlangan.set(id);
    // Telefonda tafsilot ro'yxat ostida — ko'rinishga keltiramiz.
    if (!matchMedia('(min-width: 900px)').matches) setTimeout(() => this.tafsilotEl()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }), 60);
  }

  protected oyAlmashtir(n: number) {
    const yangi = oyQosh(this.oy(), n);
    if (yangi > oyBoshi(isoKun())) return;
    this.oy.set(yangi);
  }

  protected ishora(n: number) { return ishoraPul(n); }

  protected dialogOch() {
    this.dTuri.set('Avans'); this.dSumma.set(null); this.dIzoh = ''; this.dXato.set(null);
    this.dialog.set(true);
  }

  protected async pulBer() {
    const summa = this.dSumma();
    const id = this.tanlanganId();
    if (!summa || summa <= 0 || !id) return this.dXato.set(this.til.t('Xato_Summa'));
    this.band.set(true);
    try {
      await this.server.avansBer(id, Math.round(summa), this.dTuri(), this.dIzoh.trim());
      this.dialog.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
      await Promise.all([this.hisobYukla(), this.royxatYukla()]);
    } catch (e) {
      this.dXato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }

  /** Hisob-varaqa: tanlangan oy harakatlari .xlsx (write-excel-file, faqat eksportda yuklanadi). */
  protected async eksport() {
    const h = this.h();
    if (!h || this.eksportBand()) return;
    this.eksportBand.set(true);
    try {
      const { default: writeXlsxFile } = await import('write-excel-file/browser');
      const t = (k: string) => this.til.t(k);
      const fayl = `fuelcontrol-${h.operatorIsmi.replace(/\s+/g, '-')}-${this.oy().slice(0, 7)}.xlsx`;
      type Hujayra = { value?: string | number; type?: StringConstructor | NumberConstructor; format?: string; fontWeight?: 'bold'; backgroundColor?: string };
      const matn = (v: string, qalin = false): Hujayra => ({ value: v, type: String, ...(qalin ? { fontWeight: 'bold' as const } : {}) });
      const son = (v: number, qalin = false): Hujayra => ({ value: v, type: Number, format: '#,##0', ...(qalin ? { fontWeight: 'bold' as const } : {}) });
      const sarlavha = [t('Sana'), t('Turi'), t('Izoh'), t('KimYozdi'), t('Summa')].map((x) => ({ ...matn(x, true), backgroundColor: '#E9F0FF' }));
      const malumot: Hujayra[][] = [
        [matn(h.operatorIsmi, true), matn(this.oyNomi())],
        [matn(t('OylikMaosh')), son(h.oylikMaosh)],
        [],
        sarlavha,
        ...this.harakatlar().map((r) => [matn(kun(r.sana)), matn(t(r.turi)), matn(r.izoh), matn(r.kimYozdi), son(r.summa)]),
        [matn(t('Jami'), true), {}, {}, {}, son(h.oyJami, true)],
        [matn(t('JoriyQoldiq'), true), {}, {}, {}, son(h.qoldiq, true)],
      ];
      await writeXlsxFile(malumot as never, {
        sheet: t('HisobVaraqa').slice(0, 31), columns: [{ width: 14 }, { width: 14 }, { width: 46 }, { width: 20 }, { width: 16 }], stickyRowsCount: 4,
      } as never).toFile(fayl);
      this.bildirish.korsat(t('FaylSaqlandi'));
      void this.server.auditEksport('Hisob-varaqa', `${h.operatorIsmi}, ${this.oy().slice(0, 7)} — ${fayl}`);
    } catch (e) {
      this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
    } finally {
      this.eksportBand.set(false);
    }
  }
}
