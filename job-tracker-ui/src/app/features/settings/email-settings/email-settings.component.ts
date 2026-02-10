import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { EmailAccountService } from '../../../core/services/email-account.service';
import { EmailService } from '../../../core/services/email.service';
import { EmailAccount } from '../../../core/models/application.model';

interface EmailPreset {
  name: string;
  imapServer: string;
  imapPort: number;
}

@Component({
  selector: 'app-email-settings',
  standalone: true,
  imports: [
    CommonModule, FormsModule, MatCardModule, MatButtonModule,
    MatFormFieldModule, MatInputModule, MatSelectModule, MatIconModule,
    MatSnackBarModule, MatProgressSpinnerModule,
  ],
  template: `
    <h1>Email Settings</h1>
    <mat-card>
      <mat-card-header>
        <mat-card-title>Email Account Configuration</mat-card-title>
        <mat-card-subtitle>Connect your email to automatically track job applications</mat-card-subtitle>
      </mat-card-header>
      <mat-card-content>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Email Provider</mat-label>
          <mat-select (selectionChange)="applyPreset($event.value)">
            <mat-option *ngFor="let preset of presets" [value]="preset">{{ preset.name }}</mat-option>
          </mat-select>
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Email Address</mat-label>
          <input matInput [(ngModel)]="account.emailAddress" type="email">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>IMAP Server</mat-label>
          <input matInput [(ngModel)]="account.imapServer">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>IMAP Port</mat-label>
          <input matInput [(ngModel)]="account.imapPort" type="number">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Username</mat-label>
          <input matInput [(ngModel)]="account.username">
        </mat-form-field>
        <mat-form-field appearance="outline" class="full-width">
          <mat-label>Password / App Password</mat-label>
          <input matInput [(ngModel)]="account.password" type="password">
        </mat-form-field>
      </mat-card-content>
      <mat-card-actions align="end">
        <button mat-button (click)="testConnection()" [disabled]="testing">
          <mat-spinner *ngIf="testing" diameter="20"></mat-spinner>
          <span *ngIf="!testing">Test Connection</span>
        </button>
        <button mat-raised-button color="primary" (click)="saveAccount()">Save</button>
        <button mat-button color="warn" (click)="deleteAccount()">Delete</button>
      </mat-card-actions>
    </mat-card>
    <mat-card style="margin-top: 16px">
      <mat-card-header><mat-card-title>Manual Sync</mat-card-title></mat-card-header>
      <mat-card-content><p>Trigger a manual email sync to fetch and process new emails.</p></mat-card-content>
      <mat-card-actions>
        <button mat-raised-button (click)="triggerSync()" [disabled]="syncing">
          <mat-spinner *ngIf="syncing" diameter="20"></mat-spinner>
          <span *ngIf="!syncing">Sync Now</span>
        </button>
      </mat-card-actions>
    </mat-card>
  `,
  styles: [`.full-width { width: 100%; } mat-card-actions button { margin-left: 8px; }`],
})
export class EmailSettingsComponent implements OnInit {
  account: EmailAccount = { emailAddress: '', imapServer: '', imapPort: 993, username: '', password: '' };
  testing = false;
  syncing = false;
  presets: EmailPreset[] = [
    { name: 'Gmail', imapServer: 'imap.gmail.com', imapPort: 993 },
    { name: 'Outlook / Hotmail', imapServer: 'outlook.office365.com', imapPort: 993 },
    { name: 'Yahoo Mail', imapServer: 'imap.mail.yahoo.com', imapPort: 993 },
    { name: 'iCloud', imapServer: 'imap.mail.me.com', imapPort: 993 },
  ];

  constructor(
    private accountService: EmailAccountService,
    private emailService: EmailService,
    private snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    this.accountService.getAccount().subscribe({
      next: (account) => { this.account = { ...account, password: '' }; },
      error: () => {},
    });
  }

  applyPreset(preset: EmailPreset): void {
    this.account.imapServer = preset.imapServer;
    this.account.imapPort = preset.imapPort;
  }

  testConnection(): void {
    this.testing = true;
    this.accountService.testConnection(this.account).subscribe({
      next: (result) => {
        this.testing = false;
        this.snackBar.open(result.success ? 'Connection successful!' : 'Connection failed.', 'Close', { duration: 3000 });
      },
      error: () => { this.testing = false; this.snackBar.open('Connection test failed.', 'Close', { duration: 3000 }); },
    });
  }

  saveAccount(): void {
    this.accountService.createOrUpdate(this.account).subscribe({
      next: () => this.snackBar.open('Account saved!', 'Close', { duration: 3000 }),
      error: () => this.snackBar.open('Failed to save account.', 'Close', { duration: 3000 }),
    });
  }

  deleteAccount(): void {
    if (confirm('Are you sure you want to delete this email account?')) {
      this.accountService.delete().subscribe({
        next: () => {
          this.account = { emailAddress: '', imapServer: '', imapPort: 993, username: '', password: '' };
          this.snackBar.open('Account deleted.', 'Close', { duration: 3000 });
        },
      });
    }
  }

  triggerSync(): void {
    this.syncing = true;
    this.emailService.triggerSync().subscribe({
      next: (result) => {
        this.syncing = false;
        this.snackBar.open('Sync complete! ' + result.processed + ' emails processed.', 'Close', { duration: 3000 });
      },
      error: () => { this.syncing = false; this.snackBar.open('Sync failed.', 'Close', { duration: 3000 }); },
    });
  }
}
