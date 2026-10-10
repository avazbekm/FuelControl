import { Injectable, signal } from '@angular/core';

/**
 * Smenani yopish formasining qoralamasi: bitta smena ichida sahifadan chiqib-qaytganda kiritilganlar saqlanadi,
 * smena Id o'zgarganda (yangi smena, boshqa qurilmada ochilgan smena ham) hammasi to'liq tozalanadi.
 */
@Injectable({ providedIn: 'root' })
export class YopishQoralama {
  /** Qoralama qaysi smenaga tegishli. */
  private smenaId: number | null = null;
  readonly yangi = signal<Record<number, number | null>>({});
  /** Plastik qatorlari (har biri terminaldagi bitta summa); bo'sh qator — null. Boshida bitta bo'sh qator. */
  readonly plastik = signal<(number | null)[]>([null]);
  readonly depozit = signal<number | null>(null);
  readonly naqd = signal<number | null>(null);
  readonly izoh = signal('');

  /** Qoralamani `id` smenaga bog'laydi; smena almashgan bo'lsa to'liq tozalaydi va `true` qaytaradi (`null` — qoralamani tashlash). */
  smenaga(id: number | null): boolean {
    if (this.smenaId === id) return false;
    this.smenaId = id;
    this.yangi.set({});
    this.plastik.set([null]);
    this.depozit.set(null);
    this.naqd.set(null);
    this.izoh.set('');
    return true;
  }
}
