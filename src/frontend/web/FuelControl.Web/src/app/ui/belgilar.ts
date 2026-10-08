import { Component, computed, input } from '@angular/core';

/** Yoqilg'i turi pill'i: rangli nuqta + nom ("● AI-92"); rang — yoqilg'i rangidan (`--r`). */
@Component({
  selector: 'yoqilgi-pill',
  template: `<span class="yoqilgi-pill" [style.--r]="rang()">{{ nomi() }}</span>`,
  styles: `:host { display: inline-flex; }`,
})
export class YoqilgiPill {
  readonly nomi = input.required<string>();
  readonly rang = input('#2563EB');
}

/** Mashina raqami: "01 A 777 BC" → ramkali "01 | A 777 BC". */
@Component({
  selector: 'mashina-raqami',
  template: `<span class="mashina-raqami"><span class="viloyat">{{ qism()[0] }}</span>{{ qism()[1] }}</span>`,
  styles: `:host { display: inline-flex; }`,
})
export class MashinaRaqami {
  readonly qiymat = input.required<string>();
  protected readonly qism = computed<[string, string]>(() => {
    const t = this.qiymat().trim().replace(/\s+/g, ' ');
    const i = t.indexOf(' ');
    return i > 0 && i <= 3 ? [t.slice(0, i), t.slice(i + 1)] : [t, ''];
  });
}
