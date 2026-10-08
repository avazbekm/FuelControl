import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Auth } from '../../core/auth';
import { Til } from '../../core/til';
import { Bildirish, xatoMatni } from '../../core/bildirish';
import { harflar, pul } from '../../core/format';
import type { FoydalanuvchiDto, Rol } from '../../api/model';
import { Ikon } from '../../ui/ikon';
import { Oyna } from '../../ui/oyna';
import { SonKiritish } from '../../ui/son-kiritish';
import { SozlamalarXizmati } from './sozlamalar-xizmati';

const ROLLAR: Rol[] = ['Operator', 'Boshliq', 'Admin'];

/** "Foydalanuvchilar": jadval (telefonda kartalar), foydalanuvchi dialogi va PIN/parol tiklash dialogi. */
@Component({
  selector: 'sozlamalar-foydalanuvchilar',
  imports: [FormsModule, Ikon, Oyna, SonKiritish],
  template: `
    <section class="shisha karta">
      <div class="karta-bosh sarlavha-qator">
        <h2>{{ til.t('Foydalanuvchilar') }}</h2>
        <button type="button" class="tugma asosiy" (click)="qosh()"><ikon nomi="plus" [olcham]="16" [qalinlik]="2.4" /> {{ til.t('FoydalanuvchiQoshish') }}</button>
      </div>

      <!-- Keng ekran: jadval -->
      <div class="faqat-keng jadval-oram">
        <table class="jadval">
          <thead>
            <tr><th>{{ til.t('ToliqIsm') }}</th><th>{{ til.t('Login') }}</th><th>{{ til.t('Rol') }}</th><th class="o">{{ til.t('OylikMaosh') }}</th><th>{{ til.t('Holat') }}</th><th></th></tr>
          </thead>
          <tbody>
            @for (f of x.foydalanuvchilar(); track f.id) {
              <tr>
                <td><span class="qator ism-qator"><span class="avatar kichik">{{ harflar(f.toliqIsm) }}</span><b>{{ f.toliqIsm }}</b></span></td>
                <td class="ikkilamchi">{{ f.login }}</td>
                <td><span class="pill kok">{{ til.t('Rol_' + f.rol) }}</span></td>
                <td class="o">{{ pul(f.oylikMaosh) }}</td>
                <td>@if (f.faol) { <span class="pill yashil">{{ til.t('Faol') }}</span> } @else { <span class="pill kul">{{ til.t('Nofaol') }}</span> }</td>
                <td class="amallar-yacheyka">
                  <button type="button" class="tugma kichik" (click)="tahrirla(f)">{{ til.t('Tahrirlash') }}</button>
                  <button type="button" class="tugma kichik" (click)="tiklashOch(f)">{{ til.t('PinParolTiklash') }}</button>
                </td>
              </tr>
            } @empty { <tr><td colspan="6" class="bosh">{{ til.t('MalumotYoq') }}</td></tr> }
          </tbody>
        </table>
      </div>

      <!-- Telefon: kartalar -->
      <div class="faqat-tor">
       <div class="kartalar">
        @for (f of x.foydalanuvchilar(); track f.id) {
          <div class="plitka f-karta">
            <div class="qator ora">
              <span class="qator">
                <span class="avatar">{{ harflar(f.toliqIsm) }}</span>
                <span class="matnlar">
                  <span class="ism">{{ f.toliqIsm }}</span>
                  <span class="ikkilamchi kichik-matn">{{ f.login }} · {{ pul(f.oylikMaosh) }} {{ til.t('Som') }}</span>
                </span>
              </span>
              <span class="belgilar">
                <span class="pill kok">{{ til.t('Rol_' + f.rol) }}</span>
                @if (f.faol) { <span class="pill yashil">{{ til.t('Faol') }}</span> } @else { <span class="pill kul">{{ til.t('Nofaol') }}</span> }
              </span>
            </div>
            <div class="qator tugmalar">
              <button type="button" class="tugma kichik bosh-joy" (click)="tahrirla(f)">{{ til.t('Tahrirlash') }}</button>
              <button type="button" class="tugma kichik bosh-joy" (click)="tiklashOch(f)">{{ til.t('PinParolTiklash') }}</button>
            </div>
          </div>
        } @empty { <div class="bosh">{{ til.t('MalumotYoq') }}</div> }
       </div>
      </div>
    </section>

    <!-- Foydalanuvchi dialogi -->
    <oyna [(ochiq)]="dialog" [sarlavha]="tahrirF() ? til.t('FoydalanuvchiniTahrirlash') : til.t('YangiFoydalanuvchi')" [kenglik]="520" ikon="user">
      <form class="forma-ustun" (ngSubmit)="saqla()" autocomplete="off" novalidate>
        <div class="maydon">
          <label for="f-ism">{{ til.t('ToliqIsm') }}</label>
          <input id="f-ism" class="kiritish" name="ism" [(ngModel)]="ism" autocomplete="off" data-avto />
        </div>
        <div class="ikki-ustun">
          <div class="maydon">
            <label for="f-login">{{ til.t('Login') }}</label>
            <input id="f-login" class="kiritish" name="login" [(ngModel)]="login" autocomplete="off" autocapitalize="none" spellcheck="false" />
          </div>
          <div class="maydon">
            <label for="f-rol">{{ til.t('Rol') }}</label>
            <select id="f-rol" class="kiritish" name="rol" [(ngModel)]="rol">
              @for (r of rollar; track r) { <option [value]="r">{{ til.t('Rol_' + r) }}</option> }
            </select>
          </div>
        </div>
        <div class="ikki-ustun">
          <div class="maydon">
            <label for="f-maosh">{{ til.t('OylikMaosh') }}</label>
            <son-kiritish [(qiymat)]="maosh" sinf="kiritish pul" inputId="f-maosh" />
          </div>
          <div class="maydon">
            <label for="f-pin">{{ til.t('PinYokiParol') }}</label>
            <input id="f-pin" class="kiritish" name="pin" type="password" [(ngModel)]="pin" autocomplete="new-password" />
          </div>
        </div>
        <label class="belgi"><input type="checkbox" name="faol" [(ngModel)]="faol" /> {{ til.t('FaolHolat') }}</label>
        @if (xato()) { <div class="xato-matn" role="alert">{{ xato() }}</div> }
        <div class="amallar">
          <button type="button" class="tugma" (click)="dialog.set(false)">{{ til.t('BekorQilish') }}</button>
          <button type="submit" class="tugma asosiy" [disabled]="band()">
            @if (band()) { <span class="aylanma"></span> } @else { <ikon nomi="check" [olcham]="16" [qalinlik]="2.4" /> } {{ til.t('Saqlash') }}
          </button>
        </div>
      </form>
    </oyna>

    <!-- PIN/parol tiklash dialogi -->
    <oyna [(ochiq)]="tiklash" [sarlavha]="til.t('PinParolTiklash')" [tagsarlavha]="tiklashF()?.toliqIsm ?? ''" [kenglik]="460" ikon="lock">
      <form class="forma-ustun" (ngSubmit)="tiklashSaqla()" autocomplete="off" novalidate>
        <div class="maydon">
          <label for="t-pin">{{ til.t('YangiPinParol') }}</label>
          <input id="t-pin" class="kiritish katta" name="yangipin" style="text-align:center" [(ngModel)]="yangiPin" autocomplete="off" data-avto />
        </div>
        @if (tiklashXato()) { <div class="xato-matn" role="alert">{{ tiklashXato() }}</div> }
        <div class="amallar">
          <button type="button" class="tugma" (click)="tiklash.set(false)">{{ til.t('BekorQilish') }}</button>
          <button type="submit" class="tugma asosiy" [disabled]="band()">
            @if (band()) { <span class="aylanma"></span> } @else { <ikon nomi="check" [olcham]="16" [qalinlik]="2.4" /> } {{ til.t('Tiklash') }}
          </button>
        </div>
      </form>
    </oyna>
  `,
  styles: `
    .sarlavha-qator { align-items: center; }
    .sarlavha-qator h2 { font-size: 16px; }
    .ism-qator { gap: 10px; }
    .avatar.kichik { width: 34px; height: 34px; font-size: 12px; }
    .amallar-yacheyka { white-space: nowrap; text-align: right; }
    .amallar-yacheyka .tugma + .tugma { margin-left: 6px; }
    .kartalar { display: flex; flex-direction: column; gap: 10px; }
    .f-karta { display: flex; flex-direction: column; gap: 10px; }
    .matnlar { display: flex; flex-direction: column; gap: 2px; min-width: 0; }
    .ism { font-weight: 700; }
    .belgilar { display: flex; flex-direction: column; align-items: flex-end; gap: 4px; flex: none; }
    .tugmalar { gap: 8px; }
  `,
})
export class FoydalanuvchilarBolimi {
  protected readonly til = inject(Til);
  protected readonly x = inject(SozlamalarXizmati);
  private readonly auth = inject(Auth);
  private readonly bildirish = inject(Bildirish);

  protected readonly pul = pul;
  protected readonly harflar = harflar;
  protected readonly rollar = ROLLAR;
  protected readonly band = signal(false);

  protected readonly dialog = signal(false);
  protected readonly tahrirF = signal<FoydalanuvchiDto | null>(null);
  protected readonly xato = signal<string | null>(null);
  protected ism = '';
  protected login = '';
  protected rol: Rol = 'Operator';
  protected readonly maosh = signal<number | null>(null);
  protected pin = '';
  protected faol = true;

  protected readonly tiklash = signal(false);
  protected readonly tiklashF = signal<FoydalanuvchiDto | null>(null);
  protected readonly tiklashXato = signal<string | null>(null);
  protected yangiPin = '';

  protected qosh() {
    this.tahrirF.set(null);
    this.ism = ''; this.login = ''; this.rol = 'Operator'; this.maosh.set(null); this.pin = ''; this.faol = true;
    this.xato.set(null);
    this.dialog.set(true);
  }

  protected tahrirla(f: FoydalanuvchiDto) {
    this.tahrirF.set(f);
    this.ism = f.toliqIsm; this.login = f.login; this.rol = f.rol;
    this.maosh.set(f.oylikMaosh > 0 ? f.oylikMaosh : null); this.pin = ''; this.faol = f.faol;
    this.xato.set(null);
    this.dialog.set(true);
  }

  async saqla() {
    const ism = this.ism.trim();
    const login = this.login.trim().toLowerCase();
    const t = this.tahrirF();
    if (!ism || !login || (!t && !this.pin.trim())) return this.xato.set(this.til.t('Xato_Maydon'));
    if (this.x.foydalanuvchilar().some((f) => f.login.toLowerCase() === login && f.id !== t?.id)) return this.xato.set(this.til.t('Xato_LoginBand'));
    const maosh = Math.round(this.maosh() ?? 0);
    this.band.set(true);
    try {
      if (t) {
        await this.x.foydalanuvchiTahrirla(t.id, { toliqIsm: ism, login, rol: this.rol, faol: this.faol, oylikMaosh: maosh }, this.pin);
        if (t.id === this.auth.foydalanuvchi()?.id) await this.auth.yangila(); // o'zining roli/ruxsati o'zgargan bo'lishi mumkin
      } else {
        const yangi = await this.x.foydalanuvchiYarat(ism, login, this.rol, maosh, this.pin.trim());
        // Yaratish har doim faol foydalanuvchi beradi; nofaol belgilangan bo'lsa — darhol o'chiramiz.
        if (!this.faol) await this.x.foydalanuvchiTahrirla(yangi.id, { toliqIsm: ism, login, rol: this.rol, faol: false, oylikMaosh: maosh });
      }
      this.dialog.set(false);
      this.bildirish.korsat(this.til.t('Saqlandi'));
    } catch (e) {
      this.xato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }

  protected tiklashOch(f: FoydalanuvchiDto) {
    this.tiklashF.set(f);
    this.yangiPin = '';
    this.tiklashXato.set(null);
    this.tiklash.set(true);
  }

  async tiklashSaqla() {
    const f = this.tiklashF();
    const pin = this.yangiPin.trim();
    if (!f || !pin) return this.tiklashXato.set(this.til.t('Xato_Maydon'));
    this.band.set(true);
    try {
      await this.x.pinOrnat(f.id, pin);
      this.tiklash.set(false);
      this.bildirish.korsat(this.til.t('PinTiklandi'));
    } catch (e) {
      this.tiklashXato.set(xatoMatni(e, this.til.t('AloqaYoq'), this.til.t('Xato_Umumiy')));
    } finally {
      this.band.set(false);
    }
  }
}
