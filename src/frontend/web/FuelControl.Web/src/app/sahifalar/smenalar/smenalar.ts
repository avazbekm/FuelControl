import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { api, ol } from '../../api/api';
import { Auth } from '../../core/auth';
import { Til } from '../../core/til';
import { xatoMatni } from '../../core/bildirish';
import { Malumot, OperatorElement, jonliYangila } from '../../core/malumot';
import { pul, kunSoat, davomiylik, isoKun, kunQosh, oyBoshi } from '../../core/format';
import type { Smena } from '../../api/turlar';
import { Ikon } from '../../ui/ikon';

type Davr = 'ochiq' | 'bugun' | 'kun7' | 'oy' | 'hammasi';

export function smenaJami(s: Smena) {
  return s.kutilganNaqd + s.kutilganPlastik + s.kutilganClick;
}

@Component({
  selector: 'smenalar-sahifa',
  imports: [FormsModule, RouterLink, Ikon],
  template: `
    <div class="sahifa">
      <div class="sahifa-bosh">
        <h1>{{ til.t('Smenalar') }}</h1>
        @if (auth.bor('SmenaOchish')) {
          <a class="tugma kichik" routerLink="/sotuv"><ikon nomi="plus" [olcham]="16" /> {{ til.t('SotuvKiritish') }}</a>
        }
      </div>

      <div class="chiplar">
        @for (d of davrlar; track d.kod) {
          <button class="chip" [class.tanlangan]="davr() === d.kod" (click)="davrTanla(d.kod)">{{ til.t(d.kalit) }}</button>
        }
      </div>
      @if (operatorlar().length) {
        <select class="kiritish" [ngModel]="operatorId()" (ngModelChange)="operatorTanla($event)" [attr.aria-label]="til.t('Operator')">
          <option [ngValue]="null">{{ til.t('BarchaOperatorlar') }}</option>
          @for (o of operatorlar(); track o.id) { <option [ngValue]="o.id">{{ o.ism }}</option> }
        </select>
      }

      <div class="kpilar">
        <div class="shisha kpi"><span class="nom">{{ til.t('Smenalar') }}</span><span class="qiymat">{{ korinadigan().length }}</span></div>
        <div class="shisha kpi"><span class="nom">{{ til.t('JamiSavdo') }}</span><span class="qiymat">{{ pul(yigindi().jami) }}<small>{{ til.t('Som') }}</small></span></div>
        <div class="shisha kpi"><span class="nom">{{ til.t('Kamomat') }}</span><span class="qiymat" [class.qizil]="yigindi().kamomat > 0">{{ pul(yigindi().kamomat) }}<small>{{ til.t('Som') }}</small></span></div>
        <div class="shisha kpi"><span class="nom">{{ til.t('Ortiqcha') }}</span><span class="qiymat" [class.yashil]="yigindi().ortiqcha > 0">{{ pul(yigindi().ortiqcha) }}<small>{{ til.t('Som') }}</small></span></div>
      </div>

      <section class="shisha karta">
        @if (xato()) { <div class="bosh">{{ xato() }}</div> }
        @else if (yuklanmoqda() && !smenalar().length) { <div class="skelet" style="height:180px"></div> }
        @else {
          <div class="royxat">
            @for (s of korinadigan(); track s.id) {
              <a class="element" [routerLink]="['/smenalar', s.id]">
                <span class="ikon-doira" [class.ochiq]="!s.tugadi"><ikon nomi="clock" [olcham]="18" /></span>
                <span class="matnlar">
                  <span class="asosiy-matn">{{ s.operatorIsmi }} <span class="ikkilamchi">#{{ s.id }}</span></span>
                  <span class="ikkinchi son">{{ kunSoat(s.boshlandi) }} · {{ dav(s) }} · {{ s.sotuvSoni }} {{ til.t('TaSotuv') }}</span>
                </span>
                <span class="ong">
                  <b>{{ pul(jami(s)) }}</b>
                  @if (!s.tugadi) { <span class="badge kok">{{ til.t('OchiqKichik') }}</span> }
                  @else if (s.kamomat > 0) { <span class="badge qizil">−{{ pul(s.kamomat) }}</span> }
                  @else if (s.ortiqcha > 0) { <span class="badge yashil">+{{ pul(s.ortiqcha) }}</span> }
                  @else { <span class="badge kul">{{ til.t('Yopilgan') }}</span> }
                </span>
              </a>
            } @empty { <div class="bosh">{{ til.t('MalumotYoq') }}</div> }
          </div>
        }
      </section>
    </div>
  `,
  styles: `.ikon-doira.ochiq { background: var(--b-yashil-fon); color: var(--yashil); }`,
})
export class SmenalarSahifa {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
  private readonly malumot = inject(Malumot);

  protected readonly davrlar: { kod: Davr; kalit: string }[] = [
    { kod: 'ochiq', kalit: 'HozirOchiq' }, { kod: 'bugun', kalit: 'Bugun' }, { kod: 'kun7', kalit: 'Kun7' },
    { kod: 'oy', kalit: 'ShuOy' }, { kod: 'hammasi', kalit: 'Hammasi' },
  ];
  protected readonly davr = signal<Davr>('bugun');
  protected readonly operatorId = signal<number | null>(null);
  protected readonly operatorlar = signal<OperatorElement[]>([]);
  protected readonly smenalar = signal<Smena[]>([]);
  protected readonly yuklanmoqda = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected readonly pul = pul;
  protected readonly kunSoat = kunSoat;
  protected readonly jami = smenaJami;

  protected readonly korinadigan = computed(() =>
    this.davr() === 'ochiq' ? this.smenalar().filter((s) => !s.tugadi) : this.smenalar());
  protected readonly yigindi = computed(() => {
    const r = this.korinadigan();
    return {
      jami: r.reduce((s, x) => s + smenaJami(x), 0),
      kamomat: r.reduce((s, x) => s + x.kamomat, 0),
      ortiqcha: r.reduce((s, x) => s + x.ortiqcha, 0),
    };
  });

  constructor() {
    this.yukla();
    this.malumot.operatorlar().then((o) => this.operatorlar.set(o)).catch(() => {});
    jonliYangila(() => this.yukla(), (h) => h.turi !== 'NarxOzgardi');
  }

  davrTanla(d: Davr) { this.davr.set(d); this.yukla(); }
  operatorTanla(id: number | null) { this.operatorId.set(id); this.yukla(); }

  dav(s: Smena) {
    return davomiylik(s.boshlandi, s.tugadi, this.til.t('Soat'), this.til.t('Min'));
  }

  async yukla() {
    const bugun = isoKun();
    const dan = { ochiq: undefined, bugun, kun7: kunQosh(bugun, -6), oy: oyBoshi(bugun), hammasi: undefined }[this.davr()];
    this.yuklanmoqda.set(true);
    try {
      const r = await ol(api.GET('/smenalar', {
        params: { query: { dan, gacha: dan ? bugun : undefined, operatorId: this.operatorId() ?? undefined } },
      }));
      this.smenalar.set(r);
      this.xato.set(null);
    } catch (e) {
      if (!this.smenalar().length) this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.yuklanmoqda.set(false);
    }
  }
}
