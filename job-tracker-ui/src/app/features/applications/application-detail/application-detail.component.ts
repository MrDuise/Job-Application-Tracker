import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatDividerModule } from '@angular/material/divider';
import { ApplicationService } from '../../../core/services/application.service';
import { EmailService, Email } from '../../../core/services/email.service';
import { Application, ApplicationStatus, CreateApplication } from '../../../core/models/application.model';
import { StatusBadgeComponent } from '../../../shared/components/status-badge/status-badge.component';
import { DateAgoPipe } from '../../../shared/pipes/date-ago.pipe';

@Component({
  selector: 'app-application-detail',
  standalone: true,
  imports: [
    CommonModule, RouterLink, FormsModule, MatCardModule, MatButtonModule,
    MatIconModule, MatSelectModule, MatFormFieldModule, MatInputModule,
    MatListModule, MatDividerModule, StatusBadgeComponent, DateAgoPipe,
  ],
  template: `
    <!-- New Application Form -->
    <div *ngIf="isNew">
      <h1>New Application</h1>
      <mat-card>
        <mat-card-content>
          <form (ngSubmit)="createApplication()">
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Company Name</mat-label>
              <input matInput [(ngModel)]="newApp.companyName" name="companyName" required>
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Job Title</mat-label>
              <input matInput [(ngModel)]="newApp.jobTitle" name="jobTitle" required>
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Status</mat-label>
              <mat-select [(ngModel)]="newApp.status" name="status">
                <mat-option *ngFor="let s of statuses" [value]="s">{{ formatStatus(s) }}</mat-option>
              </mat-select>
            </mat-form-field>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Notes</mat-label>
              <textarea matInput [(ngModel)]="newApp.notes" name="notes" rows="3"></textarea>
            </mat-form-field>
            <div class="actions">
              <button mat-button routerLink="/applications">Cancel</button>
              <button mat-raised-button color="primary" type="submit">Create</button>
            </div>
          </form>
        </mat-card-content>
      </mat-card>
    </div>

    <!-- Application Detail View -->
    <div *ngIf="!isNew && application">
      <div class="header">
        <div>
          <h1>{{ application.companyName }}</h1>
          <h3>{{ application.jobTitle }}</h3>
        </div>
        <app-status-badge [status]="application.status"></app-status-badge>
      </div>

      <div class="detail-grid">
        <mat-card>
          <mat-card-header>
            <mat-card-title>Details</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <p><strong>Applied:</strong> {{ application.appliedDate | date:'mediumDate' }}</p>
            <p *ngIf="application.lastUpdated"><strong>Last Updated:</strong> {{ application.lastUpdated | dateAgo }}</p>

            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Update Status</mat-label>
              <mat-select [value]="application.status" (selectionChange)="updateStatus($event.value)">
                <mat-option *ngFor="let s of statuses" [value]="s">{{ formatStatus(s) }}</mat-option>
              </mat-select>
            </mat-form-field>
          </mat-card-content>
        </mat-card>

        <mat-card>
          <mat-card-header>
            <mat-card-title>Notes</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <p class="notes" *ngIf="application.notes">{{ application.notes }}</p>
            <p *ngIf="!application.notes" class="no-data">No notes yet.</p>
            <mat-form-field appearance="outline" class="full-width">
              <mat-label>Add a note</mat-label>
              <textarea matInput [(ngModel)]="newNote" rows="2"></textarea>
            </mat-form-field>
            <button mat-raised-button color="primary" (click)="addNote()" [disabled]="!newNote">Add Note</button>
          </mat-card-content>
        </mat-card>
      </div>

      <h2>Related Emails</h2>
      <mat-card>
        <mat-list>
          <mat-list-item *ngFor="let email of emails">
            <mat-icon matListItemIcon>email</mat-icon>
            <span matListItemTitle>{{ email.subject }}</span>
            <span matListItemLine>From: {{ email.from }} | {{ email.receivedDate | dateAgo }}</span>
          </mat-list-item>
          <mat-list-item *ngIf="emails.length === 0">
            <span matListItemTitle>No emails linked to this application.</span>
          </mat-list-item>
        </mat-list>
      </mat-card>

      <div class="actions" style="margin-top: 16px">
        <button mat-button routerLink="/applications">Back to List</button>
        <button mat-raised-button color="warn" (click)="deleteApplication()">Delete</button>
      </div>
    </div>
  `,
  styles: [`
    .header { display: flex; justify-content: space-between; align-items: flex-start; }
    .detail-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin: 16px 0; }
    .full-width { width: 100%; }
    .notes { white-space: pre-wrap; }
    .no-data { color: #999; font-style: italic; }
    .actions { display: flex; justify-content: flex-end; gap: 8px; }
  `],
})
export class ApplicationDetailComponent implements OnInit {
  application: Application | null = null;
  emails: Email[] = [];
  isNew = false;
  newNote = '';
  newApp: CreateApplication = { companyName: '', jobTitle: '', status: ApplicationStatus.Applied };
  statuses = Object.values(ApplicationStatus);

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private applicationService: ApplicationService,
    private emailService: EmailService,
  ) {}

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id === 'new') {
      this.isNew = true;
    } else if (id) {
      this.loadApplication(+id);
    }
  }

  loadApplication(id: number): void {
    this.applicationService.getById(id).subscribe((app) => {
      this.application = app;
    });
    this.emailService.getByApplication(id).subscribe((emails) => {
      this.emails = emails;
    });
  }

  createApplication(): void {
    this.applicationService.create(this.newApp).subscribe((app) => {
      this.router.navigate(['/applications', app.id]);
    });
  }

  updateStatus(status: ApplicationStatus): void {
    if (this.application) {
      this.applicationService.updateStatus(this.application.id, status).subscribe(() => {
        this.loadApplication(this.application!.id);
      });
    }
  }

  addNote(): void {
    if (this.application && this.newNote) {
      this.applicationService.addNote(this.application.id, this.newNote).subscribe(() => {
        this.newNote = '';
        this.loadApplication(this.application!.id);
      });
    }
  }

  deleteApplication(): void {
    if (this.application && confirm('Are you sure?')) {
      this.applicationService.delete(this.application.id).subscribe(() => {
        this.router.navigate(['/applications']);
      });
    }
  }

  formatStatus(status: string): string {
    return status.replace(/([A-Z])/g, ' $1').trim();
  }
}
