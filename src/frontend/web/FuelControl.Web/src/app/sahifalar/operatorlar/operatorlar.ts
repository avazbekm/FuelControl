import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { api, ol } from '../../api/api';
import { Til } from '../../core/til';
import { xatoMatni } from '../../core/bildirish';
import { Malumot } from '../../core/malumot';
import { pul, isoKun, oyBoshi } from '../../core/format';
import type { OperatorHisob } from '../../api/turlar';
import { Ikon } from '../../ui/ikon';

@Component({
  selector: 'operatorlar-sahifa',
  imports: [RouterLink, Ikon],
  template: `
    <div class="sahifa">
      <div class="sahifa-bosh">
        <h1>{{ til.t('OperatorlarHisobi') }}</h1>
        <p class="sarlavha-izoh">{{ til.t('OperatorlarIzoh') }}</p>
      </div>
      @if (xato(); as x) { <div class="shisha karta bosh">{{ x }}</div> }
      @else if (royxat(); as r) {
        <div class="panjara ikki">
          @for (h of r; track h.operatorId) {
            <a class="shisha karta op-karta" [routerLink]="['/operatorlar', h.operatorId]">
              <div class="qator">
                <span class="avatar">{{ h.operatorIsmi.slice(0, 1) }}</span>
                <div class="bosh-joy">
                  <div class="ism">{{ h.operatorIsmi }}</div>
                  <div class="ikkilamchi kichik-matn">{{ til.t('OylikMaosh') }}: {{ pul(h.oylikMaosh) }} {{ til.t('Som') }}</div>
                </div>
                <ikon nomi="chevronRight" [olcham]="18" />
              </div>
              <div class="qator ora">
                <span class="ikkilamchi kichik-matn">{{ til.t('JoriyQoldiq') }}</span>
                <b class="son qoldiq" [class.qizil]="h.qoldiq < 0" [class.yashil]="h.qoldiq > 0">{{ pul(h.qoldiq) }} {{ til.t('Som') }}</b>
              </div>
            </a>
          } @empty { <div class="shisha karta bosh">{{ til.t('MalumotYoq') }}</div> }
        </div>
      } @else {
        <div class="panjara ikki">@for (i of [1, 2]; track i) { <div class="shisha karta"><div class="skelet" style="height:80px"></div></div> }</div>
      }
    </div>
  `,
  styles: `
    .op-karta { color: inherit; display: flex; flex-direction: column; gap: 14px; transition: transform 0.12s; }
    .op-karta:active { transform: scale(0.99); }
    .ism { font-weight: 650; }
    .qoldiq { font-size: 18px; }
    .avatar { width: 42px; height: 42px; border-radius: 999px; display: inline-flex; align-items: center; justify-content: center; font-weight: 700; background: var(--asosiy-och); color: var(--asosiy); }
  `,
})
export class OperatorlarSahifa {
  protected readonly til = inject(Til);
  private readonly malumot = inject(Malumot);
  protected readonly royxat = signal<OperatorHisob[] | null>(null);
  protected readonly xato = signal<string | null>(null);
  protected readonly pul = pul;

  constructor() {
    this.yukla();
  }

  async yukla() {
    try {
      const ops = await this.malumot.operatorlar();
      const oy = oyBoshi(isoKun());
      const r = await Promise.all(ops.map((o) =>
        ol(api.GET('/operatorlar/{id}/hisob', { params: { path: { id: o.id }, query: { oy } } }))));
      this.royxat.set(r);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    }
  }
}
