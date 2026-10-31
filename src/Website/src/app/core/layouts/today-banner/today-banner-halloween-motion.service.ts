import {Injectable} from '@angular/core';

/** État en mémoire uniquement : le fantôme d'arrivée ne court qu'une fois par chargement. */
@Injectable({providedIn: 'root'})
export class TodayBannerHalloweenMotionService {
  private entranceClaimed = false;

  claimEntrance(): boolean {
    if (this.entranceClaimed) {
      return false;
    }
    this.entranceClaimed = true;
    return true;
  }
}
