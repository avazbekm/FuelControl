import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Auth } from './auth';
import type { Ruxsat } from '../api/model';

export interface Bolim {
  yol: string;
  kalit: string;
  /** Tab-bar uchun qisqa nom. */
  qisqaKalit?: string;
  ikon: string;
  /** Bo'lim ko'rinishi uchun shart. */
  korinadi: (a: Auth) => boolean;
}

/** Menyu tartibi (docs/dizayn/Menyu): Boshqaruv, Savdo, Smenalar, Nasiyalar, Hisobotlar, Operatorlar, Audit jurnali, Sozlamalar. */
export const BOLIMLAR: Bolim[] = [
  { yol: 'boshqaruv', kalit: 'Boshqaruv', ikon: 'grid', korinadi: (a) => a.bor('Boshqaruv') },
  { yol: 'savdo', kalit: 'Savdo', ikon: 'pump', korinadi: (a) => a.bor('Savdo') },
  { yol: 'smenalar', kalit: 'Smenalar', ikon: 'clock', korinadi: (a) => a.bor('Smenalar') },
  { yol: 'nasiyalar', kalit: 'Nasiyalar', ikon: 'book', korinadi: (a) => a.bor('Nasiyalar') },
  { yol: 'hisobotlar', kalit: 'Hisobotlar', qisqaKalit: 'Tab_Hisobot', ikon: 'file', korinadi: (a) => a.bor('Hisobotlar') },
  { yol: 'operatorlar', kalit: 'Operatorlar', qisqaKalit: 'Tab_Operator', ikon: 'users', korinadi: (a) => a.bor('Operatorlar') },
  { yol: 'audit', kalit: 'AuditJurnali', qisqaKalit: 'Tab_Audit', ikon: 'list', korinadi: (a) => a.bor('Audit') },
  { yol: 'sozlamalar', kalit: 'Sozlamalar', ikon: 'gear', korinadi: (a) => a.bor('Sozlamalar') },
];

export function korinadiganlar(a: Auth): Bolim[] {
  return BOLIMLAR.filter((b) => b.korinadi(a));
}

export const kirganmi: CanActivateFn = () => {
  const a = inject(Auth);
  return a.kirganmi() || inject(Router).parseUrl('/kirish');
};

export const kirmagan: CanActivateFn = () => {
  const a = inject(Auth);
  return !a.kirganmi() || inject(Router).parseUrl('/');
};

export function ruxsat(r: Ruxsat): CanActivateFn {
  return () => {
    const a = inject(Auth);
    return a.bor(r) || inject(Router).parseUrl('/');
  };
}

/** Bosh sahifa: birinchi ruxsat berilgan bo'lim. */
export const boshSahifa: CanActivateFn = () => {
  const a = inject(Auth);
  const b = korinadiganlar(a)[0];
  return inject(Router).parseUrl(b ? `/${b.yol}` : '/sozlamalar');
};
