import { Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { Auth } from '../core/auth';
import { Til } from '../core/til';
import { Tema } from '../core/tema';
import { Aloqa } from '../core/aloqa';
import { korinadiganlar } from '../core/bolimlar';
import { Ikon } from '../ui/ikon';

/** Asosiy qobiq: telefonda pastki tab-bar, keng ekranda yon menyu; yuqorida aloqa banneri. */
@Component({
  selector: 'qobiq',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, Ikon],
  templateUrl: './qobiq.html',
  styleUrl: './qobiq.scss',
})
export class Qobiq {
  protected readonly auth = inject(Auth);
  protected readonly til = inject(Til);
  protected readonly tema = inject(Tema);
  protected readonly aloqa = inject(Aloqa);

  protected readonly bolimlar = computed(() => korinadiganlar(this.auth));
  /** Tab-bar: 5 tadan ko'p bo'lsa oxirgisi "Yana" varag'ini ochadi. */
  protected readonly tablar = computed(() => {
    const b = this.bolimlar();
    return b.length <= 4 ? b : b.slice(0, 4);
  });
  protected readonly qolganlar = computed(() => {
    const b = this.bolimlar();
    return b.length <= 4 ? [] : b.slice(4);
  });
  protected readonly yanaOchiq = signal(false);
  protected readonly yanaFaol = signal(false);
  protected readonly bosh = computed(() => {
    const f = this.auth.foydalanuvchi();
    return f ? f.toliqIsm.split(/\s+/).map((s) => s[0]).slice(0, 2).join('').toUpperCase() : '';
  });

  constructor() {
    const router = inject(Router);
    router.events.pipe(filter((e) => e instanceof NavigationEnd)).subscribe((e) => {
      this.yanaOchiq.set(false);
      const yol = (e as NavigationEnd).urlAfterRedirects.split('/')[1]?.split('?')[0] ?? '';
      this.yanaFaol.set(yol === 'sozlamalar' || this.qolganlar().some((b) => b.yol === yol));
    });
    // Ruxsatlar desktop'da o'zgargan bo'lishi mumkin.
    this.auth.yangila();
  }
}
