import { Injectable, inject, signal } from '@angular/core';
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

interface VerifiedProfile { subject: string; tenantId: string; companyIds: string[]; permissions: string[]; }
interface AuthConfiguration { authority: string; clientId: string; scope?: string; }

@Injectable({ providedIn: 'root' })
export class AuthService {
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

    // Check secure local session first
    const savedToken = sessionStorage.getItem('vat_secure_token');
    const savedUser = sessionStorage.getItem('vat_secure_user');
    const savedExp = Number(sessionStorage.getItem('vat_secure_expires') || '0');

    if (savedToken && savedUser && Date.now() < savedExp) {
      try {
        const account = JSON.parse(savedUser) as UserAccount;
        this.token.set(savedToken);
        this.currentUser.set(account);
        this.contextService.currentUser.set(account);
        this.expiresAt = savedExp;
        this.isAuthenticated.set(true);
        void this.contextService.loadVerifiedCompanies(savedToken, account.accessibleCompanyIds || [account.companyId]);
        return;
      } catch {
        this.clearSession();
      }
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
    if (profile.subject !== user.profile.sub || !Array.isArray(profile.permissions) || !Array.isArray(profile.companyIds)) {
      throw new Error('The verified API subject or grants do not match this session.');
    }
    const account: UserAccount = {
      id: profile.subject, username: user.profile.preferred_username || profile.subject,
      fullName: user.profile.name || profile.subject, fullNameBn: user.profile.name || profile.subject,
      email: user.profile.email || '', role: 'User', roles: ['User'],
      companyId: profile.companyIds[0] || '', companyName: '', branchId: '', branchName: '',
      accessibleCompanyIds: profile.companyIds, permissions: profile.permissions,
      assignedModuleCodes: this.modulesForPermissions(profile.permissions), isActive: true, createdAt: ''
    };
    this.currentUser.set(account);
    this.contextService.currentUser.set(account);
    await this.contextService.loadVerifiedCompanies(user.access_token, profile.companyIds);
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
      (async () => {
        try {
          const res = await fetch(`${environment.apiGatewayUrl}/auth/login`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username: credentials.username, password: credentials.password })
          });

          if (!res.ok) {
            const errBody = await res.json().catch(() => null);
            const msg = errBody?.detail || 'ইউজারনেম বা পাসওয়ার্ড সঠিক নয়। (Invalid username or password)';
            throw new Error(msg);
          }

          const data = await res.json();
          const user = data.user;
          const roleMap: Record<string, EnterpriseRole> = {
            'Admin': 'SuperAdmin',
            'Maker': 'Maker',
            'Checker': 'Checker',
            'Auditor': 'Auditor'
          };
          const primaryRole: EnterpriseRole = roleMap[user.roles?.[0]] || 'Maker';

          const account: UserAccount = {
            id: user.id,
            username: user.username,
            fullName: user.displayName,
            fullNameBn: user.displayName,
            email: `${user.username}@enterprise.vat`,
            role: primaryRole,
            roles: [primaryRole],
            companyId: user.companyId,
            companyName: 'Padma Consumer Goods & Manufacturing Ltd',
            branchId: '33333333-3333-3333-3333-333333333331',
            branchName: 'Dhaka Central Plant & HQ',
            accessibleCompanyIds: [user.companyId],
            permissions: user.permissions,
            assignedModuleCodes: this.modulesForPermissions(user.permissions),
            isActive: true,
            createdAt: new Date().toISOString()
          };

          this.token.set(data.accessToken);
          this.isAuthenticated.set(true);
          this.expiresAt = Date.now() + (data.expiresIn * 1000);
          this.currentUser.set(account);
          this.contextService.currentUser.set(account);

          sessionStorage.setItem('vat_secure_token', data.accessToken);
          sessionStorage.setItem('vat_secure_user', JSON.stringify(account));
          sessionStorage.setItem('vat_secure_expires', String(this.expiresAt));

          await this.contextService.loadVerifiedCompanies(data.accessToken, [user.companyId]);

          subscriber.next(account);
          subscriber.complete();
        } catch (err: any) {
          subscriber.error(err);
        }
      })();
    });
  }

  register(_request: RegisterUserRequest): Observable<UserAccount> {
    return throwError(() => new Error('Account creation and role assignment must be performed by your identity administrator.'));
  }

  private accountChangeUnavailable(): false {
    this.toastService.showWarning('Manage accounts and grants through your identity provider.'); return false;
  }
  updateUser(_id: string, _updates: Partial<UserAccount>): boolean { return this.accountChangeUnavailable(); }
  toggleUserStatus(_id: string): boolean { return this.accountChangeUnavailable(); }
  deleteUser(_id: string): boolean { return this.accountChangeUnavailable(); }
  resetUserPassword(_id: string, _password?: string): boolean { return this.accountChangeUnavailable(); }
  getDefaultModulesForRoles(_roles: string[]): string[] { return []; }
  getDefaultModulesForRole(_role: string): string[] { return []; }
  getPermissionsForRoles(_roles: string[]): string[] { return []; }
  getPermissionsForRole(_role: string): string[] { return []; }
}
