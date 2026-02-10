import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { EmailAccount } from '../models/application.model';

@Injectable({ providedIn: 'root' })
export class EmailAccountService {
  private readonly apiUrl = 'api/emailaccounts';

  constructor(private http: HttpClient) {}

  getAccount(): Observable<EmailAccount> {
    return this.http.get<EmailAccount>(this.apiUrl);
  }

  createOrUpdate(account: EmailAccount): Observable<void> {
    return this.http.post<void>(this.apiUrl, account);
  }

  delete(): Observable<void> {
    return this.http.delete<void>(this.apiUrl);
  }

  testConnection(account: EmailAccount): Observable<{ success: boolean }> {
    return this.http.post<{ success: boolean }>(`${this.apiUrl}/test`, account);
  }
}
