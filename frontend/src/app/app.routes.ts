import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'comissoes' },
  {
    path: 'comissoes',
    title: 'Comissões · Painel Comercial',
    loadComponent: () => import('./comissoes/comissoes').then((m) => m.Comissoes),
  },
  {
    path: 'estoque',
    title: 'Estoque · Painel Comercial',
    loadComponent: () => import('./estoque/estoque').then((m) => m.Estoque),
  },
  {
    path: 'juros',
    title: 'Juros · Painel Comercial',
    loadComponent: () => import('./juros/juros').then((m) => m.Juros),
  },
  { path: '**', redirectTo: 'comissoes' },
];
