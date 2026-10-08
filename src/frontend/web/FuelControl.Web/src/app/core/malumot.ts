import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, merge, filter } from 'rxjs';
import { Auth } from './auth';
import { Aloqa, HubHodisa } from './aloqa';
import { Server } from './server';

export interface OperatorElement { id: number; ism: string }

/** Bir nechta sahifa ishlatadigan ma'lumotlar. */
@Injectable({ providedIn: 'root' })
export class Malumot {
  private readonly auth = inject(Auth);
  private readonly server = inject(Server);

  /** Operatorlar ro'yxati (filtrlar uchun): GET /operatorlar — Operatorlar yoki Hisobotlar ruxsati bilan. */
  async operatorlar(): Promise<OperatorElement[]> {
    if (!this.auth.bor('Operatorlar') && !this.auth.bor('Hisobotlar') && !this.auth.bor('Smenalar')) return [];
    const f = await this.server.operatorlar();
    return f.map((x) => ({ id: x.id, ism: x.toliqIsm }));
  }
}

/**
 * Real vaqtda yangilash: hub hodisasi (SignalR "qayta yuklash" signali) yoki aloqa tiklanganda `yangila` chaqiriladi (300 ms debounce).
 * Komponent konstruktorida chaqiriladi.
 */
export function jonliYangila(yangila: () => void, mos: (h: HubHodisa) => boolean = () => true) {
  const aloqa = inject(Aloqa);
  merge(aloqa.hodisa$.pipe(filter(mos)), aloqa.tiklandi$)
    .pipe(debounceTime(300), takeUntilDestroyed(inject(DestroyRef)))
    .subscribe(() => yangila());
}
