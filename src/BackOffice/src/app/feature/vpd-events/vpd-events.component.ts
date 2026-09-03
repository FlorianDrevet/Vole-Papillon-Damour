import {Component, computed, inject, OnInit, signal} from '@angular/core';
import {MatDialog} from "@angular/material/dialog";

import {VpdEventsFacadeService} from "../../shared/facades/vpd-events.facade.service";
import {VpdEventModel} from "../../shared/models/vpdEvent.model";
import {VpdEventEnum} from "../../shared/enums/vpdEvent.enum";
import {
  CreateUpdateEventDialogComponent
} from "../../shared/components/dialogs/create-update-event-dialog/create-update-event-dialog.component";

@Component({
    selector: 'app-vpd-events',
    templateUrl: './vpd-events.component.html',
    standalone: false
})
export class VpdEventsComponent implements OnInit {
  private readonly vpdEventsFacade = inject(VpdEventsFacadeService);
  private readonly dialog = inject(MatDialog);

  /**
   * Une seule liste, découpée par type à l'affichage. Les trois listes séparées
   * devaient être maintenues en parallèle à chaque création, modification et
   * suppression — la branche « autre évènement » de la suppression filtrait déjà
   * la mauvaise liste, ce qui faisait disparaître des bourses aux livres.
   */
  private readonly events = signal<VpdEventModel[]>([]);

  protected readonly isLoading = signal(true);
  protected readonly hasFailed = signal(false);

  protected readonly bingoEvents = computed(() => this.eventsOfType(VpdEventEnum.Bingo));
  protected readonly booksEvents = computed(() => this.eventsOfType(VpdEventEnum.Books));
  protected readonly otherEvents = computed(() => this.eventsOfType(VpdEventEnum.Other));
  protected readonly isEmpty = computed(() => this.events().length === 0);

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.isLoading.set(true);
    this.hasFailed.set(false);

    this.vpdEventsFacade.getAllEvents$()
      .then((events: VpdEventModel[]) => {
        this.events.set(events.map(event => ({
          ...event,
          eventType: VpdEventEnum[event.eventType as unknown as keyof typeof VpdEventEnum],
        })));
        this.isLoading.set(false);
      })
      .catch(() => {
        this.hasFailed.set(true);
        this.isLoading.set(false);
      });
  }

  protected openDialogCreation(): void {
    const dialogRef = this.dialog.open(CreateUpdateEventDialogComponent, {
      maxWidth: '90vw',
      width: 'fit-content',
      height: 'fit-content',
    });

    dialogRef.afterClosed().subscribe((result?: VpdEventModel | null) => {
      // Fermer le dialogue sans valider renvoie `undefined` : l'ancienne version
      // lisait `result.eventType` sans attendre et faisait échouer la souscription.
      if (result) {
        this.events.update(all => [...all, result]);
      }
    });
  }

  protected onEventDeleted(deleted: VpdEventModel): void {
    this.events.update(all => all.filter(event => event.id !== deleted.id));
  }

  protected onEventUpdated(updated: VpdEventModel): void {
    this.events.update(all => all.map(event => event.id === updated.id ? updated : event));
  }

  private eventsOfType(type: VpdEventEnum): VpdEventModel[] {
    return this.events().filter(event => event.eventType === type);
  }
}
