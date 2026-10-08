import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Til } from '../core/til';
import { Auth } from '../core/auth';
import { Ikon } from './ikon';

/**
 * Toza o'rnatishda (aparat yo'q) smena ochib va bakka kirim yozib bo'lmaydi — yo'l-yo'riq bloki.
 * "Sozlamalar" tugmasi faqat shu ruxsati borlarga ko'rinadi va Sozlamalar → Aparatlar bo'limini ochadi.
 */
@Component({
  selector: 'aparat-yoq',
  imports: [RouterLink, Ikon],
  template: `
    <div class="malumot-blok" role="status">
      <ikon nomi="info" [olcham]="16" [qalinlik]="2" />
      <span class="matn">{{ til.t('Bosh_AparatYoq') }}</span>
      @if (auth.bor('Sozlamalar')) {
        <a class="tugma kichik asosiy" routerLink="/sozlamalar" [queryParams]="{ qism: 'aparatlar' }">{{ til.t('Sozlamalar') }}</a>
      }
    </div>
  `,
  styles: `
    :host { display: block; }
    .malumot-blok { flex-wrap: wrap; align-items: center; }
    .matn { flex: 1 1 260px; min-width: 0; }
    .tugma { flex: none; }
  `,
})
export class AparatYoq {
  protected readonly til = inject(Til);
  protected readonly auth = inject(Auth);
}
