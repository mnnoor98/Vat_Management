import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CompanyContextService } from '../company-context.service';
import { environment } from '../../../../environments/environment';
export interface TaxRule {
  id: string; taxCode: string; treatment: string; ratePercent: number; recoverablePercent: number;
  effectiveFrom: string; effectiveUntil: string; legalBasis: string; evidenceReference: string;
  createdBy: string; publishedBy: string | null; publishedAt: string | null; version: number;
}
@Injectable({ providedIn: 'root' })
export class TaxRulesApiService {
  private http = inject(HttpClient);
  private context = inject(CompanyContextService);
  private get base() { return `${environment.apiGatewayUrl}/companies/${this.context.companyGuid()}/tax/rules`; }
  list(skip = 0) { return this.http.get<TaxRule[]>(this.base, { params: { skip, take: 25 } }); }
  publish(rule: TaxRule) { return this.http.post<TaxRule>(`${this.base}/${rule.id}/publish`, { expectedVersion: rule.version }); }
}
