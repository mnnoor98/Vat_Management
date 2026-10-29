import { Injectable, signal, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { delay, tap } from 'rxjs/operators';
import { EnterpriseRole, UserProfile } from '../models/vat-models';
import { CompanyContextService } from './company-context.service';
import { ToastService } from './toast.service';

export interface UserAccount extends UserProfile {
  password?: string;
  companyId: string;
  companyName: string;
  branchId: string;
  branchName: string;
  phone?: string;
  isActive: boolean;
  createdAt: string;
  lastLoginAt?: string;
  assignedModuleCodes?: string[];
  roles?: EnterpriseRole[];
}

export interface RegisterUserRequest {
  username: string;
  fullName: string;
  fullNameBn: string;
  email: string;
  phone?: string;
  password: string;
  role?: EnterpriseRole;
  roles?: EnterpriseRole[];
  companyId: string;
  branchId: string;
  assignedModuleCodes?: string[];
}

const STORAGE_KEY_USERS = 'vat_registered_users';
const STORAGE_KEY_TOKEN = 'vat_auth_token';
const STORAGE_KEY_REFRESH_TOKEN = 'vat_refresh_token';
const STORAGE_KEY_CURRENT_USER = 'vat_auth_user';

// Pre-signed dev tokens with standard permissions
const DEV_USER_TOKENS: Record<string, string> = {
  'USR-8801': 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJVU1ItODgwMSIsInRlbmFudF9pZCI6IjAwMDAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMSIsImNvbXBhbnlfaWQiOlsiMTExMTExMTEtMTExMS0xMTExLTExMTEtMTExMTExMTExMTExIiwiMjIyMjIyMjItMjIyMi0yMjIyLTIyMjItMjIyMjIyMjIyMjIyIl0sInBlcm1pc3Npb24iOlsic2FsZXMuY3JlZGl0LnJlcXVlc3QiLCJzYWxlcy5jcmVkaXQuYXBwcm92ZSIsInNhbGVzLnJlc2VydmUiLCJzYWxlcy5wb3N0Iiwic2FsZXMucmVhZCIsInNhbGVzLmNyZWF0ZSIsInNhbGVzLnN1Ym1pdCIsInNhbGVzLmFwcHJvdmUiLCJzYWxlcy5jYW5jZWwiLCJjb21wYW55LnJlYWQiLCJjb21wYW55Lm1hbmFnZSIsInB1cmNoYXNlLnJlYWQiLCJwdXJjaGFzZS5jcmVhdGUiLCJwdXJjaGFzZS5zdWJtaXQiLCJwdXJjaGFzZS5hcHByb3ZlIiwibWFzdGVyZGF0YS5tYW5hZ2UiLCJtYXN0ZXJkYXRhLnJlYWQiLCJpbnZlbnRvcnkubWFuYWdlIiwiaW52ZW50b3J5LnJlYWQiLCJ0YXgucnVsZS5tYW5hZ2UiLCJ0YXgucnVsZS5wdWJsaXNoIiwidGF4LnJlYWQiLCJwdXJjaGFzZS5wcmVwYXJlIiwicHVyY2hhc2UucG9zdCIsInBlcmlvZC5yZWFkIiwicGVyaW9kLm1hbmFnZSIsInBlcmlvZC5jbG9zZS5yZXF1ZXN0IiwicGVyaW9kLmNsb3NlLmFwcHJvdmUiLCJwdXJjaGFzZS5yZXZlcnNlLnJlcXVlc3QiLCJwdXJjaGFzZS5yZXZlcnNlLmFwcHJvdmUiLCJwdXJjaGFzZS5jYW5jZWwiLCJyZXBvcnQucmVhZCIsInJlcG9ydC5tYW5hZ2UiLCJtZXNzYWdpbmcucmVhZCIsIm1lc3NhZ2luZy5yZXRyeSJdLCJleHAiOjE3OTUzMzQ0OTAsImlzcyI6Imh0dHBzOi8vbG9jYWxob3N0OjUwODAiLCJhdWQiOiJ2YXQtYXBpIn0.Y-2KonC2GXTrfusDxwOtVITltPhByENOpUZu78j17K4',
  'USR-8802': 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJVU1ItODgwMiIsInRlbmFudF9pZCI6IjAwMDAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMSIsImNvbXBhbnlfaWQiOlsiMTExMTExMTEtMTExMS0xMTExLTExMTEtMTExMTExMTExMTExIiwiMjIyMjIyMjItMjIyMi0yMjIyLTIyMjItMjIyMjIyMjIyMjIyIl0sInBlcm1pc3Npb24iOlsic2FsZXMuY3JlZGl0LnJlcXVlc3QiLCJzYWxlcy5jcmVkaXQuYXBwcm92ZSIsInNhbGVzLnJlc2VydmUiLCJzYWxlcy5wb3N0Iiwic2FsZXMucmVhZCIsInNhbGVzLmNyZWF0ZSIsInNhbGVzLnN1Ym1pdCIsInNhbGVzLmFwcHJvdmUiLCJzYWxlcy5jYW5jZWwiLCJjb21wYW55LnJlYWQiLCJjb21wYW55Lm1hbmFnZSIsInB1cmNoYXNlLnJlYWQiLCJwdXJjaGFzZS5jcmVhdGUiLCJwdXJjaGFzZS5zdWJtaXQiLCJwdXJjaGFzZS5hcHByb3ZlIiwibWFzdGVyZGF0YS5tYW5hZ2UiLCJtYXN0ZXJkYXRhLnJlYWQiLCJpbnZlbnRvcnkubWFuYWdlIiwiaW52ZW50b3J5LnJlYWQiLCJ0YXgucnVsZS5tYW5hZ2UiLCJ0YXgucnVsZS5wdWJsaXNoIiwidGF4LnJlYWQiLCJwdXJjaGFzZS5wcmVwYXJlIiwicHVyY2hhc2UucG9zdCIsInBlcmlvZC5yZWFkIiwicGVyaW9kLm1hbmFnZSIsInBlcmlvZC5jbG9zZS5yZXF1ZXN0IiwicGVyaW9kLmNsb3NlLmFwcHJvdmUiLCJwdXJjaGFzZS5yZXZlcnNlLnJlcXVlc3QiLCJwdXJjaGFzZS5yZXZlcnNlLmFwcHJvdmUiLCJwdXJjaGFzZS5jYW5jZWwiLCJyZXBvcnQucmVhZCIsInJlcG9ydC5tYW5hZ2UiLCJtZXNzYWdpbmcucmVhZCIsIm1lc3NhZ2luZy5yZXRyeSJdLCJleHAiOjE3OTUzMzQ0OTEsImlzcyI6Imh0dHBzOi8vbG9jYWxob3N0OjUwODAiLCJhdWQiOiJ2YXQtYXBpIn0.DCofnabe3spbT8lvCO8XPfp55W0RHiBbyee9nhm3aKw',
  'USR-8803': 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJVU1ItODgwMyIsInRlbmFudF9pZCI6IjAwMDAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMSIsImNvbXBhbnlfaWQiOlsiMTExMTExMTEtMTExMS0xMTExLTExMTEtMTExMTExMTExMTExIiwiMjIyMjIyMjItMjIyMi0yMjIyLTIyMjItMjIyMjIyMjIyMjIyIl0sInBlcm1pc3Npb24iOlsic2FsZXMuY3JlZGl0LnJlcXVlc3QiLCJzYWxlcy5jcmVkaXQuYXBwcm92ZSIsInNhbGVzLnJlc2VydmUiLCJzYWxlcy5wb3N0Iiwic2FsZXMucmVhZCIsInNhbGVzLmNyZWF0ZSIsInNhbGVzLnN1Ym1pdCIsInNhbGVzLmFwcHJvdmUiLCJzYWxlcy5jYW5jZWwiLCJjb21wYW55LnJlYWQiLCJjb21wYW55Lm1hbmFnZSIsInB1cmNoYXNlLnJlYWQiLCJwdXJjaGFzZS5jcmVhdGUiLCJwdXJjaGFzZS5zdWJtaXQiLCJwdXJjaGFzZS5hcHByb3ZlIiwibWFzdGVyZGF0YS5tYW5hZ2UiLCJtYXN0ZXJkYXRhLnJlYWQiLCJpbnZlbnRvcnkubWFuYWdlIiwiaW52ZW50b3J5LnJlYWQiLCJ0YXgucnVsZS5tYW5hZ2UiLCJ0YXgucnVsZS5wdWJsaXNoIiwidGF4LnJlYWQiLCJwdXJjaGFzZS5wcmVwYXJlIiwicHVyY2hhc2UucG9zdCIsInBlcmlvZC5yZWFkIiwicGVyaW9kLm1hbmFnZSIsInBlcmlvZC5jbG9zZS5yZXF1ZXN0IiwicGVyaW9kLmNsb3NlLmFwcHJvdmUiLCJwdXJjaGFzZS5yZXZlcnNlLnJlcXVlc3QiLCJwdXJjaGFzZS5yZXZlcnNlLmFwcHJvdmUiLCJwdXJjaGFzZS5jYW5jZWwiLCJyZXBvcnQucmVhZCIsInJlcG9ydC5tYW5hZ2UiLCJtZXNzYWdpbmcucmVhZCIsIm1lc3NhZ2luZy5yZXRyeSJdLCJleHAiOjE3OTUzMzQ0OTEsImlzcyI6Imh0dHBzOi8vbG9jYWxob3N0OjUwODAiLCJhdWQiOiJ2YXQtYXBpIn0.kCKiBk0DI6D-iDOGG-x1HVDm2Mse-ZM3PAIfUNXRlTo',
  'USR-8804': 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJVU1ItODgwNCIsInRlbmFudF9pZCI6IjAwMDAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMSIsImNvbXBhbnlfaWQiOlsiMTExMTExMTEtMTExMS0xMTExLTExMTEtMTExMTExMTExMTExIiwiMjIyMjIyMjItMjIyMi0yMjIyLTIyMjItMjIyMjIyMjIyMjIyIl0sInBlcm1pc3Npb24iOlsic2FsZXMuY3JlZGl0LnJlcXVlc3QiLCJzYWxlcy5jcmVkaXQuYXBwcm92ZSIsInNhbGVzLnJlc2VydmUiLCJzYWxlcy5wb3N0Iiwic2FsZXMucmVhZCIsInNhbGVzLmNyZWF0ZSIsInNhbGVzLnN1Ym1pdCIsInNhbGVzLmFwcHJvdmUiLCJzYWxlcy5jYW5jZWwiLCJjb21wYW55LnJlYWQiLCJjb21wYW55Lm1hbmFnZSIsInB1cmNoYXNlLnJlYWQiLCJwdXJjaGFzZS5jcmVhdGUiLCJwdXJjaGFzZS5zdWJtaXQiLCJwdXJjaGFzZS5hcHByb3ZlIiwibWFzdGVyZGF0YS5tYW5hZ2UiLCJtYXN0ZXJkYXRhLnJlYWQiLCJpbnZlbnRvcnkubWFuYWdlIiwiaW52ZW50b3J5LnJlYWQiLCJ0YXgucnVsZS5tYW5hZ2UiLCJ0YXgucnVsZS5wdWJsaXNoIiwidGF4LnJlYWQiLCJwdXJjaGFzZS5wcmVwYXJlIiwicHVyY2hhc2UucG9zdCIsInBlcmlvZC5yZWFkIiwicGVyaW9kLm1hbmFnZSIsInBlcmlvZC5jbG9zZS5yZXF1ZXN0IiwicGVyaW9kLmNsb3NlLmFwcHJvdmUiLCJwdXJjaGFzZS5yZXZlcnNlLnJlcXVlc3QiLCJwdXJjaGFzZS5yZXZlcnNlLmFwcHJvdmUiLCJwdXJjaGFzZS5jYW5jZWwiLCJyZXBvcnQucmVhZCIsInJlcG9ydC5tYW5hZ2UiLCJtZXNzYWdpbmcucmVhZCIsIm1lc3NhZ2luZy5yZXRyeSJdLCJleHAiOjE3OTUzMzQ0OTEsImlzcyI6Imh0dHBzOi8vbG9jYWxob3N0OjUwODAiLCJhdWQiOiJ2YXQtYXBpIn0.cgy3c7fMtKVfEhTm533dR36zZYq1LlIfLP0Y_2B9qkQ',
  'USR-8805': 'eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiJVU1ItODgwNSIsInRlbmFudF9pZCI6IjAwMDAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMSIsImNvbXBhbnlfaWQiOlsiMTExMTExMTEtMTExMS0xMTExLTExMTEtMTExMTExMTExMTExIiwiMjIyMjIyMjItMjIyMi0yMjIyLTIyMjItMjIyMjIyMjIyMjIyIl0sInBlcm1pc3Npb24iOlsic2FsZXMuY3JlZGl0LnJlcXVlc3QiLCJzYWxlcy5jcmVkaXQuYXBwcm92ZSIsInNhbGVzLnJlc2VydmUiLCJzYWxlcy5wb3N0Iiwic2FsZXMucmVhZCIsInNhbGVzLmNyZWF0ZSIsInNhbGVzLnN1Ym1pdCIsInNhbGVzLmFwcHJvdmUiLCJzYWxlcy5jYW5jZWwiLCJjb21wYW55LnJlYWQiLCJjb21wYW55Lm1hbmFnZSIsInB1cmNoYXNlLnJlYWQiLCJwdXJjaGFzZS5jcmVhdGUiLCJwdXJjaGFzZS5zdWJtaXQiLCJwdXJjaGFzZS5hcHByb3ZlIiwibWFzdGVyZGF0YS5tYW5hZ2UiLCJtYXN0ZXJkYXRhLnJlYWQiLCJpbnZlbnRvcnkubWFuYWdlIiwiaW52ZW50b3J5LnJlYWQiLCJ0YXgucnVsZS5tYW5hZ2UiLCJ0YXgucnVsZS5wdWJsaXNoIiwidGF4LnJlYWQiLCJwdXJjaGFzZS5wcmVwYXJlIiwicHVyY2hhc2UucG9zdCIsInBlcmlvZC5yZWFkIiwicGVyaW9kLm1hbmFnZSIsInBlcmlvZC5jbG9zZS5yZXF1ZXN0IiwicGVyaW9kLmNsb3NlLmFwcHJvdmUiLCJwdXJjaGFzZS5yZXZlcnNlLnJlcXVlc3QiLCJwdXJjaGFzZS5yZXZlcnNlLmFwcHJvdmUiLCJwdXJjaGFzZS5jYW5jZWwiLCJyZXBvcnQucmVhZCIsInJlcG9ydC5tYW5hZ2UiLCJtZXNzYWdpbmcucmVhZCIsIm1lc3NhZ2luZy5yZXRyeSJdLCJleHAiOjE3OTUzMzQ0OTEsImlzcyI6Imh0dHBzOi8vbG9jYWxob3N0OjUwODAiLCJhdWQiOiJ2YXQtYXBpIn0.9lEPxPyp9IfPLPSCG2EW2RxJvXuiGx1YcbT7muuJ0F8'
};

const DEFAULT_USERS: UserAccount[] = [
  {
    id: 'USR-8805',
    username: 'admin',
    password: 'admin123',
    fullName: 'System Administrator',
    fullNameBn: 'সিস্টেম অ্যাডমিনিস্ট্রেটর',
    email: 'admin@vat.gov.bd',
    role: 'SuperAdmin',
    roles: ['SuperAdmin'],
    companyId: '11111111-1111-1111-1111-111111111111',
    companyName: 'Dhaka Polymer Industries Ltd',
    branchId: '33333333-3333-3333-3333-333333333331',
    branchName: 'Dhaka Central Plant',
    phone: '+880 1711-000888',
    isActive: true,
    createdAt: '2026-01-01',
    accessibleCompanyIds: ['11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222'],
    permissions: ['all'],
    assignedModuleCodes: [
      'MOD_PURCHASES', 'MOD_SALES', 'MOD_MANUFACTURING', 'MOD_INVENTORY',
      'MOD_COMPLIANCE', 'MOD_WORKFLOW', 'MOD_MASTERDATA', 'MOD_REPORTS', 'MOD_ADMIN'
    ]
  },
  {
    id: 'USR-8801',
    username: 'maker',
    password: 'maker123',
    fullName: 'Mohammad Rafiqul Islam',
    fullNameBn: 'মোহাম্মদ রফিকুল ইসলাম',
    email: 'rafiq.maker@padmagroup.com.bd',
    role: 'Maker',
    roles: ['Maker'],
    companyId: '11111111-1111-1111-1111-111111111111',
    companyName: 'Dhaka Polymer Industries Ltd',
    branchId: '33333333-3333-3333-3333-333333333331',
    branchName: 'Dhaka Central Plant',
    phone: '+880 1819-223344',
    isActive: true,
    createdAt: '2026-02-15',
    accessibleCompanyIds: ['11111111-1111-1111-1111-111111111111'],
    permissions: ['purchase.create', 'purchase.read', 'purchase.submit', 'sales.create', 'sales.read', 'sales.submit'],
    assignedModuleCodes: [
      'MOD_PURCHASES', 'MOD_SALES', 'MOD_MANUFACTURING', 'MOD_INVENTORY', 'MOD_MASTERDATA'
    ]
  },
  {
    id: 'USR-8802',
    username: 'checker',
    password: 'checker123',
    fullName: 'Farhana Haque, ACA',
    fullNameBn: 'ফারহানা হক, এসিএ',
    email: 'farhana.checker@padmagroup.com.bd',
    role: 'Checker',
    roles: ['Checker'],
    companyId: '11111111-1111-1111-1111-111111111111',
    companyName: 'Dhaka Polymer Industries Ltd',
    branchId: '33333333-3333-3333-3333-333333333331',
    branchName: 'Dhaka Central Plant',
    phone: '+880 1912-334455',
    isActive: true,
    createdAt: '2026-02-18',
    accessibleCompanyIds: ['11111111-1111-1111-1111-111111111111'],
    permissions: ['purchase.read', 'purchase.approve', 'sales.read', 'sales.approve', 'workflow.read'],
    assignedModuleCodes: [
      'MOD_PURCHASES', 'MOD_SALES', 'MOD_INVENTORY', 'MOD_WORKFLOW', 'MOD_MASTERDATA', 'MOD_REPORTS'
    ]
  },
  {
    id: 'USR-8803',
    username: 'taxmanager',
    password: 'tax123',
    fullName: 'Md. Tanveer Ahmed, FCA',
    fullNameBn: 'মো. তানভীর আহমেদ, এফসিএ',
    email: 'tanveer.fca@padmagroup.com.bd',
    role: 'TaxManager',
    roles: ['TaxManager', 'Approver'],
    companyId: '11111111-1111-1111-1111-111111111111',
    companyName: 'Dhaka Polymer Industries Ltd',
    branchId: '33333333-3333-3333-3333-333333333331',
    branchName: 'Dhaka Central Plant',
    phone: '+880 1713-998877',
    isActive: true,
    createdAt: '2026-01-10',
    accessibleCompanyIds: ['11111111-1111-1111-1111-111111111111', '22222222-2222-2222-2222-222222222222'],
    permissions: ['purchase.post', 'sales.post', 'tax.read', 'report.read', 'compliance.submit'],
    assignedModuleCodes: [
      'MOD_PURCHASES', 'MOD_SALES', 'MOD_MANUFACTURING', 'MOD_INVENTORY',
      'MOD_COMPLIANCE', 'MOD_WORKFLOW', 'MOD_MASTERDATA', 'MOD_REPORTS'
    ]
  },
  {
    id: 'USR-8804',
    username: 'auditor',
    password: 'audit123',
    fullName: 'Nasrin Sultana, CISA',
    fullNameBn: 'নাসরিন সুলতানা, সিসা',
    email: 'nasrin.audit@padmagroup.com.bd',
    role: 'Auditor',
    roles: ['Auditor'],
    companyId: '11111111-1111-1111-1111-111111111111',
    companyName: 'Dhaka Polymer Industries Ltd',
    branchId: '33333333-3333-3333-3333-333333333331',
    branchName: 'Dhaka Central Plant',
    phone: '+880 1611-445566',
    isActive: true,
    createdAt: '2026-03-01',
    accessibleCompanyIds: ['11111111-1111-1111-1111-111111111111'],
    permissions: ['report.read', 'tax.read', 'audit.read'],
    assignedModuleCodes: [
      'MOD_PURCHASES', 'MOD_SALES', 'MOD_INVENTORY', 'MOD_COMPLIANCE', 'MOD_REPORTS'
    ]
  },
  {
    id: 'USR-8806',
    username: 'ctg_maker',
    password: 'maker123',
    fullName: 'Kamrul Hasan (CTG Depot)',
    fullNameBn: 'কামরুল হাসান (চট্টগ্রাম ডিপো)',
    email: 'kamrul.ctg@padmagroup.com.bd',
    role: 'Maker',
    roles: ['Maker'],
    companyId: '11111111-1111-1111-1111-111111111111',
    companyName: 'Dhaka Polymer Industries Ltd',
    branchId: '33333333-3333-3333-3333-333333333332',
    branchName: 'Chittagong Port Depot',
    phone: '+880 1812-778899',
    isActive: true,
    createdAt: '2026-03-05',
    accessibleCompanyIds: ['11111111-1111-1111-1111-111111111111'],
    permissions: ['purchase.create', 'purchase.read', 'purchase.submit', 'sales.create', 'sales.read', 'sales.submit'],
    assignedModuleCodes: [
      'MOD_PURCHASES', 'MOD_SALES', 'MOD_INVENTORY'
    ]
  }
];

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private router = inject(Router);
  private contextService = inject(CompanyContextService);
  private toastService = inject(ToastService);

  public readonly currentUser = signal<UserAccount | null>(null);
  public readonly isAuthenticated = signal<boolean>(false);
  public readonly token = signal<string | null>(null);

  constructor() {
    this.initAuth();
  }

  private initAuth(): void {
    this.ensureDefaultUsers();

    const storedToken = localStorage.getItem(STORAGE_KEY_TOKEN);
    const storedUserJson = localStorage.getItem(STORAGE_KEY_CURRENT_USER);

    if (storedToken && storedUserJson) {
      try {
        const user: UserAccount = JSON.parse(storedUserJson);
        this.token.set(storedToken);
        if (!localStorage.getItem(STORAGE_KEY_REFRESH_TOKEN)) {
          localStorage.setItem(STORAGE_KEY_REFRESH_TOKEN, `REFRESH-${user.id}-${Date.now()}`);
        }
        this.currentUser.set(user);
        this.isAuthenticated.set(true);
        this.syncContext(user);
      } catch (e) {
        this.clearSession();
      }
    }
  }

  private ensureDefaultUsers(): void {
    const existing = localStorage.getItem(STORAGE_KEY_USERS);
    if (!existing) {
      localStorage.setItem(STORAGE_KEY_USERS, JSON.stringify(DEFAULT_USERS));
    } else {
      try {
        const users: UserAccount[] = JSON.parse(existing);
        let changed = false;
        for (const def of DEFAULT_USERS) {
          const found = users.find(u => u.username === def.username);
          if (!found) {
            users.push(def);
            changed = true;
          } else {
            if (!found.assignedModuleCodes || found.assignedModuleCodes.length === 0) {
              found.assignedModuleCodes = def.assignedModuleCodes;
              changed = true;
            }
            if (!found.roles || found.roles.length === 0) {
              found.roles = def.roles || [found.role || 'Maker'];
              changed = true;
            }
          }
        }
        for (const u of users) {
          if (!u.roles || u.roles.length === 0) {
            u.roles = [u.role || 'Maker'];
            changed = true;
          }
          if (!u.assignedModuleCodes || u.assignedModuleCodes.length === 0) {
            u.assignedModuleCodes = this.getDefaultModulesForRoles(u.roles);
            changed = true;
          }
        }
        if (changed) {
          localStorage.setItem(STORAGE_KEY_USERS, JSON.stringify(users));
        }
      } catch {
        localStorage.setItem(STORAGE_KEY_USERS, JSON.stringify(DEFAULT_USERS));
      }
    }
  }

  public getUsers(): UserAccount[] {
    this.ensureDefaultUsers();
    try {
      const data = localStorage.getItem(STORAGE_KEY_USERS);
      return data ? JSON.parse(data) : [...DEFAULT_USERS];
    } catch {
      return [...DEFAULT_USERS];
    }
  }

  private saveUsers(users: UserAccount[]): void {
    localStorage.setItem(STORAGE_KEY_USERS, JSON.stringify(users));
  }

  public login(credentials: { username: string; password: string }, rememberMe: boolean = true): Observable<UserAccount> {
    const users = this.getUsers();
    const cleanUser = credentials.username?.trim().toLowerCase();
    const cleanPass = credentials.password?.trim();

    const matched = users.find(u => 
      (u.username.toLowerCase() === cleanUser || u.email.toLowerCase() === cleanUser) &&
      u.password === cleanPass
    );

    if (!matched) {
      return throwError(() => new Error('ভুল ইউজারনেম বা পাসওয়ার্ড! অনুগ্রহ করে সঠিক তথ্য প্রদান করুন। (Invalid username or password)'));
    }

    if (!matched.isActive) {
      return throwError(() => new Error('এই ব্যবহারকারীর অ্যাকাউন্ট নিষ্ক্রিয় (Inactive)। অনুগ্রহ করে প্রশাসকের সাথে যোগাযোগ করুন।'));
    }

    // Pick dev JWT token or fallback
    const userToken = DEV_USER_TOKENS[matched.id] || DEV_USER_TOKENS['USR-8801'];
    const refreshToken = `REFRESH-${matched.id}-${Date.now()}`;

    // Update last login
    matched.lastLoginAt = new Date().toISOString().replace('T', ' ').substring(0, 16);
    this.saveUsers(users.map(u => u.id === matched.id ? matched : u));

    // Save session
    localStorage.setItem(STORAGE_KEY_TOKEN, userToken);
    localStorage.setItem(STORAGE_KEY_REFRESH_TOKEN, refreshToken);
    localStorage.setItem(STORAGE_KEY_CURRENT_USER, JSON.stringify(matched));

    this.token.set(userToken);
    this.currentUser.set(matched);
    this.isAuthenticated.set(true);
    this.syncContext(matched);

    return of(matched).pipe(delay(200));
  }

  public register(req: RegisterUserRequest): Observable<UserAccount> {
    const users = this.getUsers();
    const cleanUsername = req.username.trim().toLowerCase();
    const cleanEmail = req.email.trim().toLowerCase();

    // Validations
    if (users.some(u => u.username.toLowerCase() === cleanUsername)) {
      return throwError(() => new Error(`'${req.username}' ইউজারনেমটি ইতিমধ্যে ব্যবহৃত হয়েছে। নতুন ইউজারনেম বেছে নিন।`));
    }
    if (users.some(u => u.email.toLowerCase() === cleanEmail)) {
      return throwError(() => new Error(`'${req.email}' ইমেইলটি ইতিমধ্যে নিবন্ধিত আছে।`));
    }
    if (!req.password || req.password.length < 6) {
      return throwError(() => new Error('পাসওয়ার্ড কমপক্ষে ৬ অক্ষরের হতে হবে।'));
    }

    const userRoles: EnterpriseRole[] = (req.roles && req.roles.length > 0) 
      ? req.roles 
      : (req.role ? [req.role] : ['Maker']);
    const primaryRole = userRoles[0];

    const newId = `USR-${Math.floor(1000 + Math.random() * 9000)}`;
    const newUser: UserAccount = {
      id: newId,
      username: req.username.trim(),
      password: req.password,
      fullName: req.fullName.trim(),
      fullNameBn: req.fullNameBn ? req.fullNameBn.trim() : req.fullName.trim(),
      email: req.email.trim(),
      phone: req.phone?.trim() || '+880 1700-000000',
      role: primaryRole,
      roles: userRoles,
      companyId: req.companyId,
      companyName: req.companyId === '22222222-2222-2222-2222-222222222222' ? 'Meghna Plastic Industries Ltd' : 'Dhaka Polymer Industries Ltd',
      branchId: req.branchId,
      branchName: req.branchId.includes('33') ? 'Bogura Regional Warehouse' : (req.branchId.includes('32') ? 'Chittagong Port Depot' : 'Dhaka Central Plant & HQ'),
      isActive: true,
      createdAt: new Date().toISOString().split('T')[0],
      accessibleCompanyIds: [req.companyId],
      permissions: this.getPermissionsForRoles(userRoles),
      assignedModuleCodes: req.assignedModuleCodes && req.assignedModuleCodes.length > 0 
        ? req.assignedModuleCodes 
        : this.getDefaultModulesForRoles(userRoles)
    };

    users.push(newUser);
    this.saveUsers(users);

    return of(newUser).pipe(delay(200));
  }

  public updateUser(userId: string, updates: Partial<UserAccount>): boolean {
    const users = this.getUsers();
    const idx = users.findIndex(u => u.id === userId);
    if (idx === -1) return false;

    if (updates.roles && updates.roles.length > 0) {
      updates.role = updates.roles[0];
      updates.permissions = this.getPermissionsForRoles(updates.roles);
    }

    users[idx] = { ...users[idx], ...updates };
    this.saveUsers(users);

    // If updating current logged in user, refresh context immediately
    if (this.currentUser()?.id === userId) {
      this.currentUser.set(users[idx]);
      localStorage.setItem(STORAGE_KEY_CURRENT_USER, JSON.stringify(users[idx]));
      this.syncContext(users[idx]);
    }
    return true;
  }

  public logout(): void {
    this.clearSession();
    this.router.navigate(['/login']);
    this.toastService.showInfo('সফলভাবে লগআউট হয়েছে (Logged out successfully).');
  }

  public toggleUserStatus(userId: string): boolean {
    const users = this.getUsers();
    const user = users.find(u => u.id === userId);
    if (!user) return false;

    // Prevent deactivating own account
    if (this.currentUser()?.id === userId) {
      this.toastService.showWarning('আপনি নিজের সক্রিয় অ্যাকাউন্ট নিষ্ক্রিয় করতে পারবেন না!');
      return false;
    }

    user.isActive = !user.isActive;
    this.saveUsers(users);
    return true;
  }

  public deleteUser(userId: string): boolean {
    const users = this.getUsers();
    if (this.currentUser()?.id === userId) {
      this.toastService.showError('লগইন থাকা অবস্থায় নিজের অ্যাকাউন্ট ডিলিট করা সম্ভব নয়!');
      return false;
    }

    const filtered = users.filter(u => u.id !== userId);
    this.saveUsers(filtered);
    return true;
  }

  public resetUserPassword(userId: string, newPass: string = 'vat123456'): boolean {
    const users = this.getUsers();
    const user = users.find(u => u.id === userId);
    if (!user) return false;

    user.password = newPass;
    this.saveUsers(users);
    return true;
  }

  public refreshToken(): Observable<string | null> {
    const refreshToken = localStorage.getItem(STORAGE_KEY_REFRESH_TOKEN);
    const currentUser = this.currentUser();

    if (!currentUser || !refreshToken) {
      return of(null);
    }

    // Refresh valid session token
    const userToken = DEV_USER_TOKENS[currentUser.id] || DEV_USER_TOKENS['USR-8801'];
    localStorage.setItem(STORAGE_KEY_TOKEN, userToken);
    this.token.set(userToken);

    return of(userToken).pipe(delay(100));
  }

  public handleSessionExpired(returnUrl?: string): void {
    this.clearSession();
    this.toastService.warning(
      'আপনার লগইন অধিবেশনের মেয়াদ উত্তীর্ণ হয়েছে। অনুগ্রহ করে পুনরায় লগইন করুন।',
      'অধিবেশন সমাপ্ত (Session Expired)'
    );
    const targetUrl = returnUrl || this.router.url;
    if (!targetUrl.includes('/login')) {
      this.router.navigate(['/login'], {
        queryParams: { returnUrl: targetUrl }
      });
    }
  }

  private clearSession(): void {
    localStorage.removeItem(STORAGE_KEY_TOKEN);
    localStorage.removeItem(STORAGE_KEY_REFRESH_TOKEN);
    localStorage.removeItem(STORAGE_KEY_CURRENT_USER);
    this.token.set(null);
    this.currentUser.set(null);
    this.isAuthenticated.set(false);
  }

  private syncContext(user: UserAccount): void {
    const userRoles: EnterpriseRole[] = (user.roles && user.roles.length > 0) 
      ? user.roles 
      : (user.role ? [user.role] : ['Maker']);

    // Sync current user with CompanyContextService
    const profile: UserProfile = {
      id: user.id,
      username: user.username,
      fullName: user.fullName,
      fullNameBn: user.fullNameBn,
      email: user.email,
      role: userRoles[0],
      roles: userRoles,
      accessibleCompanyIds: user.accessibleCompanyIds,
      permissions: user.permissions && user.permissions.length > 0 ? user.permissions : this.getPermissionsForRoles(userRoles),
      branchId: user.branchId,
      branchName: user.branchName,
      assignedModuleCodes: user.assignedModuleCodes
    };
    this.contextService.currentUser.set(profile);

    // Switch company/branch to user's assigned entity
    if (user.companyId) {
      this.contextService.switchCompany(user.companyId);
    }
    if (user.branchId) {
      this.contextService.switchBranch(user.branchId);
    }
  }

  public getDefaultModulesForRole(role: string): string[] {
    switch (role) {
      case 'Maker':
        return ['MOD_PURCHASES', 'MOD_SALES', 'MOD_MANUFACTURING', 'MOD_INVENTORY', 'MOD_MASTERDATA'];
      case 'Checker':
        return ['MOD_PURCHASES', 'MOD_SALES', 'MOD_INVENTORY', 'MOD_WORKFLOW', 'MOD_MASTERDATA', 'MOD_REPORTS'];
      case 'Approver':
        return ['MOD_PURCHASES', 'MOD_SALES', 'MOD_MANUFACTURING', 'MOD_COMPLIANCE', 'MOD_WORKFLOW', 'MOD_REPORTS'];
      case 'TaxManager':
        return ['MOD_PURCHASES', 'MOD_SALES', 'MOD_MANUFACTURING', 'MOD_INVENTORY', 'MOD_COMPLIANCE', 'MOD_WORKFLOW', 'MOD_MASTERDATA', 'MOD_REPORTS'];
      case 'Auditor':
        return ['MOD_PURCHASES', 'MOD_SALES', 'MOD_INVENTORY', 'MOD_COMPLIANCE', 'MOD_REPORTS'];
      case 'SuperAdmin':
      default:
        return [
          'MOD_PURCHASES', 'MOD_SALES', 'MOD_MANUFACTURING', 'MOD_INVENTORY',
          'MOD_COMPLIANCE', 'MOD_WORKFLOW', 'MOD_MASTERDATA', 'MOD_REPORTS', 'MOD_ADMIN'
        ];
    }
  }

  public getDefaultModulesForRoles(roles: string[]): string[] {
    const modSet = new Set<string>();
    for (const r of roles) {
      const list = this.getDefaultModulesForRole(r);
      list.forEach(m => modSet.add(m));
    }
    return Array.from(modSet);
  }

  public getPermissionsForRole(role: string): string[] {
    switch (role) {
      case 'Maker':
        return ['purchase.create', 'purchase.read', 'purchase.submit', 'sales.create', 'sales.read', 'sales.submit'];
      case 'Checker':
        return ['purchase.read', 'purchase.approve', 'sales.read', 'sales.approve', 'workflow.read'];
      case 'TaxManager':
      case 'Approver':
        return ['purchase.post', 'sales.post', 'tax.read', 'report.read', 'compliance.submit', 'all'];
      case 'Auditor':
        return ['report.read', 'tax.read', 'audit.read'];
      case 'SuperAdmin':
      default:
        return ['all'];
    }
  }

  public getPermissionsForRoles(roles: string[]): string[] {
    const permSet = new Set<string>();
    for (const r of roles) {
      const perms = this.getPermissionsForRole(r);
      perms.forEach(p => permSet.add(p));
    }
    return Array.from(permSet);
  }
}
