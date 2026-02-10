import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatChipsModule } from '@angular/material/chips';

@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [CommonModule, MatChipsModule],
  template: `
    <mat-chip [class]="'status-chip status-' + status.toLowerCase().replace(' ', '-')">
      {{ formatStatus(status) }}
    </mat-chip>
  `,
  styles: [`
    .status-chip { font-size: 12px; }
    .status-applied { background-color: #bbdefb !important; color: #1565c0 !important; }
    .status-underreview { background-color: #fff3e0 !important; color: #e65100 !important; }
    .status-interviewscheduled { background-color: #e1bee7 !important; color: #6a1b9a !important; }
    .status-technicalassessment { background-color: #d1c4e9 !important; color: #4527a0 !important; }
    .status-offer { background-color: #c8e6c9 !important; color: #2e7d32 !important; }
    .status-rejected { background-color: #ffcdd2 !important; color: #c62828 !important; }
    .status-withdrawn { background-color: #f5f5f5 !important; color: #616161 !important; }
  `],
})
export class StatusBadgeComponent {
  @Input() status = '';

  formatStatus(status: string): string {
    return status.replace(/([A-Z])/g, ' $1').trim();
  }
}
