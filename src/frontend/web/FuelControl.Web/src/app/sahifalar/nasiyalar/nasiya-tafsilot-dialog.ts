import { Component, computed, effect, inject, input, model, output, signal, untracked } from '@angular/core';
import { Til } from '../../core/til';
import { Auth } from '../../core/auth';
import { Server } from '../../core/server';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { kunToliq, pul, soat, kunQisqa } from '../../core/format';
import { telefonFormat } from '../../core/telefon';
import type { NasiyaQaytishiDto, NasiyaTafsilotDto, SmenaDto } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { Oyna } from '../../ui/oyna';
import { MashinaRaqami } from '../../ui/belgilar';

/**
 * Nasiya tafsiloti: qarz holati, qaytishlar tarixi va (ruxsat bo'lsa, tasdiqlash bilan) o'chirish.
 * Qoidalar (docs §1.4, §7.4–5): o'chirish faqat ochiq smenadagi yozuv uchun, muallif yoki `Smenalar` ruxsati bor foydalanuvchi;
 * qaytishi bor nasiya o'chirilmaydi; smenaga bog'lanmagan qaytishni faqat `Smenalar` ruxsatlisi o'chiradi.
 */
@Component({
  selector: 'nasiya-tafsilot-dialog',
  imports: [Ikon, Oyna, MashinaRaqami],
  template: `
    <oyna [(ochiq)]="ochiq" [sarlavha]="n()?.mijozIsmi ?? ''" [tagsarlavha]="tel(n()?.telefon)" ikon="book" ikonRang="to-rang">
      @if (t(); as t) {
        <div class="qator ora orala tepa">
          <mashina-raqami [qiymat]="t.nasiya.mashinaRaqami" />
          <span class="pill katta" [class]="holatRang()">{{ holatMatn() }}</span>
        </div>
        <div class="tortlik">
          <div class="kichik-plitka"><span class="kap">{{ til.t('Nasiya_Qarz') }}</span><span class="qiymat">{{ pul(t.nasiya.summa) }}</span></div>
          <div class="kichik-plitka"><span class="kap">{{ til.t('Nasiya_Qaytgan') }}</span><span class="qiymat yashil">{{ pul(t.nasiya.qaytgan) }}</span></div>
          <div class="kichik-plitka"><span class="kap">{{ til.t('Nasiya_Qoldiq') }}</span><span class="qiymat">{{ pul(t.nasiya.qoldiq) }}</span></div>
          <div class="kichik-plitka" [class.otgan]="t.nasiya.holati === 'MuddatiOtgan'"><span class="kap">{{ til.t('Nasiya_MuddatYorliq') }}</span><span class="qiymat" [class.qizil]="t.nasiya.holati === 'MuddatiOtgan'">{{ kunToliq(t.nasiya.muddat) }}</span></div>
        </div>
        <span class="ikkilamchi kichik-matn">{{ yozilganMatn() }}</span>

        <div class="bolim-bosh"><span class="yorliq">{{ til.t('Nasiyalar_QaytishlarTarixi') }}</span></div>
        <div class="qaytishlar">
          @for (q of t.qaytishlar; track q.id) {
            <div class="qaytish">
              <div class="matnlar">
                <span class="qator orala kichik-gap">
                  <b class="son">+{{ pul(q.summa) }}</b>
                  <span class="pill" [class]="usulRang(q.usul)">{{ til.t('Tolov_' + q.usul) }}</span>
                  @if (q.smenaId == null) { <span class="pill kul">{{ til.t('Nasiyalar_SmenadanTashqari') }}</span> }
                </span>
                <span class="ikkilamchi kichik-matn">{{ kunQisqa(q.vaqt) }} {{ soat(q.vaqt) }} · {{ q.kimYozdi }}@if (q.smenaId != null) { · #{{ q.smenaId }} }@if (q.izoh) { · “{{ q.izoh }}” }</span>
              </div>
              @if (qaytishniOchirsaBoladi(q)) {
                <button type="button" class="tugma ikonli kichik shaffof xavfli" [attr.aria-label]="til.t('Ochirish')" [title]="til.t('Ochirish')" (click)="surov.set({ tur: 'qaytish', id: q.id })"><ikon nomi="x" [olcham]="16" [qalinlik]="2.2" /></button>
              }
            </div>
          } @empty {
            <div class="ikkilamchi kichik-matn bosh-qator">{{ til.t('Nasiyalar_QaytishYoq') }}</div>
          }
        </div>

        @if (nasiyaniOchirsaBoladi() && t.qaytishlar.length > 0) {
          <div class="malumot-blok"><ikon nomi="info" [olcham]="16" [qalinlik]="2" /><span>{{ til.t('Nasiyalar_QaytishiBorOchirilmaydi') }}</span></div>
        }

        @if (surov(); as s) {
          <div class="ogohlik-blok surov" role="alert">
            <ikon nomi="warning" [olcham]="16" [qalinlik]="2" />
            <span class="bosh-joy">{{ til.t(s.tur === 'nasiya' ? 'Nasiyalar_OchirishSavol' : 'Nasiyalar_QaytishOchirishSavol') }}</span>
            <button type="button" class="tugma kichik" (click)="surov.set(null)">{{ til.t('BekorQilish') }}</button>
            <button type="button" class="tugma kichik xavfli toliq" [disabled]="band()" (click)="ochir(s)">
              @if (band()) { <span class="aylanma"></span> } {{ til.t('Nasiyalar_HaOchirish') }}
            </button>
          </div>
        }

        <div class="amallar">
          @if (nasiyaniOchirsaBoladi() && t.qaytishlar.length === 0) {
            <button type="button" class="tugma xavfli ochirish" (click)="surov.set({ tur: 'nasiya', id: t.nasiya.id })">{{ til.t('Nasiyalar_Ochirish') }}</button>
          }
          <button type="button" class="tugma" (click)="ochiq.set(false)">{{ til.t('Yopish') }}</button>
          @if (qaytdiBoladi()) {
            <button type="button" class="tugma asosiy" (click)="qaytdi.emit(t.nasiya.id)"><ikon nomi="undo" [olcham]="16" [qalinlik]="2" /> {{ til.t('Nasiya_QarzQaytdi') }}</button>
          }
        </div>
      } @else {
        <div class="skelet" style="height: 160px"></div>
      }
    </oyna>
  `,
  styles: `
    .tepa { gap: 10px; }
    .tortlik { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 8px; }
    .kichik-plitka { padding: 10px 12px; border-radius: 14px; background: var(--malumot-fon); display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .kichik-plitka.otgan { background: var(--kamomat-fon); }
    .kichik-plitka .kap { font-size: 10.5px; font-weight: 700; letter-spacing: 0.8px; text-transform: uppercase; color: var(--matn-2); }
    .kichik-plitka .qiymat { font-size: 15px; font-weight: 800; font-variant-numeric: tabular-nums; white-space: nowrap; }
    .qiymat.yashil { color: var(--yashil); }
    .qiymat.qizil { color: var(--b-qizil); }
    .bolim-bosh { padding-top: 4px; }
    .qaytishlar { display: flex; flex-direction: column; border-top: 1px solid var(--chiziq); }
    .qaytish { display: flex; align-items: center; gap: 8px; padding: 10px 2px; border-bottom: 1px solid var(--chiziq); }
    .qaytish .matnlar { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 3px; }
    .kichik-gap { gap: 8px; }
    .bosh-qator { padding: 12px 2px; }
    .surov { align-items: center; flex-wrap: wrap; }
    .surov .bosh-joy { min-width: 180px; }
    .amallar .ochirish { margin-right: auto; }
    @media (max-width: 480px) { .tortlik { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
  `,
})
export class NasiyaTafsilotDialog {
  protected readonly til = inject(Til);
  private readonly auth = inject(Auth);
  private readonly server = inject(Server);
  private readonly bildirish = inject(Bildirish);

  readonly ochiq = model(false);
  readonly nasiyaId = input<number | null>(null);
  /** Hozirgi ochiq smena (o'chirish qoidalari uchun). */
  readonly joriy = input<SmenaDto | null>(null);
  /** O'chirilgandan keyin ro'yxatni yangilash uchun. */
  readonly ozgardi = output<void>();
  /** "Qarz qaytdi" tugmasi — ota sahifa qaytish dialogini ochadi. */
  readonly qaytdi = output<number>();

  protected readonly pul = pul;
  protected readonly tel = telefonFormat;
  protected readonly soat = soat;
  protected readonly kunQisqa = kunQisqa;
  protected readonly kunToliq = kunToliq;
  protected readonly t = signal<NasiyaTafsilotDto | null>(null);
  protected readonly n = computed(() => this.t()?.nasiya ?? null);
  protected readonly surov = signal<{ tur: 'nasiya' | 'qaytish'; id: number } | null>(null);
  protected readonly band = signal(false);

  private get menId(): number { return this.auth.foydalanuvchi()?.id ?? 0; }
  private readonly boshliq = computed(() => this.auth.bor('Smenalar'));

  /** Faqat ochiq smenadagi, muallifi o'zi yoki boshliq bo'lsa. */
  protected readonly nasiyaniOchirsaBoladi = computed(() => {
    const n = this.n(); const j = this.joriy();
    return !!n && !!j && n.smenaId === j.id && (this.boshliq() || n.muallifId === this.menId);
  });
  protected readonly qaytdiBoladi = computed(() => {
    const n = this.n();
    return !!n && n.holati !== 'Yopilgan' && this.auth.bor('QarzQaytdi') && (!!this.joriy() || this.boshliq());
  });

  protected readonly holatRang = computed(() => {
    const n = this.n();
    if (!n) return '';
    return n.holati === 'Yopilgan' ? 'yashil' : n.holati === 'MuddatiOtgan' ? 'qizil' : n.muddatgachaKun <= 3 ? 'sariq' : 'kok';
  });
  protected readonly holatMatn = computed(() => {
    const n = this.n();
    if (!n) return '';
    if (n.holati === 'Yopilgan') return this.til.t('Nasiyalar_YopilganSana', n.yopildi ? kunQisqa(n.yopildi) : '');
    if (n.holati === 'MuddatiOtgan') return this.til.t('Nasiya_KunOtdi', Math.abs(n.muddatgachaKun));
    if (n.muddatgachaKun === 0) return this.til.t('Nasiya_Bugun');
    if (n.muddatgachaKun === 1) return this.til.t('Nasiya_Ertaga');
    return this.til.t('Nasiya_KunQoldi', n.muddatgachaKun);
  });
  protected readonly yozilganMatn = computed(() => {
    const n = this.n();
    if (!n) return '';
    const s = this.til.t('Nasiya_YozilganSatr', kunQisqa(n.yozildi), n.smenaId, n.operatorIsmi);
    return n.izoh ? `${s} · “${n.izoh}”` : s;
  });
  protected usulRang(u: string) { return u === 'Naqd' ? 'yashil' : u === 'Plastik' ? 'moviy' : 'binafsha'; }

  protected qaytishniOchirsaBoladi(q: NasiyaQaytishiDto): boolean {
    if (q.smenaId == null) return this.boshliq();
    const j = this.joriy();
    return !!j && q.smenaId === j.id && (this.boshliq() || q.muallifId === this.menId);
  }

  constructor() {
    effect(() => {
      if (!this.ochiq()) return;
      const id = this.nasiyaId();
      untracked(() => { this.surov.set(null); this.t.set(null); if (id != null) this.yukla(id); });
    });
  }

  private async yukla(id: number) {
    try {
      this.t.set(await this.server.nasiya(id));
    } catch (e) {
      this.bildirish.xato(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy'));
      this.ochiq.set(false);
    }
  }

  protected async ochir(s: { tur: 'nasiya' | 'qaytish'; id: number }) {
    this.band.set(true);
    try {
      if (s.tur === 'nasiya') {
        await this.server.nasiyaOchir(s.id);
        this.ochiq.set(false);
      } else {
        await this.server.qaytishOchir(s.id);
        const id = this.nasiyaId();
        if (id != null) await this.yukla(id);
      }
      this.surov.set(null);
      this.bildirish.korsat(this.til.t('Nasiyalar_Ochirildi'));
      this.ozgardi.emit();
    } catch (e) {
      this.surov.set(null);
      this.bildirish.korsat(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')), true);
    } finally {
      this.band.set(false);
    }
  }
}
