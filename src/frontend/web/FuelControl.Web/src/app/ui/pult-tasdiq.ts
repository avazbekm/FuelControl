import { Component, input, output, signal } from '@angular/core';
import { BakOgohDialog, BakOgohQatori } from './bak-ogoh';
import { KrugQatori, KrugTasdiqDialog } from './krug';

/**
 * Pult ko'rsatkichi saqlanishidan oldingi tasdiqlar zanjiri (docs §8.15–8.16), tartib: krug oshgan bo'lsa "Pult krugi oshdi", keyin bakdagidan ko'p
 * litr chiqsa bak tasdig'i. Host saqlash tugmasida `boshla()` ni chaqiradi; tasdiq kerak bo'lmasa yoki hammasi tasdiqlansa `davom` chiqadi
 * (saqlash yoki yopishda odatdagi "Smenani yopasizmi?").
 * Dialoglar ketma-ket ochilganda "orqaga" tarixi chalkashmasligi uchun keyingi qadam biroz kechiktiriladi.
 */
@Component({
  selector: 'pult-tasdiq',
  imports: [KrugTasdiqDialog, BakOgohDialog],
  template: `
    <krug-tasdiq-dialog [(ochiq)]="krugOchiq" [qatorlar]="krugQatorlari()" (davom)="krugdanKeyin()" />
    <bak-ogoh-dialog [(ochiq)]="bakOchiq" [qatorlar]="bakQatorlari()" (davom)="oxiri()" />
  `,
})
export class PultTasdiq {
  readonly krugQatorlari = input<KrugQatori[]>([]);
  readonly bakQatorlari = input<BakOgohQatori[]>([]);
  readonly davom = output<void>();

  protected readonly krugOchiq = signal(false);
  protected readonly bakOchiq = signal(false);

  /** Zanjirni boshlaydi: hech qanday tasdiq kerak bo'lmasa darhol `davom`. */
  boshla() {
    if (this.krugQatorlari().length) this.krugOchiq.set(true);
    else if (this.bakQatorlari().length) this.bakOchiq.set(true);
    else this.davom.emit();
  }

  /** Krug tasdiqlandi: bak tasdig'i kerak bo'lsa u, aks holda davom. */
  protected krugdanKeyin() {
    if (this.bakQatorlari().length) setTimeout(() => this.bakOchiq.set(true), 200);
    else this.oxiri();
  }

  protected oxiri() { setTimeout(() => this.davom.emit(), 200); }
}
