import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Til } from '../../core/til';
import { Server } from '../../core/server';
import { jonliYangila } from '../../core/malumot';
import { xatoMatni } from '../../core/bildirish';
import { harflar, kun, soat } from '../../core/format';
import type { AuditYozuviDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';

const FILTRLAR = [
  { tur: '', kalit: 'Hammasi' },
  { tur: 'smena', kalit: 'Smena' },
  { tur: 'nasiya', kalit: 'Audit_TurNasiya' },
  { tur: 'xarajat', kalit: 'Audit_TurXarajat' },
  { tur: 'bak', kalit: 'Audit_TurBak' },
  { tur: 'tuzatish', kalit: 'Audit_TurTuzatish' },
];

/** Amal turi → pill rangi (docs/dizayn/Audit): smena ko'k, nasiya to'q sariq, xarajat sariq, bak yashil, tuzatish qizil, qolganlari kul. */
const PILL: Record<string, string> = { smena: 'pill-smena', nasiya: 'pill-nasiya', xarajat: 'pill-xarajat', bak: 'pill-bak', tuzatish: 'pill-tuzatish' };

/** Audit jurnali: qidiruv + tur chiplari + yozuvlar (keng ekranda ustunlar, telefonda o'z-o'zidan qatorga o'raladi). */
@Component({
  selector: 'audit-sahifa',
  imports: [FormsModule, Ikon],
  template: `
    <div class="sahifa">
      <header class="sahifa-bosh">
        <div class="sarlavha">
          <h1>{{ til.t('AuditJurnali') }}</h1>
          <span class="sarlavha-izoh">{{ til.t('Audit_Izoh') }}</span>
        </div>
      </header>

      <section class="shisha jurnal" aria-labelledby="audit-yozuvlar">
        <h2 id="audit-yozuvlar" class="yashirin-sarlavha">{{ til.t('Audit_Yozuvlar') }}</h2>
        <div class="filtr-qator">
          <div class="qidiruv-oram">
            <span class="qidiruv-ikon"><ikon nomi="search" [olcham]="17" [qalinlik]="2" /></span>
            <input class="kiritish qidiruv" type="search" name="q" [ngModel]="qidiruv()" (ngModelChange)="qidirish($event)" [attr.aria-label]="til.t('Qidirish')" [placeholder]="til.t('Audit_QidiruvPlaceholder')" autocomplete="off" />
          </div>
          <div class="chiplar" role="group" [attr.aria-label]="til.t('Audit_AmalTuri')">
            @for (f of filtrlar; track f.tur) {
              <button type="button" class="chip kichik" [class.tanlangan]="tur() === f.tur" [attr.aria-pressed]="tur() === f.tur" (click)="turTanla(f.tur)">{{ til.t(f.kalit) }}</button>
            }
          </div>
        </div>

        @if (xato(); as x) {
          <div class="bosh">{{ x }}</div>
        } @else if (!yuklandi()) {
          <div class="skelet" style="height: 220px"></div>
        } @else {
          <ul class="yozuvlar">
            @for (y of yozuvlar(); track y.id) {
              <li class="yozuv">
                <span class="vaqt"><b>{{ soat(y.vaqt) }}</b><span>{{ kun(y.vaqt) }}</span></span>
                <span class="kim"><span class="avatar kichik">{{ harflar(y.kim) }}</span><span class="kim-ism">{{ y.kim }}</span></span>
                <span class="amal"><span class="pill katta" [class]="belgi(y)">{{ y.amal }}</span></span>
                <span class="tafsilot">{{ y.tafsilot }}</span>
              </li>
            } @empty { <li class="bosh">{{ til.t('MalumotYoq') }}</li> }
          </ul>
        }
      </section>
    </div>
  `,
  styles: `
    .jurnal { padding: 18px 20px 10px; display: flex; flex-direction: column; gap: 12px; }
    .yashirin-sarlavha { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); margin: -1px; }
    .filtr-qator { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
    .qidiruv-oram { flex: 1 1 280px; min-width: 0; position: relative; display: flex; align-items: center; }
    .qidiruv-ikon { position: absolute; left: 16px; display: inline-flex; color: var(--matn-2); pointer-events: none; }
    .kiritish.qidiruv { min-height: 44px; padding: 0 18px 0 44px; border-radius: 22px; font-size: 14px; }
    .chip.kichik { min-height: 44px; padding: 0 16px; font-size: 13.5px; }
    .yozuvlar { list-style: none; margin: 0; padding: 0; }
    .yozuv { display: flex; flex-wrap: wrap; align-items: center; gap: 10px 14px; padding: 12px 4px; border-bottom: 1px solid var(--chiziq); }
    .yozuv:last-child { border-bottom: 0; }
    .vaqt { width: 92px; flex: none; display: flex; flex-direction: column; gap: 2px; font-variant-numeric: tabular-nums; }
    .vaqt b { font-size: 13.5px; font-weight: 700; }
    .vaqt span { font-size: 12px; color: var(--matn-2); }
    .kim { width: 190px; flex: none; display: flex; align-items: center; gap: 10px; min-width: 0; }
    .avatar.kichik { width: 34px; height: 34px; font-size: 12px; }
    .kim-ism { font-size: 13.5px; font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .amal { width: 170px; flex: none; }
    .tafsilot { flex: 1 1 280px; min-width: 0; font-size: 13.5px; line-height: 1.45; color: var(--matn-3); font-variant-numeric: tabular-nums; }
    .pill-smena { background: var(--b-kok-fon); color: var(--b-kok); }
    .pill-nasiya { background: light-dark(#FFE9DA, #4A2A18); color: light-dark(#9A3412, #FFB68A); }
    .pill-xarajat { background: var(--b-sariq-fon); color: var(--b-sariq); }
    .pill-bak { background: var(--b-yashil-fon); color: var(--b-yashil); }
    .pill-tuzatish { background: var(--b-qizil-fon); color: var(--b-qizil); }
    @media (max-width: 699px) {
      .jurnal { padding: 16px 14px 6px; }
      .kim, .amal { width: auto; flex: 1 1 140px; }
      .tafsilot { flex: 1 1 100%; }
    }
  `,
})
export class AuditSahifa {
  protected readonly til = inject(Til);
  private readonly server = inject(Server);

  protected readonly filtrlar = FILTRLAR;
  protected readonly harflar = harflar;
  protected readonly soat = soat;
  protected readonly kun = kun;
  protected readonly qidiruv = signal('');
  protected readonly tur = signal('');
  private readonly hammasi = signal<AuditYozuviDto[]>([]);
  protected readonly yuklandi = signal(false);
  protected readonly xato = signal<string | null>(null);
  private qidirTaymer: ReturnType<typeof setTimeout> | undefined;

  /** Tur filtri serverdan keladi; "Hammasi"da hisob/sozlama/kirish ham ko'rinadi. */
  protected readonly yozuvlar = computed(() => this.hammasi());

  constructor() {
    this.yukla();
    jonliYangila(() => this.yukla());
  }

  protected belgi(y: AuditYozuviDto): string {
    return PILL[y.tur] ?? 'kul';
  }

  protected qidirish(q: string) {
    this.qidiruv.set(q);
    clearTimeout(this.qidirTaymer);
    this.qidirTaymer = setTimeout(() => this.yukla(), 250);
  }

  protected turTanla(t: string) {
    this.tur.set(t);
    this.yukla();
  }

  private async yukla() {
    try {
      this.hammasi.set(await this.server.audit(this.qidiruv().trim() || undefined, this.tur() || undefined));
      this.xato.set(null);
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.yuklandi.set(true);
    }
  }
}
