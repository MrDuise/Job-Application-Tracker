import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatButtonModule } from '@angular/material/button';
import { DashboardService } from '../../core/services/dashboard.service';
import { DashboardStats } from '../../core/models/application.model';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { DateAgoPipe } from '../../shared/pipes/date-ago.pipe';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [
    CommonModule, RouterLink, MatCardModule, MatIconModule,
    MatListModule, MatButtonModule, StatusBadgeComponent, DateAgoPipe,
  ],
  template: `
    <h1>Dashboard</h1>

    <div class="stats-grid" *ngIf="stats">
      <mat-card class="stat-card">
        <mat-card-header>
          <mat-icon mat-card-avatar>work</mat-icon>
          <mat-card-title>{{ stats.totalApplications }}</mat-card-title>
          <mat-card-subtitle>Total Applications</mat-card-subtitle>
        </mat-card-header>
      </mat-card>

      <mat-card class="stat-card" *ngFor="let entry of statusEntries">
        <mat-card-header>
          <mat-card-title>{{ entry[1] }}</mat-card-title>
          <mat-card-subtitle>{{ formatStatus(entry[0]) }}</mat-card-subtitle>
        </mat-card-header>
      </mat-card>
    </div>

    <h2>Recent Activity</h2>
    <mat-card *ngIf="stats">
      <mat-list>
        <mat-list-item *ngFor="let activity of stats.recentActivity">
          <mat-icon matListItemIcon>business</mat-icon>
          <span matListItemTitle>
            <a [routerLink]="['/applications', activity.applicationId]">
              {{ activity.companyName }} - {{ activity.jobTitle }}
            </a>
          </span>
          <span matListItemLine>
            <app-status-badge [status]="activity.status"></app-status-badge>
            <span class="date">{{ activity.date | dateAgo }}</span>
          </span>
        </mat-list-item>
        <mat-list-item *ngIf="stats.recentActivity.length === 0">
          <span matListItemTitle>No applications yet. Start tracking!</span>
        </mat-list-item>
      </mat-list>
    </mat-card>
  `,
  styles: [`
    .stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
      gap: 16px;
      margin-bottom: 24px;
    }
    .stat-card mat-card-title { font-size: 2rem; }
    .date { margin-left: 8px; color: #666; font-size: 12px; }
    a { color: inherit; text-decoration: none; }
    a:hover { text-decoration: underline; }
  `],
})
export class DashboardComponent implements OnInit {
  stats: DashboardStats | null = null;
  statusEntries: [string, number][] = [];

  constructor(private dashboardService: DashboardService) {}

  ngOnInit(): void {
    this.dashboardService.getStats().subscribe((stats) => {
      this.stats = stats;
      this.statusEntries = Object.entries(stats.statusCounts);
    });
  }

  formatStatus(status: string): string {
    return status.replace(/([A-Z])/g, ' $1').trim();
  }
}
