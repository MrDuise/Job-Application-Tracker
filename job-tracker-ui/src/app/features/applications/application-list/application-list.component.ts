import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatSortModule, Sort } from '@angular/material/sort';
import { ApplicationService } from '../../../core/services/application.service';
import { Application, ApplicationStatus } from '../../../core/models/application.model';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { DateAgoPipe } from '../../../shared/pipes/date-ago.pipe';

@Component({
  selector: 'app-application-list',
  standalone: true,
  imports: [
    CommonModule, RouterLink, FormsModule, MatTableModule, MatButtonModule,
    MatIconModule, MatSelectModule, MatFormFieldModule, MatInputModule,
    MatDialogModule, MatSortModule, StatusBadgeComponent, DateAgoPipe,
  ],
  template: `
    <div class="header">
      <h1>Applications</h1>
      <button mat-raised-button color="primary" routerLink="/applications/new">
        <mat-icon>add</mat-icon> New Application
      </button>
    </div>

    <div class="status-counters">
      <div class="counter-card total" [class.active]="statusFilter === ''" (click)="filterByStatus('')">
        <div class="counter-value">{{ applications.length }}</div>
        <div class="counter-label">Total</div>
      </div>
      <div class="counter-card applied" [class.active]="statusFilter === 'Applied'" (click)="filterByStatus('Applied')">
        <div class="counter-value">{{ countByStatus('Applied') }}</div>
        <div class="counter-label">Applied</div>
      </div>
      <div class="counter-card under-review" [class.active]="statusFilter === 'UnderReview'" (click)="filterByStatus('UnderReview')">
        <div class="counter-value">{{ countByStatus('UnderReview') }}</div>
        <div class="counter-label">Under Review</div>
      </div>
      <div class="counter-card interview" [class.active]="statusFilter === 'InterviewScheduled'" (click)="filterByStatus('InterviewScheduled')">
        <div class="counter-value">{{ countByStatus('InterviewScheduled') }}</div>
        <div class="counter-label">Interview</div>
      </div>
      <div class="counter-card technical" [class.active]="statusFilter === 'TechnicalAssessment'" (click)="filterByStatus('TechnicalAssessment')">
        <div class="counter-value">{{ countByStatus('TechnicalAssessment') }}</div>
        <div class="counter-label">Technical</div>
      </div>
      <div class="counter-card offer" [class.active]="statusFilter === 'Offer'" (click)="filterByStatus('Offer')">
        <div class="counter-value">{{ countByStatus('Offer') }}</div>
        <div class="counter-label">Offer</div>
      </div>
      <div class="counter-card rejected" [class.active]="statusFilter === 'Rejected'" (click)="filterByStatus('Rejected')">
        <div class="counter-value">{{ countByStatus('Rejected') }}</div>
        <div class="counter-label">Rejected</div>
      </div>
      <div class="counter-card withdrawn" [class.active]="statusFilter === 'Withdrawn'" (click)="filterByStatus('Withdrawn')">
        <div class="counter-value">{{ countByStatus('Withdrawn') }}</div>
        <div class="counter-label">Withdrawn</div>
      </div>
    </div>

    <div class="filters">
      <mat-form-field appearance="outline">
        <mat-label>Search</mat-label>
        <input matInput [(ngModel)]="searchQuery" (ngModelChange)="applyFilter()" placeholder="Search by company or title...">
        <mat-icon matSuffix>search</mat-icon>
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>Status Filter</mat-label>
        <mat-select [(ngModel)]="statusFilter" (ngModelChange)="applyFilter()">
          <mat-option value="">All</mat-option>
          <mat-option *ngFor="let status of statuses" [value]="status">
            {{ formatStatus(status) }}
          </mat-option>
        </mat-select>
      </mat-form-field>
    </div>

    <table mat-table [dataSource]="filteredApplications" matSort (matSortChange)="sortData($event)">
      <ng-container matColumnDef="companyName">
        <th mat-header-cell *matHeaderCellDef mat-sort-header>Company</th>
        <td mat-cell *matCellDef="let app">
          <a [routerLink]="['/applications', app.id]">{{ app.companyName }}</a>
        </td>
      </ng-container>

      <ng-container matColumnDef="jobTitle">
        <th mat-header-cell *matHeaderCellDef mat-sort-header>Position</th>
        <td mat-cell *matCellDef="let app">{{ app.jobTitle }}</td>
      </ng-container>

      <ng-container matColumnDef="status">
        <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
        <td mat-cell *matCellDef="let app">
          <app-status-badge [status]="app.status"></app-status-badge>
        </td>
      </ng-container>

      <ng-container matColumnDef="appliedDate">
        <th mat-header-cell *matHeaderCellDef mat-sort-header>Applied</th>
        <td mat-cell *matCellDef="let app">{{ app.appliedDate | dateAgo }}</td>
      </ng-container>

      <ng-container matColumnDef="emailCount">
        <th mat-header-cell *matHeaderCellDef>Emails</th>
        <td mat-cell *matCellDef="let app">{{ app.emailCount }}</td>
      </ng-container>

      <ng-container matColumnDef="actions">
        <th mat-header-cell *matHeaderCellDef>Actions</th>
        <td mat-cell *matCellDef="let app">
          <button mat-icon-button color="warn" (click)="deleteApplication(app.id); $event.stopPropagation()">
            <mat-icon>delete</mat-icon>
          </button>
        </td>
      </ng-container>

      <tr mat-header-row *matHeaderRowDef="displayedColumns"></tr>
      <tr mat-row *matRowDef="let row; columns: displayedColumns;" class="clickable-row"></tr>
    </table>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: center; }
    .status-counters {
      display: flex;
      gap: 12px;
      margin-bottom: 20px;
      flex-wrap: wrap;
    }
    .counter-card {
      flex: 1;
      min-width: 100px;
      padding: 12px 16px;
      border-radius: 8px;
      text-align: center;
      cursor: pointer;
      transition: transform 0.15s, box-shadow 0.15s;
      border: 2px solid transparent;
    }
    .counter-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 8px rgba(0,0,0,0.12);
    }
    .counter-card.active {
      border-color: rgba(0,0,0,0.4);
    }
    .counter-value {
      font-size: 1.8rem;
      font-weight: 700;
      line-height: 1;
    }
    .counter-label {
      font-size: 0.75rem;
      margin-top: 4px;
      font-weight: 500;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }
    .counter-card.total { background: #e3f2fd; color: #0d47a1; }
    .counter-card.applied { background: #bbdefb; color: #1565c0; }
    .counter-card.under-review { background: #fff3e0; color: #e65100; }
    .counter-card.interview { background: #e1bee7; color: #6a1b9a; }
    .counter-card.technical { background: #d1c4e9; color: #4527a0; }
    .counter-card.offer { background: #c8e6c9; color: #2e7d32; }
    .counter-card.rejected { background: #ffcdd2; color: #c62828; }
    .counter-card.withdrawn { background: #f5f5f5; color: #616161; }
    .filters { display: flex; gap: 16px; margin-bottom: 16px; }
    .filters mat-form-field { flex: 1; }
    table { width: 100%; }
    a { color: #1976d2; text-decoration: none; }
    a:hover { text-decoration: underline; }
    .clickable-row:hover { background-color: #f5f5f5; }
  `],
})
export class ApplicationListComponent implements OnInit {
  applications: Application[] = [];
  filteredApplications: Application[] = [];
  searchQuery = '';
  statusFilter = '';
  displayedColumns = ['companyName', 'jobTitle', 'status', 'appliedDate', 'emailCount', 'actions'];
  statuses = Object.values(ApplicationStatus);

  constructor(private applicationService: ApplicationService) {}

  ngOnInit(): void {
    this.loadApplications();
  }

  loadApplications(): void {
    this.applicationService.getAll().subscribe((apps) => {
      this.applications = apps;
      this.applyFilter();
    });
  }

  countByStatus(status: string): number {
    return this.applications.filter(app => app.status === status).length;
  }

  filterByStatus(status: string): void {
    this.statusFilter = status;
    this.applyFilter();
  }

  applyFilter(): void {
    this.filteredApplications = this.applications.filter((app) => {
      const matchesSearch = !this.searchQuery ||
        app.companyName.toLowerCase().includes(this.searchQuery.toLowerCase()) ||
        app.jobTitle.toLowerCase().includes(this.searchQuery.toLowerCase());
      const matchesStatus = !this.statusFilter || app.status === this.statusFilter;
      return matchesSearch && matchesStatus;
    });
  }

  sortData(sort: Sort): void {
    if (!sort.active || sort.direction === '') {
      this.filteredApplications = [...this.filteredApplications];
      return;
    }

    this.filteredApplications.sort((a, b) => {
      const isAsc = sort.direction === 'asc';
      switch (sort.active) {
        case 'companyName': return compare(a.companyName, b.companyName, isAsc);
        case 'jobTitle': return compare(a.jobTitle, b.jobTitle, isAsc);
        case 'status': return compare(a.status, b.status, isAsc);
        case 'appliedDate': return compare(a.appliedDate, b.appliedDate, isAsc);
        default: return 0;
      }
    });
  }

  deleteApplication(id: number): void {
    if (confirm('Are you sure you want to delete this application?')) {
      this.applicationService.delete(id).subscribe(() => this.loadApplications());
    }
  }

  formatStatus(status: string): string {
    return status.replace(/([A-Z])/g, ' $1').trim();
  }
}

function compare(a: string, b: string, isAsc: boolean): number {
  return (a < b ? -1 : 1) * (isAsc ? 1 : -1);
}
