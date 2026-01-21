import { Routes } from '@angular/router';

import { BookListComponent } from './features/book/book-list/book-list.component';
import { AddBookComponent } from './features/book/add-book/add-book.component';
import { EditBookComponent } from './features/book/edit-book/edit-book.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { LoginComponent } from './features/auth/login/login.component';
import { RegisterComponent } from './features/auth/register/register.component';
import { AuthGuard } from './core/guards/auth.guard';
import { MainLayoutComponent } from './shared/components/main-layout/main-layout.component';
import { BookPerformanceComponent } from './features/book/book-performance/book-performance.component';
import { PlatformsComponent } from './features/platforms/platforms.component';
import { RequestPlatformComponent } from './features/platforms/request-platform/request-platform.component';

export const routes: Routes = [
    {
        path: '',
        component: MainLayoutComponent,
        canActivate: [AuthGuard],
        children: [
            { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
            { path: 'dashboard', component: DashboardComponent },
            { path: 'books', component: BookListComponent },
            { path: 'books/add', component: AddBookComponent },
            { path: 'books/edit/:id', component: EditBookComponent },
            { path: 'books/:id/performance', component: BookPerformanceComponent },
            { path: 'platforms', component: PlatformsComponent },
            { path: 'platforms/request', component: RequestPlatformComponent },
        ]
    },
    { path: 'login', component: LoginComponent },
    { path: 'register', component: RegisterComponent },
];
