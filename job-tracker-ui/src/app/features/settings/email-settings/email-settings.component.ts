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
import { GoogleAuthService } from '../../../core/services/google-auth.service';
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
        <!-- Connected via Google OAuth indicator -->
        <div *ngIf="isGoogleConnected" class="google-connected">
          <mat-icon>check_circle</mat-icon>
          <span>Connected to Gmail as <strong>{{ account.emailAddress }}</strong></span>
          <button mat-button color="warn" (click)="deleteAccount()">Disconnect</button>
        </div>

        <div *ngIf="!isGoogleConnected">
          <mat-form-field appearance="outline" class="full-width">
            <mat-label>Email Provider</mat-label>
            <mat-select [(value)]="selectedPreset" (selectionChange)="applyPreset($event.value)">
              <mat-option *ngFor="let preset of presets" [value]="preset">{{ preset.name }}</mat-option>
            </mat-select>
          </mat-form-field>

          <!-- Gmail selected: show Sign in with Google -->
          <div *ngIf="isGmailSelected" class="google-signin-section">
            <p class="gmail-info">
              Gmail requires OAuth authentication. Click below to securely connect your Google account.
            </p>
            <button
              mat-raised-button
              class="google-signin-btn"
              (click)="signInWithGoogle()"
              [disabled]="signingIn">
              <mat-spinner *ngIf="signingIn" diameter="20"></mat-spinner>
              <img *ngIf="!signingIn" src="https://developers.google.com/identity/images/g-logo.png"
                   alt="Google" class="google-logo">
              <span *ngIf="!signingIn">Sign in with Google</span>
            </button>
            <p *ngIf="!googleConfigured" class="config-warning">
              Google OAuth is not configured on the server. Contact your admin to set up Google API credentials.
            </p>
          </div>

          <!-- Non-Gmail: show normal IMAP fields -->
          <div *ngIf="!isGmailSelected">
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
          </div>
        </div>
      </mat-card-content>
      <mat-card-actions align="end" *ngIf="!isGmailSelected && !isGoogleConnected">
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
  styles: [`
    .full-width { width: 100%; }
    mat-card-actions button { margin-left: 8px; }
    .google-signin-section {
      display: flex;
      flex-direction: column;
      align-items: center;
      padding: 24px 0;
    }
    .gmail-info {
      color: rgba(0,0,0,0.6);
      text-align: center;
      margin-bottom: 16px;
    }
    .google-signin-btn {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 8px 24px;
      font-size: 16px;
      background: white !important;
      color: rgba(0,0,0,0.54) !important;
      border: 1px solid #dadce0 !important;
      border-radius: 4px;
      font-weight: 500;
      text-transform: none;
    }
    .google-signin-btn:hover {
      background: #f7f8f8 !important;
      box-shadow: 0 1px 2px 0 rgba(60,64,67,.3);
    }
    .google-logo {
      width: 20px;
      height: 20px;
    }
    .google-connected {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 16px;
      background: #e8f5e9;
      border-radius: 8px;
      margin-bottom: 16px;
    }
    .google-connected mat-icon {
      color: #4caf50;
    }
    .config-warning {
      color: #f44336;
      font-size: 12px;
      margin-top: 12px;
    }
  `],
})
export class EmailSettingsComponent implements OnInit {
  account: EmailAccount = { emailAddress: '', imapServer: '', imapPort: 993, username: '', password: '' };
  testing = false;
  syncing = false;
  signingIn = false;
  selectedPreset: EmailPreset | null = null;
  googleClientId = '';
  googleConfigured = false;
  isGoogleConnected = false;

  presets: EmailPreset[] = [
    { name: 'Gmail', imapServer: 'imap.gmail.com', imapPort: 993 },
    { name: 'Outlook / Hotmail', imapServer: 'outlook.office365.com', imapPort: 993 },
    { name: 'Yahoo Mail', imapServer: 'imap.mail.yahoo.com', imapPort: 993 },
    { name: 'iCloud', imapServer: 'imap.mail.me.com', imapPort: 993 },
  ];

  get isGmailSelected(): boolean {
    return this.selectedPreset?.name === 'Gmail';
  }

  constructor(
    private accountService: EmailAccountService,
    private emailService: EmailService,
    private googleAuth: GoogleAuthService,
    private snackBar: MatSnackBar,
  ) {}

  ngOnInit(): void {
    this.accountService.getAccount().subscribe({
      next: (account: any) => {
        this.account = { ...account, password: '' };
        if (account.authType === 'GoogleOAuth') {
          this.isGoogleConnected = true;
        }
      },
      error: () => {},
    });

    this.googleAuth.getClientId().subscribe({
      next: (res) => {
        this.googleClientId = res.clientId;
        this.googleConfigured = true;
      },
      error: () => { this.googleConfigured = false; },
    });
  }

  applyPreset(preset: EmailPreset): void {
    this.account.imapServer = preset.imapServer;
    this.account.imapPort = preset.imapPort;
  }

  signInWithGoogle(): void {
    if (!this.googleConfigured) {
      this.snackBar.open('Google OAuth is not configured on the server.', 'Close', { duration: 5000 });
      return;
    }

    this.signingIn = true;
    this.googleAuth.initializeAndSignIn(this.googleClientId).subscribe({
      next: (result) => {
        this.signingIn = false;
        this.isGoogleConnected = true;
        this.account.emailAddress = result.email;
        this.snackBar.open(result.message, 'Close', { duration: 3000 });
      },
      error: (err) => {
        this.signingIn = false;
        this.snackBar.open('Google sign-in failed: ' + (err?.message || 'Unknown error'), 'Close', { duration: 5000 });
      },
    });
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
          this.isGoogleConnected = false;
          this.selectedPreset = null;
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
