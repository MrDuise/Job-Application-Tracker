import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent),
  },
  {
    path: 'applications',
    loadComponent: () =>
      import('./features/applications/application-list/application-list.component').then(m => m.ApplicationListComponent),
  },
  {
    path: 'applications/:id',
    loadComponent: () =>
      import('./features/applications/application-detail/application-detail.component').then(m => m.ApplicationDetailComponent),
  },
  {
    path: 'kanban',
    loadComponent: () =>
      import('./features/applications/application-kanban/application-kanban.component').then(m => m.ApplicationKanbanComponent),
  },
  {
    path: 'settings',
    loadComponent: () =>
      import('./features/settings/email-settings/email-settings.component').then(m => m.EmailSettingsComponent),
  },
];
