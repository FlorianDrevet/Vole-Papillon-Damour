import { platformBrowser } from '@angular/platform-browser';

import { AppModule } from './app/app.module';
import { initApplicationInsights } from './app/shared/services/application-insights';

/**
 * MSAL renouvelle les jetons silencieusement en chargeant `redirectUri` dans une
 * iframe cachée. Ce `redirectUri` est la racine du BackOffice : sans ce garde-fou,
 * toute l'application démarre une seconde fois dans l'iframe. MsalGuard y refuse
 * alors d'activer la route (il bloque volontairement les rechargements en iframe
 * cachée) et surtout ce démarrage complet peut dépasser le délai que MSAL accorde
 * à l'iframe — le renouvellement échoue et l'utilisateur se retrouve déconnecté.
 *
 * L'iframe n'a rien à afficher : MSAL lit seulement son URL depuis la fenêtre
 * parente. On ne démarre donc Angular que dans la fenêtre principale.
 */
function isAuthenticationFrame(): boolean {
  if (window.self === window.top) {
    return false;
  }

  const response = `${window.location.hash}${window.location.search}`;
  return /[#&?](code|error|state)=/.test(response);
}

if (!isAuthenticationFrame()) {
  initApplicationInsights();

  // `platformBrowser` et non `platformBrowserDynamic` : les gabarits sont compilés
  // au moment de la construction (AOT), la variante « dynamic » n'apporte que le
  // compilateur d'exécution, dont l'application n'a pas l'usage.
  platformBrowser().bootstrapModule(AppModule, {
    ngZoneEventCoalescing: true,
  })
    .catch(err => console.error(err));
}
