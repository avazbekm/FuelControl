import { Component, computed, input } from '@angular/core';

// Desktop Controls/Ikon.cs dagi bilan bir xil ingichka chiziqli ikonkalar (24×24, stroke).
const YOLLAR: Record<string, string> = {
  grid: 'M3 3h7v7H3z M14 3h7v7h-7z M3 14h7v7H3z M14 14h7v7h-7z',
  plus: 'M12 5v14 M5 12h14',
  clock: 'M21 12A9 9 0 1 1 3 12A9 9 0 1 1 21 12 M12 7v5l3 2',
  file: 'M6 3h8l4 4v14H6z M9 13h6 M9 17h6',
  users: 'M12.5 8A3.5 3.5 0 1 1 5.5 8A3.5 3.5 0 1 1 12.5 8 M3 20c0-3.3 2.7-6 6-6s6 2.7 6 6 M19.5 9A2.5 2.5 0 1 1 14.5 9A2.5 2.5 0 1 1 19.5 9 M21 19c0-2.5-1.8-4.5-4-4.8',
  list: 'M4 6h16 M4 12h16 M4 18h16',
  gear: 'M15 12A3 3 0 1 1 9 12A3 3 0 1 1 15 12 M12 2v3 M12 19v3 M2 12h3 M19 12h3 M4.9 4.9l2.1 2.1 M17 17l2.1 2.1 M4.9 19.1L7 17 M17 7l2.1-2.1',
  power: 'M12 3v9 M6.3 6.3a8 8 0 1 0 11.4 0',
  drop: 'M12 3s6 6.5 6 11a6 6 0 0 1-12 0c0-4.5 6-11 6-11z',
  download: 'M12 4v11 M7 10l5 5 5-5 M4 19h16',
  refresh: 'M20 12a8 8 0 1 1-2.3-5.7 M20 4v5h-5',
  cash: 'M4 6h16a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2z M15 12A3 3 0 1 1 9 12A3 3 0 1 1 15 12',
  card: 'M4 5h16a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V7a2 2 0 0 1 2-2z M2 10h20',
  phone: 'M9 2h6a2 2 0 0 1 2 2v16a2 2 0 0 1-2 2H9a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2z M11 18h2',
  mix: 'M4 8h13l-3-3 M20 16H7l3 3',
  check: 'M5 12l5 5L20 7',
  excel: 'M6 3h8l4 4v14H6z M9 11l6 6 M15 11l-6 6',
  arrow: 'M5 12h14 M13 6l6 6-6 6',
  search: 'M18 11A7 7 0 1 1 4 11A7 7 0 1 1 18 11 M20 20l-4-4',
  pump: 'M5 21V5a2 2 0 0 1 2-2h6a2 2 0 0 1 2 2v16 M3 21h14 M15 9h2a2 2 0 0 1 2 2v6a1.5 1.5 0 0 0 3 0V9l-2-2 M8 6h4v4H8z',
  up: 'M7 14l5-5 5 5',
  down: 'M7 10l5 5 5-5',
  back: 'M19 12H5 M11 6l-6 6 6 6',
  backspace: 'M21 6H8l-5 6 5 6h13a1 1 0 0 0 1-1V7a1 1 0 0 0-1-1z M12 9l6 6 M18 9l-6 6',
  save: 'M5 3h11l3 3v15H5z M8 3v6h8V3 M8 21v-7h8v7',
  user: 'M16 8A4 4 0 1 1 8 8A4 4 0 1 1 16 8 M4 21c0-4.4 3.6-8 8-8s8 3.6 8 8',
  edit: 'M12 20h9 M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4z',
  sun: 'M17 12A5 5 0 1 1 7 12A5 5 0 1 1 17 12 M12 1v2 M12 21v2 M4.2 4.2l1.4 1.4 M18.4 18.4l1.4 1.4 M1 12h2 M21 12h2 M4.2 19.8l1.4-1.4 M18.4 5.6l1.4-1.4',
  moon: 'M21 12.8A9 9 0 1 1 11.2 3a7 7 0 0 0 9.8 9.8z',
  shield: 'M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6l8-3z M9 12l2 2 4-4',
  play: 'M6 4l14 8-14 8z',
  stop: 'M5 5h14v14H5z',
  lock: 'M6 11h12v10H6z M8 11V7a4 4 0 0 1 8 0v4',
  x: 'M6 6l12 12 M18 6L6 18',
  more: 'M5 12h.01 M12 12h.01 M19 12h.01',
  wifiOff: 'M2 2l20 20 M8.5 16.5a5 5 0 0 1 7 0 M5 12.9a10 10 0 0 1 5.2-2.8 M19 12.9a10 10 0 0 0-3-2 M2 8.8a15 15 0 0 1 4.2-2.6 M22 8.8A15 15 0 0 0 11 5 M12 20h.01',
  globe: 'M21 12A9 9 0 1 1 3 12A9 9 0 1 1 21 12 M3 12h18 M12 3c2.5 2.7 3.8 5.7 3.8 9s-1.3 6.3-3.8 9c-2.5-2.7-3.8-5.7-3.8-9S9.5 5.7 12 3z',
  chevronLeft: 'M15 18l-6-6 6-6',
  chevronRight: 'M9 18l6-6-6-6',
  upload: 'M12 20V9 M7 14l5-5 5 5 M4 5h16',
  share: 'M12 15V3 M8 7l4-4 4 4 M6 11H5v10h14V11h-1',
  install: 'M12 3v12 M7 10l5 5 5-5 M5 21h14',
  undo: 'M9 14L4 9l5-5 M4 9h10.5a5.5 5.5 0 0 1 0 11H11',
  info: 'M12 21a9 9 0 1 0 0-18a9 9 0 0 0 0 18z M12 11v5 M12 8h.01',
  database: 'M5 6c0-1.66 3.13-3 7-3s7 1.34 7 3v12c0 1.66-3.13 3-7 3s-7-1.34-7-3z M5 6c0 1.66 3.13 3 7 3s7-1.34 7-3 M5 12c0 1.66 3.13 3 7 3s7-1.34 7-3',
  wallet: 'M19 7V5H5a2 2 0 0 0 0 4h15v10H5a2 2 0 0 1-2-2V7 M16 14h.01',
  warning: 'M12 9v4 M12 17h.01 M10.3 3.9L1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0z',
  receipt: 'M6 3h12v18l-3-2-3 2-3-2-3 2z M9 8h6 M9 12h6 M9 16h3',
  book: 'M5 4.5A1.5 1.5 0 0 1 6.5 3H19v15H6.5A1.5 1.5 0 0 0 5 19.5z M5 19.5A1.5 1.5 0 0 0 6.5 21H19 M9 7h6 M9 11h6',
  truck: 'M2 6h12v10H2z M14 10h4l3 3v3h-7 M6.5 19a1.5 1.5 0 1 0 0-3a1.5 1.5 0 0 0 0 3z M17.5 19a1.5 1.5 0 1 0 0-3a1.5 1.5 0 0 0 0 3z',
  trash: 'M4 7h16 M10 11v6 M14 11v6 M6 7l1 13h10l1-13 M9 7V4h6v3',
  tag: 'M3 12V4a1 1 0 0 1 1-1h8l9 9-9 9z M7.5 7.5h.01',
};

@Component({
  selector: 'ikon',
  template: `<svg viewBox="0 0 24 24" [attr.width]="olcham()" [attr.height]="olcham()" fill="none" stroke="currentColor"
    [attr.stroke-width]="qalinlik()" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path [attr.d]="yol()" /></svg>`,
  styles: `:host { display: inline-flex; flex: none; line-height: 0; }`,
})
export class Ikon {
  readonly nomi = input.required<string>();
  readonly olcham = input(20);
  readonly qalinlik = input(1.7);
  protected readonly yol = computed(() => YOLLAR[this.nomi()] ?? YOLLAR['grid']);
}
