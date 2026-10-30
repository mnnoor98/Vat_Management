import { throwError } from 'rxjs';
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, delay, catchError, map } from 'rxjs';
import { ApprovalTask } from '../../models/vat-models';
import { MockDataService } from '../mock-data.service';
import { NavigationService } from '../navigation.service';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class WorkflowApiService {
  private http = inject(HttpClient);
  private mockData = inject(MockDataService);
  private navService = inject(NavigationService);
  private context = inject(CompanyContextService);

  private get baseUrl(): string {
    return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}/workflows`;
  }

  public getMyTasks(): Observable<ApprovalTask[]> {
    if (environment.useMockData) {
      return of([...this.mockData.approvalTasks]).pipe(delay(60));
    }
    return this.http.get<ApprovalTask[]>(`${this.baseUrl}/my-tasks`).pipe(
      map(tasks => {
        const pendingCount = tasks.filter(t => t.status === 'Pending').length;
        this.navService.setBadgeCount('MENU_WORKFLOW', pendingCount);
        return tasks;
      }),
      catchError(error => throwError(() => error))
    );
  }

  public approveTask(taskId: string, remarks?: string): Observable<boolean> {
    if (!environment.useMockData) return throwError(() => new Error('Open the source document and submit a versioned approval decision.'));
    const task = this.mockData.approvalTasks.find(t => t.id === taskId);
    if (task) {
      task.status = 'Approved';
      task.notes = remarks || 'Approved by Checker/Approver';
      this.navService.updateBadgeCount('MENU_WORKFLOW', -1);

      if (task.documentType === 'PurchaseInvoice') {
        const inv = this.mockData.purchaseInvoices.find(p => p.id === task.documentId);
        if (inv) inv.status = 'Approved';
      } else if (task.documentType === 'SalesInvoice') {
        const inv = this.mockData.salesInvoices.find(s => s.id === task.documentId);
        if (inv) inv.status = 'Approved';
      }
    }

    if (environment.useMockData) {
      return of(true).pipe(delay(100));
    }
    return this.http.post<boolean>(`${this.baseUrl}/tasks/${taskId}/approve`, { remarks }).pipe(
      map(() => {
        this.navService.updateBadgeCount('MENU_WORKFLOW', -1);
        return true;
      })
    );
  }

  public rejectTask(taskId: string, rejectionReason: string): Observable<boolean> {
    if (!environment.useMockData) return throwError(() => new Error('Open the source document and submit a versioned approval decision.'));
    const task = this.mockData.approvalTasks.find(t => t.id === taskId);
    if (task) {
      task.status = 'Rejected';
      task.notes = rejectionReason;
      this.navService.updateBadgeCount('MENU_WORKFLOW', -1);

      if (task.documentType === 'PurchaseInvoice') {
        const inv = this.mockData.purchaseInvoices.find(p => p.id === task.documentId);
        if (inv) inv.status = 'Rejected';
      } else if (task.documentType === 'SalesInvoice') {
        const inv = this.mockData.salesInvoices.find(s => s.id === task.documentId);
        if (inv) inv.status = 'Rejected';
      }
    }

    if (environment.useMockData) {
      return of(true).pipe(delay(100));
    }
    return this.http.post<boolean>(`${this.baseUrl}/tasks/${taskId}/reject`, { reason: rejectionReason }).pipe(
      map(() => {
        this.navService.updateBadgeCount('MENU_WORKFLOW', -1);
        return true;
      })
    );
  }

  public sendBackTask(taskId: string, correctionNotes: string): Observable<boolean> {
    if (!environment.useMockData) return throwError(() => new Error('Open the source document and submit a versioned approval decision.'));
    const task = this.mockData.approvalTasks.find(t => t.id === taskId);
    if (task) {
      task.status = 'SentBack';
      task.notes = correctionNotes;
      this.navService.updateBadgeCount('MENU_WORKFLOW', -1);
    }

    if (environment.useMockData) {
      return of(true).pipe(delay(100));
    }
    return this.http.post<boolean>(`${this.baseUrl}/tasks/${taskId}/send-back`, { notes: correctionNotes }).pipe(
      map(() => {
        this.navService.updateBadgeCount('MENU_WORKFLOW', -1);
        return true;
      })
    );
  }
}
