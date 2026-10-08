import { Routes } from '@angular/router';
import { boshSahifa, kirganmi, kirmagan, ruxsat } from './core/bolimlar';
import { Qobiq } from './qobiq/qobiq';

// Hash marshrutlash (/#/smenalar): API yo'llari bilan bir domenda to'qnashmaydi va API'da SPA fallback sozlashni talab qilmaydi.
// Smenalar/Operatorlar: tanlangan yozuv `?smena=41` / `?id=2` so'rov parametri bilan (bitta komponent: keng ekranda ro'yxat + tafsilot, telefonda bittasi).
export const routes: Routes = [
  { path: 'kirish', canActivate: [kirmagan], loadComponent: () => import('./sahifalar/kirish/kirish').then((m) => m.KirishSahifa) },
  {
    path: '',
    component: Qobiq,
    canActivate: [kirganmi],
    canActivateChild: [kirganmi],
    children: [
      { path: '', pathMatch: 'full', canActivate: [boshSahifa], children: [] },
      { path: 'boshqaruv', canActivate: [ruxsat('Boshqaruv')], loadComponent: () => import('./sahifalar/boshqaruv/boshqaruv').then((m) => m.BoshqaruvSahifa) },
      { path: 'savdo', canActivate: [ruxsat('Savdo')], loadComponent: () => import('./sahifalar/savdo/savdo').then((m) => m.SavdoSahifa) },
      { path: 'savdo/yopish', canActivate: [ruxsat('SmenaYopish')], loadComponent: () => import('./sahifalar/savdo/smena-yopish').then((m) => m.SmenaYopishSahifa) },
      { path: 'smenalar', canActivate: [ruxsat('Smenalar')], loadComponent: () => import('./sahifalar/smenalar/smenalar').then((m) => m.SmenalarSahifa) },
      { path: 'nasiyalar', canActivate: [ruxsat('Nasiyalar')], loadComponent: () => import('./sahifalar/nasiyalar/nasiyalar').then((m) => m.NasiyalarSahifa) },
      { path: 'hisobotlar', canActivate: [ruxsat('Hisobotlar')], loadComponent: () => import('./sahifalar/hisobotlar/hisobotlar').then((m) => m.HisobotlarSahifa) },
      { path: 'operatorlar', canActivate: [ruxsat('Operatorlar')], loadComponent: () => import('./sahifalar/operatorlar/operatorlar').then((m) => m.OperatorlarSahifa) },
      { path: 'hisobim', loadComponent: () => import('./sahifalar/operatorlar/operatorlar').then((m) => m.OperatorlarSahifa) },
      { path: 'audit', canActivate: [ruxsat('Audit')], loadComponent: () => import('./sahifalar/audit/audit').then((m) => m.AuditSahifa) },
      { path: 'sozlamalar', loadComponent: () => import('./sahifalar/sozlamalar/sozlamalar').then((m) => m.SozlamalarSahifa) },
    ],
  },
  { path: '**', redirectTo: '' },
];
