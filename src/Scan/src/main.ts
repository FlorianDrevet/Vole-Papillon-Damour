import 'zone.js';
import {platformBrowser} from '@angular/platform-browser';

import {AppModule} from './app/app.module';
import {initApplicationInsights} from './app/application-insights';

void initApplicationInsights();

platformBrowser()
  .bootstrapModule(AppModule)
  .catch(error => console.error(error));
