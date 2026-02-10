import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { CdkDragDrop, DragDropModule } from '@angular/cdk/drag-drop';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { ApplicationService } from '../../../core/services/application.service';
import { Application, ApplicationStatus } from '../../../core/models/application.model';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';

interface KanbanColumn {
  status: ApplicationStatus;
  label: string;
  applications: Application[];
}

@Component({
  selector: 'app-application-kanban',
  standalone: true,
  imports: [CommonModule, RouterLink, DragDropModule, MatCardModule, MatIconModule, StatusBadgeComponent],
  template: `
    <h1>Kanban Board</h1>
    <div class="kanban-board">
      <div class="kanban-column" *ngFor="let column of columns">
        <div class="column-header">
          <h3>{{ column.label }}</h3>
          <span class="count">{{ column.applications.length }}</span>
        </div>
        <div
          class="column-content"
          cdkDropList
          [cdkDropListData]="column"
          [id]="column.status"
          [cdkDropListConnectedTo]="columnIds"
          (cdkDropListDropped)="onDrop($event)">
          <mat-card
            *ngFor="let app of column.applications"
            cdkDrag
            [cdkDragData]="app"
            class="kanban-card">
            <mat-card-header>
              <mat-card-title>
                <a [routerLink]="['/applications', app.id]">{{ app.companyName }}</a>
              </mat-card-title>
              <mat-card-subtitle>{{ app.jobTitle }}</mat-card-subtitle>
            </mat-card-header>
          </mat-card>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .kanban-board { display: flex; gap: 12px; overflow-x: auto; padding-bottom: 16px; }
    .kanban-column { min-width: 250px; flex: 1; background: #f5f5f5; border-radius: 8px; padding: 12px; }
    .column-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px; }
    .column-header h3 { margin: 0; font-size: 14px; }
    .count { background: #e0e0e0; border-radius: 12px; padding: 2px 8px; font-size: 12px; }
    .column-content { min-height: 100px; }
    .kanban-card { margin-bottom: 8px; cursor: grab; }
    .kanban-card:active { cursor: grabbing; }
    a { color: inherit; text-decoration: none; }
    a:hover { text-decoration: underline; }
    .cdk-drag-preview { box-shadow: 0 5px 5px -3px rgba(0,0,0,.2); }
    .cdk-drag-placeholder { opacity: 0.3; }
  `],
})
export class ApplicationKanbanComponent implements OnInit {
  columns: KanbanColumn[] = [];
  columnIds: string[] = [];

  private statusLabels: Record<string, string> = {
    [ApplicationStatus.Applied]: 'Applied',
    [ApplicationStatus.UnderReview]: 'Under Review',
    [ApplicationStatus.InterviewScheduled]: 'Interview',
    [ApplicationStatus.TechnicalAssessment]: 'Assessment',
    [ApplicationStatus.Offer]: 'Offer',
    [ApplicationStatus.Rejected]: 'Rejected',
    [ApplicationStatus.Withdrawn]: 'Withdrawn',
  };

  constructor(private applicationService: ApplicationService) {}

  ngOnInit(): void {
    this.columns = Object.values(ApplicationStatus).map((status) => ({
      status,
      label: this.statusLabels[status] || status,
      applications: [],
    }));
    this.columnIds = this.columns.map((c) => c.status);

    this.applicationService.getAll().subscribe((apps) => {
      for (const app of apps) {
        const column = this.columns.find((c) => c.status === app.status);
        column?.applications.push(app);
      }
    });
  }

  onDrop(event: CdkDragDrop<KanbanColumn>): void {
    if (event.previousContainer === event.container) return;
    const app: Application = event.item.data;
    const targetColumn = event.container.data;
    const sourceColumn = event.previousContainer.data;
    sourceColumn.applications = sourceColumn.applications.filter((a: Application) => a.id !== app.id);
    app.status = targetColumn.status;
    targetColumn.applications.push(app);
    this.applicationService.updateStatus(app.id, targetColumn.status).subscribe();
  }
}
