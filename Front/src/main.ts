import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

async function bootstrap(): Promise<void> {
  try {
    const res = await fetch('config.json', { cache: 'no-cache' });
    if (res.ok) {
      window.__APP_CONFIG__ = await res.json();
    }
  } catch {
    // No runtime config; fall back to the compiled default.
  }
  await bootstrapApplication(AppComponent, appConfig);
}

bootstrap().catch((err) => console.error(err));
