import { Component, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SwUpdate } from '@angular/service-worker';
import { filter } from 'rxjs';
import { Bildirish } from './core/bildirish';
import { Til } from './core/til';
import { Tema } from './core/tema';
import { Navbat } from './core/navbat';
import { Ikon } from './ui/ikon';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, Ikon],
  template: `
    <router-outlet />
    @if (bildirish.joriy(); as x) {
      <div class="bildirish shisha" [class.xato]="x.xato" role="status" aria-live="polite">
        <ikon [nomi]="x.xato ? 'x' : 'check'" [olcham]="16" [qalinlik]="2.2" /> {{ x.matn }}
      </div>
    }
    @if (yangiVersiya()) {
      <div class="bildirish shisha" style="bottom:auto;top:calc(12px + env(safe-area-inset-top))">
        {{ til.t('YangiVersiya') }}
        <button class="tugma kichik asosiy" (click)="qaytaYukla()">{{ til.t('Yangilash2') }}</button>
      </div>
    }
  `,
})
export class App {
  protected readonly bildirish = inject(Bildirish);
  protected readonly til = inject(Til);
  protected readonly yangiVersiya = signal(false);

  constructor() {
    inject(Tema);
    inject(Navbat); // navbat ilova ochilishi bilan yuborishni boshlaydi
    const sw = inject(SwUpdate);
    if (sw.isEnabled) {
      sw.versionUpdates.pipe(filter((e) => e.type === 'VERSION_READY')).subscribe(() => this.yangiVersiya.set(true));
    }
  }

  qaytaYukla() {
    location.reload();
  }
}
