import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Application, ApplicationStatus, CreateApplication, DashboardStats, UpdateApplication } from '../models/application.model';

@Injectable({ providedIn: 'root' })
export class ApplicationService {
  private readonly apiUrl = 'api/applications';

  constructor(private http: HttpClient) {}

  getAll(): Observable<Application[]> {
    return this.http.get<Application[]>(this.apiUrl);
  }

  getById(id: number): Observable<Application> {
    return this.http.get<Application>(`${this.apiUrl}/${id}`);
  }

  getByStatus(status: ApplicationStatus): Observable<Application[]> {
    return this.http.get<Application[]>(`${this.apiUrl}/status/${status}`);
  }

  create(dto: CreateApplication): Observable<Application> {
    return this.http.post<Application>(this.apiUrl, dto);
  }

  update(id: number, dto: UpdateApplication): Observable<Application> {
    return this.http.put<Application>(`${this.apiUrl}/${id}`, dto);
  }

  updateStatus(id: number, status: ApplicationStatus): Observable<void> {
    return this.http.patch<void>(`${this.apiUrl}/${id}/status`, JSON.stringify(status), {
      headers: { 'Content-Type': 'application/json' },
    });
  }

  addNote(id: number, note: string): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/${id}/notes`, JSON.stringify(note), {
      headers: { 'Content-Type': 'application/json' },
    });
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
