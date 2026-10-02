import { DestroyRef, Injectable, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, merge, filter } from 'rxjs';
import { api, ol, ApiXato } from '../api/api';
import { Auth } from './auth';
import { Aloqa, HubHodisa } from './aloqa';
import type { Aparat, Yoqilgi } from '../api/turlar';

export type { Yoqilgi };

export interface OperatorElement { id: number; ism: string }

const APARAT_KESH = 'fc.aparatlar';
const YOQILGI_KESH = 'fc.yoqilgilar';

function oqiKesh<T>(kalit: string): T[] {
  try { return JSON.parse(localStorage.getItem(kalit) ?? '[]') as T[]; } catch { return []; }
}

/** Serverdan oladi va keshlaydi; aloqa yo'q (yoki 5xx) bo'lsa keshdagini qaytaradi. */
async function keshli<T>(kalit: string, ol: () => Promise<T[]>): Promise<T[]> {
  try {
    const r = await ol();
    try { localStorage.setItem(kalit, JSON.stringify(r)); } catch { /* */ }
    return r;
  } catch (e) {
    const k = oqiKesh<T>(kalit);
    if (k.length && !(e instanceof ApiXato && e.status < 500)) return k;
    throw e;
  }
}

/** Bir nechta sahifa ishlatadigan ma'lumotlar. */
@Injectable({ providedIn: 'root' })
export class Malumot {
  private readonly auth = inject(Auth);

  /** Aparatlar; offline sotuv kiritish uchun oxirgi ro'yxat localStorage'da saqlanadi. */
  async aparatlar(): Promise<Aparat[]> {
    const r = await keshli(APARAT_KESH, () => ol(api.GET('/aparatlar')));
    return [...r].sort((a, b) => a.raqam - b.raqam);
  }

  /** Yoqilg'i turlari (narx shu yerda). Offline uchun keshlanadi. */
  yoqilgilar(): Promise<Yoqilgi[]> {
    return keshli(YOQILGI_KESH, () => ol(api.GET('/yoqilgilar')));
  }

  keshdan<T>(kalit: 'aparat' | 'yoqilgi'): T[] {
    return oqiKesh<T>(kalit === 'aparat' ? APARAT_KESH : YOQILGI_KESH);
  }

  /** Operatorlar ro'yxati (filtrlar uchun): GET /operatorlar — Operatorlar yoki Hisobotlar ruxsati bilan. */
  async operatorlar(): Promise<OperatorElement[]> {
    if (!this.auth.bor('Operatorlar') && !this.auth.bor('Hisobotlar')) return [];
    const f = await ol(api.GET('/operatorlar'));
    return f.map((x) => ({ id: x.id, ism: x.toliqIsm }));
  }
}

/**
 * Real vaqtda yangilash: hub hodisasi yoki aloqa tiklanganda `yangila` chaqiriladi (300 ms debounce).
 * Komponent konstruktorida chaqiriladi.
 */
export function jonliYangila(yangila: () => void, mos: (h: HubHodisa) => boolean = () => true) {
  const aloqa = inject(Aloqa);
  merge(aloqa.hodisa$.pipe(filter(mos)), aloqa.tiklandi$)
    .pipe(debounceTime(300), takeUntilDestroyed(inject(DestroyRef)))
    .subscribe(() => yangila());
}
