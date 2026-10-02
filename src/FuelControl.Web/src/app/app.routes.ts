import { Routes } from '@angular/router';
import { boshSahifa, kirganmi, kirmagan, ruxsat } from './core/bolimlar';
import { Qobiq } from './qobiq/qobiq';

// Hash marshrutlash (/#/smenalar): API yo'llari (/smenalar, /sotuvlar…) bilan bir domenda to'qnashmaydi
// va API'da SPA fallback sozlashni talab qilmaydi.
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
      { path: 'hisobotlar', canActivate: [ruxsat('Hisobotlar')], loadComponent: () => import('./sahifalar/hisobotlar/hisobotlar').then((m) => m.HisobotlarSahifa) },
      { path: 'smenalar', canActivate: [ruxsat('Smenalar')], loadComponent: () => import('./sahifalar/smenalar/smenalar').then((m) => m.SmenalarSahifa) },
      { path: 'smenalar/:id', loadComponent: () => import('./sahifalar/smenalar/smena-tafsilot').then((m) => m.SmenaTafsilotSahifa) },
      { path: 'sotuv', canActivate: [ruxsat('SotuvKiritish')], loadComponent: () => import('./sahifalar/sotuv/sotuv').then((m) => m.SotuvSahifa) },
      { path: 'operatorlar', canActivate: [ruxsat('Operatorlar')], loadComponent: () => import('./sahifalar/operatorlar/operatorlar').then((m) => m.OperatorlarSahifa) },
      { path: 'operatorlar/:id', canActivate: [ruxsat('Operatorlar')], loadComponent: () => import('./sahifalar/operatorlar/operator-hisob').then((m) => m.OperatorHisobSahifa) },
      { path: 'hisobim', loadComponent: () => import('./sahifalar/operatorlar/operator-hisob').then((m) => m.OperatorHisobSahifa) },
      { path: 'audit', canActivate: [ruxsat('Audit')], loadComponent: () => import('./sahifalar/audit/audit').then((m) => m.AuditSahifa) },
      { path: 'sozlamalar', loadComponent: () => import('./sahifalar/sozlamalar/sozlamalar').then((m) => m.SozlamalarSahifa) },
    ],
  },
  { path: '**', redirectTo: '' },
];
