import { Component, inject } from '@angular/core';
import { Auth } from '../../core/auth';
import { Til, TilKodi } from '../../core/til';
import { Tema, TemaRejimi } from '../../core/tema';
import { Navbat } from '../../core/navbat';
import { Ikon } from '../../ui/ikon';
import { OrnatishTaklif } from '../../ui/ornatish-taklif';

/** PWA'da faqat til, mavzu va chiqish (qolgan sozlamalar desktop'da, Admin uchun). */
@Component({
  selector: 'sozlamalar-sahifa',
  imports: [Ikon, OrnatishTaklif],
  template: `
    <div class="sahifa">
      <div class="sahifa-bosh"><h1>{{ til.t('Sozlamalar') }}</h1></div>

      <section class="shisha karta ustunlar">
        <div class="qator">
          <span class="ikon-doira"><ikon nomi="user" [olcham]="18" /></span>
          <div class="bosh-joy">
            <div style="font-weight:650">{{ auth.foydalanuvchi()?.toliqIsm }}</div>
            <div class="ikkilamchi kichik-matn">{{ auth.foydalanuvchi()?.login }} · {{ til.t('Rol_' + auth.foydalanuvchi()?.rol) }}</div>
          </div>
        </div>
      </section>

      <section class="shisha karta ustunlar">
        <h2><ikon nomi="globe" [olcham]="18" /> {{ til.t('Til') }}</h2>
        <div class="segment">
          @for (t of tillar; track t.kod) {
            <button [class.tanlangan]="til.kod() === t.kod" (click)="til.tanla(t.kod)">{{ til.t(t.kalit) }}</button>
          }
        </div>
        <h2><ikon [nomi]="tema.qorongimi() ? 'moon' : 'sun'" [olcham]="18" /> {{ til.t('RejimIzoh') }}</h2>
        <div class="segment">
          @for (r of rejimlar; track r.kod) {
            <button [class.tanlangan]="tema.rejim() === r.kod" (click)="tema.rejim.set(r.kod)">{{ til.t(r.kalit) }}</button>
          }
        </div>
      </section>

      <ornatish-taklif />

      @if (navbat.royxat().length) {
        <section class="shisha karta ustunlar">
          <div class="qator">
            <ikon nomi="upload" [olcham]="18" />
            <span class="bosh-joy">{{ til.t('Navbatda', navbat.royxat().length) }}</span>
            <button class="tugma kichik" (click)="navbat.yubor()">{{ til.t('HozirYuborish') }}</button>
          </div>
        </section>
      }

      <button class="tugma xavfli keng" (click)="auth.chiqish()"><ikon nomi="power" [olcham]="18" /> {{ til.t('Chiqish') }}</button>
      <p class="ikkilamchi kichik-matn" style="text-align:center;margin:0">FuelControl PWA · {{ til.t('Versiya') }} 1.0</p>
    </div>
  `,
})
export class SozlamalarSahifa {
  protected readonly auth = inject(Auth);
  protected readonly til = inject(Til);
  protected readonly tema = inject(Tema);
  protected readonly navbat = inject(Navbat);
  protected readonly tillar: { kod: TilKodi; kalit: string }[] = [
    { kod: 'uz', kalit: 'Lotin' }, { kod: 'uzk', kalit: 'Kirill' }, { kod: 'ru', kalit: 'Rus' },
  ];
  protected readonly rejimlar: { kod: TemaRejimi; kalit: string }[] = [
    { kod: 'yorug', kalit: 'Yorug' }, { kod: 'qorongi', kalit: 'Qorongi' }, { kod: 'tizim', kalit: 'Tizim' },
  ];
}
