import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { UserManager, WebStorageStateStore, User } from 'oidc-client-ts';
import { EnterpriseRole, UserProfile } from '../models/vat-models';
import { CompanyContextService } from './company-context.service';
import { ToastService } from './toast.service';
import { environment } from '../../../environments/environment';

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

interface VerifiedProfile { subject: string; tenantId: string; companyIds: string[]; permissions: string[]; roles?: string[]; username?: string; displayName?: string; assignedModuleCodes?: string[]; branchId?: string; email?: string; }
interface AuthConfiguration { authority: string; clientId: string; scope?: string; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly contextService = inject(CompanyContextService);
  private readonly toastService = inject(ToastService);
  readonly currentUser = signal<UserAccount | null>(null);
  readonly isAuthenticated = signal(false);
  readonly token = signal<string | null>(null);
  private managerPromise?: Promise<UserManager>;
  private expiresAt = 0;
  readonly ready: Promise<void>;

  constructor() {
    this.ready = this.restore();
  }

  private manager(): Promise<UserManager> {
    return this.managerPromise ??= (async () => {
      const response = await fetch('/auth-config.json', { cache: 'no-store', credentials: 'same-origin' });
      if (!response.ok) throw new Error('Sign-in is not configured. Contact your administrator.');
      const config = await response.json() as AuthConfiguration;
      if (!config.authority || !config.clientId || new URL(config.authority).protocol !== 'https:') {
        throw new Error('Configure an HTTPS identity provider and public SPA client ID in auth-config.json.');
      }
      const manager = new UserManager({
        authority: config.authority, client_id: config.clientId,
        redirect_uri: `${location.origin}/auth/callback`,
        post_logout_redirect_uri: `${location.origin}/login`,
        response_type: 'code', scope: config.scope || 'openid profile',
        automaticSilentRenew: false, loadUserInfo: false,
        userStore: new WebStorageStateStore({ store: sessionStorage }),
        stateStore: new WebStorageStateStore({ store: sessionStorage })
      });
      manager.events.addAccessTokenExpired(() => this.handleSessionExpired());
      manager.events.addUserSignedOut(() => this.handleSessionExpired());
      return manager;
    })();
  }

  async startLogin(returnUrl = '/app/portal'): Promise<void> {
    const manager = await this.manager();
    await manager.clearStaleState();
    await manager.signinRedirect({ state: { returnUrl: this.safeReturnUrl(returnUrl) } });
  }

  async completeLogin(): Promise<string> {
    const manager = await this.manager();
    try {
      const user = await manager.signinRedirectCallback();
      await this.acceptVerifiedUser(user);
      const state = user.state as { returnUrl?: string } | undefined;
      return this.safeReturnUrl(state?.returnUrl);
    } catch (error) {
      await manager.removeUser();
      this.clearSession();
      throw error;
    } finally {
      history.replaceState(null, '', '/auth/callback');
    }
  }

  private async restore(): Promise<void> {
    if (location.pathname === '/auth/callback') return;

    const savedToken = sessionStorage.getItem('vat_secure_token');
    const savedExp = Number(sessionStorage.getItem('vat_secure_expires') || '0');
    if (savedToken && Date.now() < savedExp) {
      try {
        await this.acceptVerifiedUser({ access_token: savedToken, expired: false,
          expires_at: savedExp / 1000, profile: {} } as User);
        return;
      } catch { this.clearSession(); }
    }

    try {
      const user = await (await this.manager()).getUser();
      if (user && !user.expired) await this.acceptVerifiedUser(user);
    } catch {
      this.clearSession();
    }
  }

  private async acceptVerifiedUser(user: User): Promise<void> {
    if (!user.access_token || user.expired || !user.expires_at) throw new Error('The sign-in session has expired.');
    const response = await fetch(`${environment.apiGatewayUrl}/identity/me`, {
      headers: { Authorization: `Bearer ${user.access_token}` }, cache: 'no-store'
    });
    if (!response.ok) throw new Error('The API did not accept this identity. Check issuer, audience and application grants.');
    const profile = await response.json() as VerifiedProfile;
    if ((user.profile.sub && profile.subject !== user.profile.sub) || !Array.isArray(profile.permissions) || !Array.isArray(profile.companyIds)) {
      throw new Error('The verified API subject or grants do not match this session.');
    }
    const supportedRoles: EnterpriseRole[] = ['SuperAdmin', 'TaxManager', 'Maker', 'Checker', 'Approver', 'Auditor', 'User'];
    const roles = [...new Set((Array.isArray(profile.roles) ? profile.roles : [])
      .map(role => role === 'Admin' ? 'SuperAdmin' : role)
      .filter((role): role is EnterpriseRole => supportedRoles.includes(role as EnterpriseRole)))];
    if (!roles.length) roles.push('User');
    const account: UserAccount = {
      id: profile.subject, username: profile.username || user.profile.preferred_username || profile.subject,
      fullName: profile.displayName || user.profile.name || profile.subject, fullNameBn: profile.displayName || user.profile.name || profile.subject,
      email: profile.email || user.profile.email || '', role: roles[0], roles,
      companyId: profile.companyIds[0] || '', companyName: '', branchId: profile.branchId || '', branchName: '',
      accessibleCompanyIds: profile.companyIds, permissions: profile.permissions,
      assignedModuleCodes: Array.isArray(profile.assignedModuleCodes) ? profile.assignedModuleCodes : this.modulesForPermissions(profile.permissions), isActive: true, createdAt: ''
    };
    this.currentUser.set(account);
    this.contextService.currentUser.set(account);
    await this.contextService.loadVerifiedCompanies(user.access_token, profile.companyIds);
    if (profile.branchId) this.contextService.switchBranch(profile.branchId);
    this.expiresAt = user.expires_at * 1000;
    this.token.set(user.access_token);
    this.isAuthenticated.set(true);
  }

  accessToken(): string | null {
    if (Date.now() >= this.expiresAt) { this.clearSession(); return null; }
    return this.token();
  }

  hasValidSession(): boolean { return !!this.accessToken() && this.isAuthenticated(); }

  private safeReturnUrl(url?: string): string {
    return url?.startsWith('/app/') && !url.includes('\\') ? url : '/app/portal';
  }

  logout(): void {
    this.clearSession();
    void this.manager().then(async manager => {
      const user = await manager.getUser();
      await manager.removeUser();
      await manager.signoutRedirect({ id_token_hint: user?.id_token });
    }).catch(() => this.router.navigate(['/login']));
  }

  handleSessionExpired(returnUrl?: string): void {
    this.clearSession();
    void this.managerPromise?.then(manager => manager.removeUser()).catch(() => undefined);
    void this.router.navigate(['/login'], { queryParams: { returnUrl: this.safeReturnUrl(returnUrl) } });
  }

  refreshToken(): Observable<string | null> {
    return of(null);
  }

  private clearSession(): void {
    sessionStorage.removeItem('vat_secure_token');
    sessionStorage.removeItem('vat_secure_user');
    sessionStorage.removeItem('vat_secure_expires');
    this.token.set(null);
    this.currentUser.set(null);
    this.isAuthenticated.set(false);
    this.expiresAt = 0;
    this.contextService.clearVerifiedContext();
  }

  private modulesForPermissions(permissions: string[]): string[] {
    const modules: Record<string, string> = {
      purchase: 'MOD_PURCHASES', sales: 'MOD_SALES', inventory: 'MOD_INVENTORY',
      manufacturing: 'MOD_MANUFACTURING', compliance: 'MOD_COMPLIANCE', workflow: 'MOD_WORKFLOW',
      masterdata: 'MOD_MASTERDATA', report: 'MOD_REPORTS', identity: 'MOD_ADMIN'
    };
    return [...new Set(permissions.map(p => modules[p.split('.')[0]]).filter(Boolean))];
  }

  getUsers(): UserAccount[] { return this.currentUser() ? [this.currentUser()!] : []; }

  login(credentials: { username: string; password: string }, _rememberMe = true): Observable<UserAccount> {
    return new Observable<UserAccount>(subscriber => {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), 15000);
      void (async () => {
        try {
          await this.ready;
          this.clearSession();
          const response = await fetch(`${environment.apiGatewayUrl}/auth/login`, {
            method: 'POST', headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(credentials), signal: controller.signal
          });
          if (!response.ok) {
            if (response.status === 401) throw new Error('Invalid username or password.');
            if (response.status === 404) throw new Error('Local login is unavailable. Check the backend development configuration.');
            throw new Error('The login server could not complete sign-in. Please try again.');
          }
          const data = await response.json();
          if (!data.accessToken || !(data.expiresIn > 0)) throw new Error('Invalid login response.');
          const expires = Date.now() + data.expiresIn * 1000;
          await this.acceptVerifiedUser({ access_token: data.accessToken, expired: false,
            expires_at: expires / 1000, profile: {} } as User);
          sessionStorage.setItem('vat_secure_token', data.accessToken);
          sessionStorage.setItem('vat_secure_expires', String(expires));
          subscriber.next(this.currentUser()!);
          subscriber.complete();
        } catch (error) {
          this.clearSession();
          subscriber.error(error instanceof TypeError || (error instanceof Error && error.name === 'AbortError')
            ? new Error('Cannot reach the login server. Check that the backend is running and try again.') : error);
        } finally { clearTimeout(timeout); }
      })();
      return () => { clearTimeout(timeout); controller.abort(); };
    });
  }

  loadUsers(): Observable<UserAccount[]> {
    return this.http.get<UserAccount[]>(`${environment.apiGatewayUrl}/companies/${this.contextService.companyGuid()}/identity/users`);
  }
  register(request: RegisterUserRequest): Observable<UserAccount> {
    return this.http.post<UserAccount>(`${environment.apiGatewayUrl}/companies/${request.companyId}/identity/users`, request);
  }

  private accountChangeUnavailable(): false {
    this.toastService.showWarning('Manage accounts and grants through your identity provider.'); return false;
  }
  updateUser(_id: string, _updates: Partial<UserAccount>): boolean { return this.accountChangeUnavailable(); }
  toggleUserStatus(_id: string): boolean { return this.accountChangeUnavailable(); }
  deleteUser(_id: string): boolean { return this.accountChangeUnavailable(); }
  resetUserPassword(_id: string, _password?: string): boolean { return this.accountChangeUnavailable(); }
  getDefaultModulesForRoles(roles: string[]): string[] {
    const defaults: Record<string, string[]> = {
      SuperAdmin: ['MOD_PURCHASES','MOD_SALES','MOD_MANUFACTURING','MOD_INVENTORY','MOD_COMPLIANCE','MOD_WORKFLOW','MOD_MASTERDATA','MOD_REPORTS','MOD_ADMIN'],
      Maker: ['MOD_PURCHASES','MOD_SALES','MOD_MANUFACTURING','MOD_INVENTORY','MOD_MASTERDATA'],
      Checker: ['MOD_PURCHASES','MOD_SALES','MOD_INVENTORY','MOD_WORKFLOW','MOD_MASTERDATA','MOD_REPORTS'],
      TaxManager: ['MOD_COMPLIANCE','MOD_REPORTS'], Approver: ['MOD_WORKFLOW','MOD_MANUFACTURING'],
      Auditor: ['MOD_PURCHASES','MOD_SALES','MOD_INVENTORY','MOD_COMPLIANCE','MOD_REPORTS']
    };
    return [...new Set(roles.flatMap(role => defaults[role] || []))];
  }
  getDefaultModulesForRole(role: string): string[] { return this.getDefaultModulesForRoles([role]); }
  getPermissionsForRoles(_roles: string[]): string[] { return []; }
  getPermissionsForRole(_role: string): string[] { return []; }
}
