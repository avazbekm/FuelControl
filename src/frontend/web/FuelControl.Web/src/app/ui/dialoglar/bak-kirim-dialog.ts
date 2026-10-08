import { Component, computed, effect, inject, input, model, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { hozirToshkent, kunOy, litrQisqa, toshkentdan } from '../../core/format';
import type { AparatDto } from '../../api/model';
import { Ikon } from '../ikon';
import { Oyna } from '../oyna';
import { EnterKeyingi } from '../enter-keyingi';
import { SonKiritish } from '../son-kiritish';

/** "Bakka kirim" dialogi (docs/dizayn/BakKirim): zavoddan kelgan yoqilg'i, litrda. Pult ko'rsatkichi o'zgarmaydi. */
@Component({
  selector: 'bak-kirim-dialog',
  imports: [FormsModule, Ikon, Oyna, SonKiritish, EnterKeyingi],
  template: `
    <oyna [(ochiq)]="ochiq" [sarlavha]="til.t('Bak_Kirim')" [tagsarlavha]="til.t('Bak_KirimIzoh')" ikon="truck" ikonRang="yashil">
      <form class="forma-ustun" enterKeyingi (ngSubmit)="saqla()" autocomplete="off" novalidate>
        <div class="maydon">
          <label for="bk-aparat">{{ til.t('Bak_QaysiAparat') }}</label>
          <select id="bk-aparat" class="kiritish aparat-tanlov" name="aparat" [ngModel]="aparatId()" (ngModelChange)="aparatId.set(+$event)">
            @for (a of aparatlar(); track a.id) { <option [value]="a.id">{{ til.t('Aparat_Raqami', a.raqam) }} · {{ a.yoqilgiNomi }}</option> }
          </select>
        </div>
        <div class="ikki-ustun plitkalar">
          <div class="tanlov-plitka">
            <span class="yorliq">{{ til.t('Bak_HozirgiQoldiq') }}</span>
            <span class="qiymat">{{ litrQisqa(tanlangan()?.bakQoldiq ?? 0) }} <small>L</small></span>
            <span class="izoh">{{ oxirgiKirim() }}</span>
          </div>
          <div class="tanlov-plitka keyin" aria-live="polite">
            <span class="yorliq yashil-yorliq">{{ til.t('Bak_KirimdanKeyin') }}</span>
            <span class="qiymat">{{ keyin() }} <small>L</small></span>
            <span class="izoh">{{ til.t('Bak_PultOzgarmaydi') }}</span>
          </div>
        </div>
        <div class="maydon">
          <label for="bk-litr">{{ til.t('Bak_KelganMiqdor') }}</label>
          <son-kiritish [(qiymat)]="litr" [kasr]="2" sinf="kiritish katta" inputId="bk-litr" [avto]="true" />
        </div>
        <div class="ikki-ustun">
          <div class="maydon">
            <label for="bk-hujjat">{{ til.t('Bak_Hujjat') }}</label>
            <input id="bk-hujjat" class="kiritish" name="hujjat" [(ngModel)]="hujjat" [placeholder]="til.t('Bak_HujjatPlaceholder')" autocomplete="off" />
          </div>
          <div class="maydon">
            <label for="bk-vaqt">{{ til.t('Bak_KelganVaqt') }}</label>
            <input id="bk-vaqt" class="kiritish" name="vaqt" type="datetime-local" [(ngModel)]="vaqt" />
          </div>
        </div>
        <div class="malumot-blok"><ikon nomi="info" [olcham]="16" [qalinlik]="2" /><span>{{ til.t('Bak_Tushuntirish') }}</span></div>
        @if (xato()) { <div class="xato-matn" role="alert">{{ xato() }}</div> }
        <div class="amallar">
          <button type="button" class="tugma" (click)="ochiq.set(false)">{{ til.t('BekorQilish') }}</button>
          <button type="submit" class="tugma yashil" [disabled]="band()">
            @if (band()) { <span class="aylanma"></span> } @else { <ikon nomi="plus" [olcham]="16" [qalinlik]="2.4" /> } {{ til.t('Bak_KirimniSaqlash') }}
          </button>
        </div>
      </form>
    </oyna>
  `,
  styles: `
    .aparat-tanlov { font-weight: 600; }
    .tanlov-plitka { padding: 14px 16px; border-radius: 18px; background: var(--malumot-fon); display: flex; flex-direction: column; gap: 3px; min-width: 0; }
    .tanlov-plitka.keyin { background: rgba(217, 245, 230, 0.75); border: 1px solid #9FE3BF; }
    .yashil-yorliq { color: #0E6B42; }
    .tanlov-plitka .qiymat { font-size: 22px; font-weight: 800; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .tanlov-plitka small { font-size: 13px; font-weight: 600; color: var(--matn-2); }
    .tanlov-plitka .izoh { font-size: 12px; color: var(--matn-2); }
    .tanlov-plitka.keyin .izoh { color: #2E3D5C; }
    .plitkalar { grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); }
  `,
})
export class BakKirimDialog {
  protected readonly til = inject(Til);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);

  readonly ochiq = model(false);
  /** Oldindan tanlangan aparat (Boshqaruv/Savdo kartasidan). */
  readonly oldindanAparat = input<number | null>(null);
  readonly saqlandi = output<void>();

  protected readonly litrQisqa = litrQisqa;
  protected readonly aparatlar = signal<AparatDto[]>([]);
  protected readonly aparatId = signal(0);
  protected readonly litr = signal<number | null>(null);
  protected hujjat = '';
  protected vaqt = '';
  protected readonly band = signal(false);
  protected readonly xato = signal<string | null>(null);

  protected readonly tanlangan = computed(() => this.aparatlar().find((a) => a.id === this.aparatId()) ?? null);
  protected readonly keyin = computed(() => {
    const a = this.tanlangan(); const l = this.litr();
    return a && l != null && l > 0 ? litrQisqa(Math.round((a.bakQoldiq + l) * 100) / 100) : '—';
  });
  protected readonly oxirgiKirim = computed(() => {
    const a = this.tanlangan();
    return a?.oxirgiKirimVaqti ? this.til.t('Bak_OxirgiKirim', kunOy(a.oxirgiKirimVaqti.slice(0, 10)), litrQisqa(a.oxirgiKirimLitr ?? 0)) : this.til.t('Bak_KirimYoq');
  });

  constructor() {
    effect(() => {
      if (!this.ochiq()) return;
      const oldin = this.oldindanAparat();
      untracked(() => this.boshlash(oldin));
    });
  }

  private async boshlash(oldin: number | null) {
    this.litr.set(null); this.hujjat = ''; this.xato.set(null);
    const h = hozirToshkent();
    this.vaqt = `${h.kun}T${h.soat}`;
    try {
      const r = await this.server.aparatlar();
      this.aparatlar.set(r);
      this.aparatId.set(oldin && r.some((a) => a.id === oldin) ? oldin : (r[0]?.id ?? 0));
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    }
  }

  async saqla() {
    const l = this.litr();
    if (!l || l <= 0) return this.xato.set(this.til.t('Bak_Xato'));
    const a = this.tanlangan();
    if (!a) return;
    this.xato.set(null);
    this.band.set(true);
    try {
      const [kun, soat] = (this.vaqt || '').split('T');
      await this.server.bakKirim(a.id, { litr: l, vaqt: kun && soat ? toshkentdan(kun, soat.slice(0, 5)) : null, hujjat: this.hujjat.trim() || null });
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

