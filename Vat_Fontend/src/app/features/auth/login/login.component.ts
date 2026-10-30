import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-login', standalone: true, imports: [CommonModule],
  template: `<main class="sign-in"><section><h1>Enterprise VAT Management</h1>
    <p>আপনার প্রতিষ্ঠানের পরিচয় সেবা দিয়ে নিরাপদে প্রবেশ করুন।</p>
    <p>Sign in with your organization's identity provider.</p>
    <p role="alert" *ngIf="errorMessage">{{ errorMessage }}</p>
    <button class="btn btn-primary" [disabled]="isLoading" (click)="onSubmit()">
      {{ isLoading ? 'Signing in…' : 'Sign in / প্রবেশ করুন' }}</button>
    <p>Account access and permissions are managed by your administrator.</p>
  </section></main>`,
  styles: [`.sign-in { min-height:100vh;display:grid;place-items:center;padding:24px; }
    section { max-width:480px;padding:36px;border:1px solid #64748b;border-radius:16px; }
    [role=alert] { color:#ef4444; } button { margin:16px 0; }`]
})
export class LoginComponent implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  isLoading = false;
  errorMessage = '';
  async ngOnInit(): Promise<void> {
    if (location.pathname !== '/auth/callback') return;
    this.isLoading = true;
    try { await this.router.navigateByUrl(await this.auth.completeLogin(), { replaceUrl: true }); }
    catch (error) { this.errorMessage = error instanceof Error ? error.message : 'Sign-in failed.'; }
    finally { this.isLoading = false; }
  }
  async onSubmit(): Promise<void> {
    this.isLoading = true; this.errorMessage = '';
    try { await this.auth.startLogin(this.route.snapshot.queryParamMap.get('returnUrl') || undefined); }
    catch (error) { this.errorMessage = error instanceof Error ? error.message : 'Sign-in failed.'; this.isLoading = false; }
  }
}
