import 'zone.js';
import {platformBrowserDynamic} from '@angular/platform-browser-dynamic';

import {AppModule} from './app/app.module';
import {initApplicationInsights} from './app/application-insights';

void initApplicationInsights();

platformBrowserDynamic()
  .bootstrapModule(AppModule)
  .catch(error => console.error(error));
