import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Auth } from './auth';
import type { Ruxsat } from '../api/turlar';

export interface Bolim {
  yol: string;
  kalit: string;
  /** Tab-bar uchun qisqa nom. */
  qisqaKalit?: string;
  ikon: string;
  /** Bo'lim ko'rinishi uchun shart. */
  korinadi: (a: Auth) => boolean;
}

/** Menyu tartibi: boshliq uchun avval hisobotlar, operator uchun sotuv. */
export const BOLIMLAR: Bolim[] = [
  { yol: 'boshqaruv', kalit: 'BoshqaruvPaneli', qisqaKalit: 'Boshqaruv', ikon: 'grid', korinadi: (a) => a.bor('Boshqaruv') },
  { yol: 'sotuv', kalit: 'SotuvKiritish', qisqaKalit: 'Sotuv', ikon: 'plus', korinadi: (a) => a.bor('SotuvKiritish') },
  { yol: 'hisobotlar', kalit: 'Hisobotlar', ikon: 'file', korinadi: (a) => a.bor('Hisobotlar') },
  { yol: 'smenalar', kalit: 'Smenalar', ikon: 'clock', korinadi: (a) => a.bor('Smenalar') },
  { yol: 'operatorlar', kalit: 'OperatorlarHisobi', qisqaKalit: 'Operatorlar', ikon: 'users', korinadi: (a) => a.bor('Operatorlar') },
  // Operatorlar ruxsati yo'q, lekin sotuv kirituvchi — o'z hisob-varaqasini ko'radi (API o'z id'si uchun ruxsat beradi).
  { yol: 'hisobim', kalit: 'MeningHisobim', ikon: 'user', korinadi: (a) => !a.bor('Operatorlar') && a.bor('SotuvKiritish') },
  { yol: 'audit', kalit: 'AuditJurnali', ikon: 'shield', korinadi: (a) => a.bor('Audit') },
];

export function korinadiganlar(a: Auth): Bolim[] {
  const r = BOLIMLAR.filter((b) => b.korinadi(a));
  // Boshliq uchun sotuv kiritish ikkinchi darajali — oxiriga suriladi.
  if (a.bor('Boshqaruv') || a.bor('Hisobotlar')) {
    const i = r.findIndex((b) => b.yol === 'sotuv');
    if (i >= 0) r.push(...r.splice(i, 1));
  }
  return r;
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
