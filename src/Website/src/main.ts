import { platformBrowser } from '@angular/platform-browser';

import { AppModule } from './app/app.module';
import { initApplicationInsights } from './app/shared/services/application-insights';

initApplicationInsights();

platformBrowser().bootstrapModule(AppModule, {
  ngZoneEventCoalescing: true,
})
  .catch(err => console.error(err));
