import { Component, computed, inject, input, output, signal, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { api, ol } from '../api/api';
import { Til } from '../core/til';
import { Bildirish, xatoMatni } from '../core/bildirish';
import { pul, litr, sonOl } from '../core/format';
import type { Aparat, Sotuv } from '../api/turlar';
import { Ikon } from './ikon';
import { TolovKiritish, TolovTanlovi, Qismlar, tolovlarYasa, tanlovniTikla, BOSH_QISMLAR } from './tolov-kiritish';

/** Sotuvni tahrirlash yoki bekor qilish dialogi. Sabab majburiy — audit jurnaliga tushadi. */
@Component({
  selector: 'sotuv-tahrir',
  imports: [FormsModule, Ikon, TolovKiritish],
  template: `
    <div class="parda" (click)="yopildi.emit(false)">
      <form class="dialog" (click)="$event.stopPropagation()" (ngSubmit)="saqla()" role="dialog" aria-modal="true">
        <div class="dialog-bosh">
          <h2>{{ rejim() === 'tahrir' ? til.t('SotuvniTahrirlashSarlavha', s().id) : til.t('SotuvniBekorQilish') }}</h2>
          <button type="button" class="tugma ikonli kichik" (click)="yopildi.emit(false)" aria-label="×"><ikon nomi="x" [olcham]="18" /></button>
        </div>

        <div class="shisha hozirgi">
          <span class="ikkilamchi kichik-matn">{{ til.t('Hozirgi') }}</span>
          <div class="qator ora">
            <span>{{ til.t('Ap') }} {{ s().aparatRaqami }} · {{ s().yoqilgiNomi }} · {{ litr(s().litr) }} {{ til.t('L') }}</span>
            <b class="son">{{ pul(s().summa) }}</b>
          </div>
          <span class="ikkilamchi kichik-matn">{{ s().operatorIsmi }} · {{ tolovMatni() }}</span>
        </div>

        @if (rejim() === 'tahrir') {
          <div class="maydon">
            <label for="t-aparat">{{ til.t('Aparat') }}</label>
            <select id="t-aparat" class="kiritish" name="aparat" [ngModel]="aparatId()" (ngModelChange)="aparatId.set(+$event)">
              @for (a of aparatlar(); track a.id) { <option [value]="a.id">{{ til.t('Ap') }} {{ a.raqam }} · {{ a.yoqilgiNomi }}</option> }
            </select>
          </div>
          <div class="maydon">
            <label for="t-summa">{{ til.t('SummaSom') }}</label>
            <input id="t-summa" class="kiritish son" name="summa" inputmode="numeric" (focus)="$any($event.target).select()" [ngModel]="summaMatn()" (ngModelChange)="summaMatn.set($event)" />
          </div>
          <tolov-kiritish [summa]="summa()" [(turi)]="turi" [(qismlar)]="qismlar" />
        }

        <div class="maydon">
          <label for="t-sabab">{{ til.t('Sabab') }}</label>
          <textarea id="t-sabab" class="kiritish" name="sabab" [(ngModel)]="sabab" [placeholder]="til.t('SababMisol')"></textarea>
          <span class="ikkilamchi kichik-matn">{{ til.t('SababMajbur') }}</span>
        </div>

        @if (xato()) { <div class="xato-matn" role="alert">{{ xato() }}</div> }

        <div class="amallar">
          <button type="button" class="tugma" (click)="yopildi.emit(false)">{{ til.t('Yopish') }}</button>
          <button type="submit" class="tugma" [class.asosiy]="rejim() === 'tahrir'" [class.xavfli]="rejim() === 'bekor'" [class.toliq]="rejim() === 'bekor'" [disabled]="band()">
            @if (band()) { <span class="aylanma"></span> }
            {{ rejim() === 'tahrir' ? til.t('Saqlash') : til.t('BekorQilish') }}
          </button>
        </div>
      </form>
    </div>
  `,
  styles: `.hozirgi { padding: 12px 14px; border-radius: 16px; display: flex; flex-direction: column; gap: 4px; box-shadow: none; }`,
})
export class SotuvTahrir implements OnInit {
  protected readonly til = inject(Til);
  private readonly bildirish = inject(Bildirish);
  readonly s = input.required<Sotuv>();
  readonly rejim = input.required<'tahrir' | 'bekor'>();
  readonly aparatlar = input<Aparat[]>([]);
  readonly yopildi = output<boolean>();

  protected readonly aparatId = signal(0);
  protected readonly summaMatn = signal('');
  protected readonly summa = computed(() => Math.round(sonOl(this.summaMatn()) ?? 0));
  protected readonly turi = signal<TolovTanlovi>('Naqd');
  protected readonly qismlar = signal<Qismlar>(BOSH_QISMLAR());
  protected sabab = '';
  protected readonly band = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected readonly pul = pul;
  protected readonly litr = litr;
  protected readonly tolovMatni = computed(() =>
    this.s().tolovlar.map((t) => `${this.til.t(t.turi)} ${pul(t.summa)}`).join(' + '));

  ngOnInit() {
    const s = this.s();
    this.aparatId.set(s.aparatId);
    this.summaMatn.set(String(s.summa));
    const t = tanlovniTikla(s.tolovlar);
    this.turi.set(t.turi);
    this.qismlar.set(t.qismlar);
  }

  async saqla() {
    this.xato.set(null);
    if (!this.sabab.trim()) return this.xato.set(this.til.t('Xato_Sabab'));
    const id = this.s().id;
    try {
      if (this.rejim() === 'tahrir') {
        if (this.summa() <= 0) return this.xato.set(this.til.t('Xato_Summa'));
        const tolovlar = tolovlarYasa(this.turi(), this.qismlar(), this.summa());
        if (!tolovlar) return this.xato.set(this.til.t('Xato_AralashYigindi'));
        this.band.set(true);
        await ol(api.PUT('/sotuvlar/{id}', { params: { path: { id } }, body: { aparatId: this.aparatId(), summa: this.summa(), tolovlar, sabab: this.sabab.trim() } }));
      } else {
        this.band.set(true);
        await ol(api.POST('/sotuvlar/{id}/bekor', { params: { path: { id } }, body: { sabab: this.sabab.trim() } }));
      }
      this.bildirish.korsat(this.til.t('Saqlandi'));
      this.yopildi.emit(true);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
