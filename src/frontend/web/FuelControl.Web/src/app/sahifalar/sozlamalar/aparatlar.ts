import { Component, ElementRef, computed, effect, inject, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { litr, litrQisqa } from '../../core/format';
import type { AparatDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { SonKiritish } from '../../ui/son-kiritish';
import { SozlamalarXizmati } from './sozlamalar-xizmati';

/**
 * "Aparatlar" (docs/dizayn/SozlamalarAparatlar): aparat kartalari (pult ko'rsatkichi, bak qoldig'i) va o'ngda tahrirlash paneli.
 * Pult yoki bak qoldig'ini qo'lda tuzatish — sabab majburiy, auditga yoziladi. Yangi aparatda boshlang'ich pult va bak qoldig'i kiritiladi.
 */
@Component({
  selector: 'sozlamalar-aparatlar',
  imports: [FormsModule, Ikon, SonKiritish],
  template: `
    <div class="qator-panjara">
      <section class="shisha karta kartalar-karta asosiy" aria-labelledby="ap-sarlavha">
        <div class="karta-bosh sarlavha-qator">
          <div class="sarlavha-matn">
            <h2 id="ap-sarlavha">{{ til.t('Aparatlar') }}</h2>
            <span class="karta-izoh">{{ til.t('Sozlama_AparatIzoh') }}</span>
          </div>
          <button type="button" class="tugma asosiy" (click)="qosh()"><ikon nomi="plus" [olcham]="16" [qalinlik]="2.4" /> {{ til.t('AparatQoshish') }}</button>
        </div>
        <div class="aparat-panjara">
          @for (a of x.aparatlar(); track a.id) {
            <article class="aparat" [class.tanlangan]="!yangi() && a.id === tanlanganId()">
              <div class="aparat-bosh">
                <h3>{{ til.t('Aparat_Raqami', a.raqam) }}</h3>
                <span class="yoq-pill" [style.--r]="rang(a.yoqilgiTuriId)">{{ a.yoqilgiNomi }}</span>
                <button type="button" class="qalam" [attr.aria-label]="til.t('Aparat_Tahrirlash', a.raqam)" (click)="tanla(a)"><ikon nomi="edit" [olcham]="16" [qalinlik]="2" /></button>
              </div>
              <span class="juft"><span class="ikkilamchi">{{ til.t('Aparat_PultKorsatkichi') }}</span><b class="son">{{ litr(a.totalLitr) }} L</b></span>
              <span class="juft"><span class="ikkilamchi">{{ til.t('Aparat_BakQoldigi') }}</span><b class="son bak" [class.manfiy]="a.bakQoldiq < 0">{{ litrQisqa(a.bakQoldiq) }} L</b></span>
            </article>
          } @empty { <div class="bosh">{{ til.t('MalumotYoq') }}</div> }
        </div>
      </section>

      <section class="shisha panel yon" #panel aria-labelledby="ap-panel">
        <div class="panel-bosh">
          <h2 id="ap-panel">{{ yangi() ? til.t('YangiAparat') : til.t('Aparat_Tahrirlash', raqam() ?? 0) }}</h2>
          <span class="karta-izoh">{{ til.t(yangi() ? 'Aparat_YangiIzoh' : 'Aparat_AuditgaYoziladi') }}</span>
        </div>
        <form class="forma-ustun" (ngSubmit)="saqla()" autocomplete="off" novalidate>
          <div class="ikki-ustun juft-ustun">
            <div class="maydon">
              <label for="ap-raqam">{{ til.t('Raqam') }}</label>
              <son-kiritish [(qiymat)]="raqam" sinf="kiritish qalin-matn" inputId="ap-raqam" />
            </div>
            <div class="maydon">
              <label for="ap-yoq">{{ til.t('Yoqilgi') }}</label>
              <select id="ap-yoq" class="kiritish qalin-matn" name="yoq" [ngModel]="yoqilgiId()" (ngModelChange)="yoqilgiId.set(+$event)">
                @for (y of x.yoqilgilar(); track y.id) { <option [value]="y.id">{{ y.nomi }}</option> }
              </select>
            </div>
          </div>
          <div class="maydon">
            <label for="ap-pult">{{ til.t(yangi() ? 'Aparat_BoshlangichPult' : 'Aparat_PultLitr') }}</label>
            <son-kiritish [(qiymat)]="pult" [kasr]="2" sinf="kiritish pul" inputId="ap-pult" />
            <span class="yordam-matn">{{ til.t('Aparat_PultIzoh') }}</span>
          </div>
          <div class="maydon">
            <label for="ap-bak">{{ til.t(yangi() ? 'Aparat_BoshlangichBak' : 'Aparat_BakLitr') }}</label>
            <son-kiritish [(qiymat)]="bak" [kasr]="2" sinf="kiritish pul" inputId="ap-bak" />
            <span class="yordam-matn">{{ til.t('Aparat_BakIzoh') }}</span>
          </div>
          @if (!yangi()) {
            <div class="maydon">
              <label for="ap-sabab">{{ til.t('Aparat_TuzatishSababi') }}</label>
              <input id="ap-sabab" class="kiritish" name="sabab" [(ngModel)]="sabab" [class.xato]="sababKerak() && !sabab.trim() && urindi()" [placeholder]="til.t('Aparat_SababPlaceholder')" autocomplete="off" />
            </div>
          }
          @if (xato()) { <div class="xato-matn" role="alert">{{ xato() }}</div> }
          <div class="amallar">
            <button type="button" class="tugma" (click)="bekor()">{{ til.t('BekorQilish') }}</button>
            <button type="submit" class="tugma asosiy" [disabled]="band()">
              @if (band()) { <span class="aylanma"></span> } @else { <ikon nomi="check" [olcham]="16" [qalinlik]="2.4" /> } {{ til.t('Saqlash') }}
            </button>
          </div>
        </form>
      </section>
    </div>
  `,
  styles: `
    .qator-panjara { align-items: flex-start; }
    .kartalar-karta { padding: 20px; flex: 999 1 520px; }
    .sarlavha-qator { align-items: center; }
    .sarlavha-matn { display: flex; flex-direction: column; gap: 3px; }
    .sarlavha-matn h2 { font-size: 16px; }
    .aparat-panjara { display: grid; grid-template-columns: repeat(auto-fill, minmax(230px, 1fr)); gap: 12px; }
    .aparat { min-width: 0; padding: 12px 12px 14px 16px; display: flex; flex-direction: column; gap: 8px; border-radius: 20px; background: var(--plitka-fon); border: 1px solid var(--plitka-chegara); }
    .aparat.tanlangan { background: var(--malumot-fon); border: 2px solid #2F6BFF; padding: 11px 11px 13px 15px; }
    .aparat-bosh { display: flex; align-items: center; gap: 8px; }
    .aparat-bosh h3 { flex: 1; font-size: 16px; font-weight: 800; }
    .yoq-pill {
      display: inline-flex; align-items: center; padding: 4px 11px; border-radius: 999px; font-size: 12px; font-weight: 700;
      color: color-mix(in srgb, var(--r, #2563EB) 72%, light-dark(#0B1530, #fff)); background: color-mix(in srgb, var(--r, #2563EB) 16%, light-dark(#fff, #0C1120));
    }
    .qalam { width: 44px; height: 44px; flex: none; margin: -6px -4px -6px 0; border-radius: 999px; display: inline-flex; align-items: center; justify-content: center; color: var(--matn-3); cursor: pointer; background: transparent; border: 0; }
    .juft { display: flex; justify-content: space-between; gap: 10px; font-size: 13px; }
    .juft b { font-weight: 800; }
    .juft .bak { color: var(--yashil); }
    .juft .bak.manfiy { color: var(--qizil); }

    .panel { flex: 1 1 360px; min-width: 0; padding: 22px; display: flex; flex-direction: column; gap: 14px; background: var(--dialog); border-radius: 26px; box-shadow: 0 24px 60px rgba(20, 40, 90, 0.18), 0 2px 6px rgba(20, 40, 90, 0.08); scroll-margin-top: 12px; }
    .panel-bosh { display: flex; flex-direction: column; gap: 3px; }
    .panel-bosh h2 { font-size: 19px; font-weight: 800; }
    .juft-ustun { grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); }
    :host ::ng-deep .kiritish.qalin-matn { font-weight: 700; }
    .amallar { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: 10px; padding-top: 2px; }
  `,
})
export class AparatlarBolimi {
  protected readonly til = inject(Til);
  protected readonly x = inject(SozlamalarXizmati);
  private readonly bildirish = inject(Bildirish);
  private readonly panel = viewChild<ElementRef<HTMLElement>>('panel');

  protected readonly litr = litr;
  protected readonly litrQisqa = litrQisqa;
  protected readonly tanlanganId = signal<number | null>(null);
  protected readonly yangi = signal(false);
  protected readonly raqam = signal<number | null>(null);
  protected readonly yoqilgiId = signal(0);
  protected readonly pult = signal<number | null>(null);
  protected readonly bak = signal<number | null>(null);
  protected sabab = '';
  protected readonly band = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected readonly urindi = signal(false);

  /** Maydonlar asl (saqlangan) qiymatdan farq qiladimi. */
  private ozgargan(): boolean {
    const a = this.tanlangan();
    return !!a && (this.raqam() !== a.raqam || this.yoqilgiId() !== a.yoqilgiTuriId || this.pult() !== a.totalLitr || this.bak() !== a.bakQoldiq || !!this.sabab);
  }

  private readonly tanlangan = computed<AparatDto | null>(() => this.x.aparatlar().find((a) => a.id === this.tanlanganId()) ?? null);
  /** Pult yoki bak qoldig'i asl qiymatdan farq qilsa, tuzatish sababi majburiy. */
  protected readonly sababKerak = computed(() => {
    const a = this.tanlangan();
    if (this.yangi() || !a) return false;
    return this.pult() !== a.totalLitr || this.bak() !== a.bakQoldiq;
  });

  constructor() {
    // Ma'lumot yuklangach (yoki aparatlar o'zgarganda) tanlangan aparat bo'lmasa — birinchisi tanlanadi.
    effect(() => {
      const r = this.x.aparatlar();
      untracked(() => {
        if (this.yangi()) return;
        if (!r.length) { this.tanlanganId.set(null); return; }
        const a = r.find((q) => q.id === this.tanlanganId()) ?? r[0];
        // Foydalanuvchi yozayotganda (boshqa qurilmadan yangilanish kelsa) kiritilgan qiymatlar o'chib ketmasin.
        if (a.id !== this.tanlanganId() || !this.ozgargan()) this.maydonlar(a);
      });
    });
  }

  protected rang(yoqilgiTuriId: number): string {
    return this.x.yoqilgilar().find((y) => y.id === yoqilgiTuriId)?.rang ?? '#2563EB';
  }

  private maydonlar(a: AparatDto) {
    this.tanlanganId.set(a.id);
    this.raqam.set(a.raqam);
    this.yoqilgiId.set(a.yoqilgiTuriId);
    this.pult.set(a.totalLitr);
    this.bak.set(a.bakQoldiq);
    this.sabab = '';
    this.xato.set(null);
    this.urindi.set(false);
  }

  protected tanla(a: AparatDto) {
    this.yangi.set(false);
    this.maydonlar(a);
    this.panelgaOt();
  }

  protected qosh() {
    this.yangi.set(true);
    this.tanlanganId.set(null);
    this.raqam.set(Math.max(0, ...this.x.aparatlar().map((a) => a.raqam)) + 1);
    this.yoqilgiId.set(this.x.yoqilgilar()[0]?.id ?? 0);
    this.pult.set(0);
    this.bak.set(0);
    this.sabab = '';
    this.xato.set(null);
    this.urindi.set(false);
    this.panelgaOt();
  }

  protected bekor() {
    this.yangi.set(false);
    const a = this.tanlangan() ?? this.x.aparatlar()[0];
    if (a) this.maydonlar(a);
  }

  /** Telefonda panel kartalar ostida — ko'rinishga keltiramiz. */
  private panelgaOt() {
    if (!matchMedia('(min-width: 900px)').matches) setTimeout(() => this.panel()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' }), 60);
  }

  protected async saqla() {
    this.urindi.set(true);
    const raqam = this.raqam();
    const pult = this.pult();
    const bak = this.bak();
    if (!raqam || raqam <= 0 || !this.yoqilgiId()) return this.xato.set(this.til.t('Xato_Maydon'));
    if (pult == null || pult < 0) return this.xato.set(this.til.t('Aparat_XatoPult'));
    if (bak == null || bak < 0) return this.xato.set(this.til.t('Aparat_XatoBakManfiy'));
    if (this.x.aparatlar().some((a) => a.raqam === raqam && a.id !== this.tanlanganId())) return this.xato.set(this.til.t('Aparat_XatoRaqamBand'));
    this.xato.set(null);
    this.band.set(true);
    try {
      if (this.yangi()) {
        await this.x.aparatYarat({ raqam, yoqilgiTuriId: this.yoqilgiId(), boshlangichTotalLitr: pult, boshlangichBakQoldiq: bak });
        this.yangi.set(false);
        const yangiAparat = this.x.aparatlar().find((a) => a.raqam === raqam);
        if (yangiAparat) this.maydonlar(yangiAparat);
      } else {
        const a = this.tanlangan();
        if (!a) return;
        const o = pult !== a.totalLitr || bak !== a.bakQoldiq;
        if (o && !this.sabab.trim()) return this.xato.set(this.til.t('Aparat_XatoSabab'));
        await this.x.aparatTahrirla(a.id, {
          raqam, yoqilgiTuriId: this.yoqilgiId(),
          totalLitr: pult !== a.totalLitr ? pult : null, bakQoldiq: bak !== a.bakQoldiq ? bak : null, sabab: o ? this.sabab.trim() : null,
        });
        this.sabab = '';
      }
      this.bildirish.korsat(this.til.t('Saqlandi'));
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
