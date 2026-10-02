import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { api, ol } from '../../api/api';
import { Til } from '../../core/til';
import { xatoMatni } from '../../core/bildirish';
import { kunSoat } from '../../core/format';
import type { AuditYozuvi } from '../../api/turlar';
import { Ikon } from '../../ui/ikon';

@Component({
  selector: 'audit-sahifa',
  imports: [FormsModule, Ikon],
  template: `
    <div class="sahifa">
      <div class="sahifa-bosh">
        <h1>{{ til.t('AuditJurnali') }}</h1>
        <p class="sarlavha-izoh">{{ til.t('AuditIzoh') }}</p>
      </div>
      <div class="qidiruv">
        <ikon nomi="search" [olcham]="18" />
        <input class="kiritish" type="search" [ngModel]="q()" (ngModelChange)="qidir($event)" [placeholder]="til.t('Qidirish')" [attr.aria-label]="til.t('Qidirish')" />
      </div>
      <section class="shisha karta">
        @if (xato(); as x) { <div class="bosh">{{ x }}</div> }
        @else if (yozuvlar(); as r) {
          <div class="royxat">
            @for (y of r; track y.id) {
              <div class="element">
                <span class="matnlar">
                  <span class="qator ora"><span class="asosiy-matn">{{ y.amal }}</span><span class="ikkilamchi kichik-matn son">{{ kunSoat(y.vaqt) }}</span></span>
                  <span class="ikkinchi tafsilot">{{ y.tafsilot }}</span>
                  <span class="ikkinchi"><ikon nomi="user" [olcham]="12" /> {{ y.kim }}</span>
                </span>
              </div>
            } @empty { <div class="bosh">{{ til.t('MalumotYoq') }}</div> }
          </div>
        } @else { <div class="skelet" style="height:200px"></div> }
      </section>
    </div>
  `,
  styles: `
    .qidiruv { position: relative; }
    .qidiruv ikon { position: absolute; left: 16px; top: 50%; transform: translateY(-50%); color: var(--matn-2); }
    .qidiruv .kiritish { padding-left: 44px; border-radius: 999px; }
    .tafsilot { white-space: normal !important; overflow-wrap: anywhere; color: var(--matn-3) !important; }
  `,
})
export class AuditSahifa {
  protected readonly til = inject(Til);
  protected readonly q = signal('');
  protected readonly yozuvlar = signal<AuditYozuvi[] | null>(null);
  protected readonly xato = signal<string | null>(null);
  protected readonly kunSoat = kunSoat;
  private taymer: ReturnType<typeof setTimeout> | undefined;

  constructor() {
    this.yukla();
  }

  qidir(v: string) {
    this.q.set(v);
    clearTimeout(this.taymer);
    this.taymer = setTimeout(() => this.yukla(), 350);
  }

  async yukla() {
    try {
      this.yozuvlar.set(await ol(api.GET('/audit', { params: { query: { q: this.q().trim() || undefined } } })));
      this.xato.set(null);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    }
  }
}
