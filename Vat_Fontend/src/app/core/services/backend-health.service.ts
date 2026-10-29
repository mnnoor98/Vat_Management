import { Injectable, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { catchError, map, of, timeout } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface HealthCheckResult {
  status: 'online' | 'offline' | 'checking';
  live: boolean;
  ready: boolean;
  latencyMs: number;
  serviceName: string;
  checkedAt: Date;
  details?: string;
}

@Injectable({
  providedIn: 'root'
})
export class BackendHealthService {
  private http = inject(HttpClient);

  public readonly status = signal<'online' | 'offline' | 'checking'>('checking');
  public readonly isLive = signal<boolean>(false);
  public readonly isReady = signal<boolean>(false);
  public readonly latencyMs = signal<number>(0);
  public readonly serviceName = signal<string>('Vat.Api (ASP.NET Core 10)');
  public readonly lastChecked = signal<Date | null>(null);
  public readonly environmentInfo = signal(environment);

  constructor() {
    this.checkHealth();
  }

  public checkHealth(): void {
    this.status.set('checking');
    const start = performance.now();

    // Check live endpoint
    this.http.get<{ status: string; service?: string }>(`${environment.healthCheckUrl}/live`)
      .pipe(
        timeout(3000),
        catchError(err => {
          console.warn('[BackendHealthService] Health check failed:', err);
          return of(null);
        })
      )
      .subscribe(liveRes => {
        const latency = Math.round(performance.now() - start);
        this.latencyMs.set(latency);
        this.lastChecked.set(new Date());

        if (liveRes && liveRes.status === 'Healthy') {
          this.isLive.set(true);
          this.status.set('online');
          if (liveRes.service) {
            this.serviceName.set(liveRes.service);
          }

          // Also check readiness (database connectivity)
          this.http.get<{ status: string }>(`${environment.healthCheckUrl}/ready`)
            .pipe(
              timeout(3000),
              catchError(() => of(null))
            )
            .subscribe(readyRes => {
              this.isReady.set(readyRes?.status === 'Healthy');
            });
        } else {
          this.isLive.set(false);
          this.isReady.set(false);
          this.status.set('offline');
        }
      });
  }
}
