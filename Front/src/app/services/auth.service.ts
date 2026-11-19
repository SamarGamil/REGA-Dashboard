import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../environments/environment';

interface LoginResponse {
  token: string;
  expiresAt: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly TOKEN_KEY = 'aqar_token';

  login(username: string, password: string): Observable<LoginResponse> {
    return this.http
      .post<LoginResponse>(`${environment.apiUrl}/auth/login`, { username, password })
      .pipe(tap(response => localStorage.setItem(this.TOKEN_KEY, response.token)));
  }

  logout(): void {
    // Revoke the token server-side (best-effort), then clear local state regardless of the outcome.
    const clear = () => {
      localStorage.removeItem(this.TOKEN_KEY);
      this.router.navigate(['/login']);
    };
    if (!this.getToken()) {
      clear();
      return;
    }
    this.http.post(`${environment.apiUrl}/auth/logout`, null).subscribe({
      next: clear,
      error: clear
    });
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    const token = this.getToken();
    if (!token) return false;

    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      const expiry = payload.exp * 1000;
      return Date.now() < expiry;
    } catch {
      return false;
    }
  }
}
