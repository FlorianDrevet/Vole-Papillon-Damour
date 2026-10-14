import {inject} from '@angular/core';
import {CanActivateFn, Router, UrlTree} from '@angular/router';
import {VpdEventsFacadeService} from '../../shared/facades/vpd-events.facade.service';

/**
 * RG-24 : `/loto`, l'adresse courte affichée sous le QR code. Le jour du loto elle
 * ouvre son tableau en direct ; sinon la fiche du prochain loto, ou les événements.
 */
export const lotoRedirectGuard: CanActivateFn = async (): Promise<UrlTree> => {
  const router = inject(Router);
  const events = inject(VpdEventsFacadeService);

  try {
    const today = await events.getTodayEvent();
    if (today?.eventType === 'Bingo') {
      return router.createUrlTree(['/evenement', today.id, 'tableau']);
    }
    const nextLoto = await events.getLatestEventBingo();
    return router.createUrlTree(['/evenement', nextLoto.id]);
  } catch {
    return router.createUrlTree(['/evenement']);
  }
};
