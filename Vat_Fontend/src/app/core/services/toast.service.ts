import { Injectable, signal } from '@angular/core';

export interface ToastItem {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title?: string;
  message: string;
  duration?: number;
}

@Injectable({
  providedIn: 'root'
})
export class ToastService {
  public readonly toasts = signal<ToastItem[]>([]);

  public show(message: string, type: 'success' | 'error' | 'warning' | 'info' = 'info', title?: string, duration: number = 3500): void {
    const id = `toast-${Date.now()}-${Math.random().toString(36).substr(2, 4)}`;
    const toast: ToastItem = { id, type, message, title, duration };

    this.toasts.update(current => [...current, toast]);

    if (duration > 0) {
      setTimeout(() => {
        this.remove(id);
      }, duration);
    }
  }

  public success(message: string, title?: string): void {
    this.show(message, 'success', title || 'সফল (Success)');
  }

  public error(message: string, title?: string): void {
    this.show(message, 'error', title || 'ত্রুটি (Error)', 4500);
  }

  public warning(message: string, title?: string): void {
    this.show(message, 'warning', title || 'সতর্কতা (Warning)');
  }

  public info(message: string, title?: string): void {
    this.show(message, 'info', title || 'তথ্য (Info)');
  }

  public showSuccess(message: string, title?: string): void {
    this.success(message, title);
  }

  public showError(message: string, title?: string): void {
    this.error(message, title);
  }

  public showWarning(message: string, title?: string): void {
    this.warning(message, title);
  }

  public showInfo(message: string, title?: string): void {
    this.info(message, title);
  }

  public remove(id: string): void {
    this.toasts.update(current => current.filter(t => t.id !== id));
  }
}
