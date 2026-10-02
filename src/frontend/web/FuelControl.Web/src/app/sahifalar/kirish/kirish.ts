import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Auth } from '../../core/auth';
import { Til, TilKodi } from '../../core/til';
import { Tema } from '../../core/tema';
import { ApiXato, AloqaXato } from '../../api/api';
import { Ikon } from '../../ui/ikon';
import { OrnatishTaklif } from '../../ui/ornatish-taklif';

@Component({
  selector: 'kirish-sahifa',
  imports: [FormsModule, Ikon, OrnatishTaklif],
  templateUrl: './kirish.html',
  styleUrl: './kirish.scss',
})
export class KirishSahifa {
  protected readonly auth = inject(Auth);
  protected readonly til = inject(Til);
  protected readonly tema = inject(Tema);
  private readonly router = inject(Router);

  protected readonly tillar: { kod: TilKodi; nom: string }[] = [
    { kod: 'uz', nom: 'UZ' }, { kod: 'uzk', nom: 'ЎЗ' }, { kod: 'ru', nom: 'RU' },
  ];
  protected login = '';
  protected parol = '';
  protected eslab = true;
  protected readonly pinRejim = signal(false);
  protected readonly yuklanmoqda = signal(false);
  protected readonly xato = signal<string | null>(null);
  protected readonly raqamlar = ['1', '2', '3', '4', '5', '6', '7', '8', '9', '', '0', '<'];

  tugmaBos(r: string) {
    if (r === '<') this.parol = this.parol.slice(0, -1);
    else if (r && this.parol.length < 12) this.parol += r;
  }

  async kir() {
    this.xato.set(null);
    if (!this.login.trim()) return this.xato.set(this.til.t('LoginKiriting'));
    if (!this.parol) return this.xato.set(this.til.t('ParolniKiriting'));
    this.yuklanmoqda.set(true);
    try {
      await this.auth.kirish(this.login.trim(), this.parol, this.eslab);
      this.router.navigateByUrl('/');
    } catch (e) {
      this.parol = '';
      this.xato.set(
        e instanceof AloqaXato ? this.til.t('AloqaYoq')
        : e instanceof ApiXato && e.status === 401 ? this.til.t('Xato_Login')
        : e instanceof ApiXato ? e.message
        : this.til.t('Xato_Umumiy'));
    } finally {
      this.yuklanmoqda.set(false);
    }
  }
}
