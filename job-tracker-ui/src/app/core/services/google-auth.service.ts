import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, from, switchMap } from 'rxjs';

declare const google: any;

@Injectable({ providedIn: 'root' })
export class GoogleAuthService {
  private readonly apiUrl = 'api/auth/google';
  private codeClient: any;

  constructor(private http: HttpClient) {}

  getClientId(): Observable<{ clientId: string }> {
    return this.http.get<{ clientId: string }>(`${this.apiUrl}/client-id`);
  }

  initializeAndSignIn(clientId: string): Observable<{ email: string; message: string }> {
    return from(this.requestAuthCode(clientId)).pipe(
      switchMap((code) =>
        this.http.post<{ email: string; message: string }>(`${this.apiUrl}/callback`, { code })
      )
    );
  }

  private requestAuthCode(clientId: string): Promise<string> {
    return new Promise((resolve, reject) => {
      this.codeClient = google.accounts.oauth2.initCodeClient({
        client_id: clientId,
        scope: 'https://mail.google.com/',
        ux_mode: 'popup',
        callback: (response: any) => {
          if (response.code) {
            resolve(response.code);
          } else {
            reject(new Error('No authorization code received'));
          }
        },
        error_callback: (error: any) => {
          reject(new Error(error.type || 'Google sign-in failed'));
        },
      });
      this.codeClient.requestCode();
    });
  }
}
