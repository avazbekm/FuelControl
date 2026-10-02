import { Injectable, signal } from '@angular/core';
import { ApiXato, AloqaXato } from '../api/api';

export interface Xabar { matn: string; xato: boolean }

/** Pastki qisqa xabar (toast). */
@Injectable({ providedIn: 'root' })
export class Bildirish {
  readonly joriy = signal<Xabar | null>(null);
  private taymer: ReturnType<typeof setTimeout> | undefined;

  korsat(matn: string, xato = false) {
    clearTimeout(this.taymer);
    this.joriy.set({ matn, xato });
    this.taymer = setTimeout(() => this.joriy.set(null), xato ? 5000 : 2600);
  }

  /** Xatoni foydalanuvchiga tushunarli matnga aylantiradi (server ProblemDetails.detail o'zbekcha keladi). */
  xato(e: unknown, aloqaYoqMatni: string, umumiy: string) {
    this.korsat(xatoMatni(e, aloqaYoqMatni, umumiy), true);
  }
}

export function xatoMatni(e: unknown, aloqaYoqMatni: string, umumiy: string): string {
  if (e instanceof AloqaXato) return aloqaYoqMatni;
  if (e instanceof ApiXato) return e.message;
  return umumiy;
}
