import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Email {
  id: string;
  applicationId?: number;
  subject: string;
  from: string;
  to: string;
  body: string;
  receivedDate: string;
  isRead: boolean;
  threadId?: string;
}

@Injectable({ providedIn: 'root' })
export class EmailService {
  private readonly apiUrl = 'api/emails';

  constructor(private http: HttpClient) {}

  getAll(): Observable<Email[]> {
    return this.http.get<Email[]>(this.apiUrl);
  }

  getById(id: string): Observable<Email> {
    return this.http.get<Email>(`${this.apiUrl}/${id}`);
  }

  getByApplication(appId: number): Observable<Email[]> {
    return this.http.get<Email[]>(`${this.apiUrl}/application/${appId}`);
  }

  triggerSync(): Observable<{ processed: number }> {
    return this.http.post<{ processed: number }>(`${this.apiUrl}/sync`, {});
  }

  linkToApplication(emailId: string, applicationId: number): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${emailId}/link`, applicationId);
  }
}
