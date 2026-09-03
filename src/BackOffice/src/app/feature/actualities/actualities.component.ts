import {Component, computed, inject, OnInit, signal} from '@angular/core';
import {MatDialog} from "@angular/material/dialog";
import {groupBy} from 'lodash';

import {ActualityModel} from "../../shared/models/actuality.model";
import {AxiosService} from "../../shared/services/axios.service";
import {MethodEnum} from "../../shared/enums/method.enum";
import {
  CreateUpdateActualityDialogComponent
} from "../../shared/components/dialogs/create-update-actuality-dialog/create-update-actuality-dialog.component";

interface ActualityMonth {
  month: string;
  year: number;
  actualities: ActualityModel[];
}

const MONTH_NUMBERS: Record<string, number> = {
  janvier: 1,
  février: 2,
  mars: 3,
  avril: 4,
  mai: 5,
  juin: 6,
  juillet: 7,
  août: 8,
  septembre: 9,
  octobre: 10,
  novembre: 11,
  décembre: 12,
};

@Component({
    selector: 'app-actualities',
    templateUrl: './actualities.component.html',
    standalone: false
})
export class ActualitiesComponent implements OnInit {
  private readonly axiosService = inject(AxiosService);
  private readonly dialog = inject(MatDialog);

  private readonly actualities = signal<ActualityModel[]>([]);

  protected readonly isLoading = signal(true);
  protected readonly hasFailed = signal(false);

  /**
   * Regroupement dérivé de la liste, et non recopié à chaque mutation : les deux
   * signaux se désynchronisaient dès qu'une branche oubliait de rejouer le
   * regroupement après une création ou une suppression.
   */
  protected readonly groupedActualities = computed<ActualityMonth[]>(() =>
    this.groupByMonth(this.actualities()),
  );

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.isLoading.set(true);
    this.hasFailed.set(false);

    this.axiosService.request$(MethodEnum.GET, 'actuality/all', {})
      .then((actualities: ActualityModel[]) => {
        this.actualities.set(actualities);
        this.isLoading.set(false);
      })
      .catch(() => {
        // Sans cette branche, un appel en échec laissait l'indicateur de
        // chargement tourner indéfiniment, sans message ni moyen de réessayer.
        this.hasFailed.set(true);
        this.isLoading.set(false);
      });
  }

  protected openDialogCreation(): void {
    const dialogRef = this.dialog.open(CreateUpdateActualityDialogComponent, {
      maxWidth: '100vw',
      width: 'fit-content',
      height: 'fit-content',
    });

    dialogRef.afterClosed().subscribe((result?: ActualityModel | null) => {
      // Fermer le dialogue par la touche Échap ou en cliquant à côté renvoie
      // `undefined`, que l'ancien test `!== null` laissait passer : une entrée
      // vide était alors ajoutée à la liste.
      if (result) {
        this.actualities.update(all => this.sortByDate([result, ...all]));
      }
    });
  }

  protected actualityUpdated(updated: ActualityModel): void {
    this.actualities.update(all =>
      this.sortByDate(all.map(actuality => actuality.id === updated.id ? updated : actuality)),
    );
  }

  protected actualityDeleted(id: string): void {
    if (!id) {
      return;
    }

    this.actualities.update(all => all.filter(actuality => actuality.id !== id));
  }

  private sortByDate(actualities: ActualityModel[]): ActualityModel[] {
    return [...actualities].sort(
      (a, b) => new Date(b.date).getTime() - new Date(a.date).getTime(),
    );
  }

  private groupByMonth(actualities: ActualityModel[]): ActualityMonth[] {
    const grouped = groupBy(actualities, actuality =>
      new Date(actuality.date).toLocaleString('fr-FR', {month: 'long', year: 'numeric'}),
    );

    return Object.entries(grouped)
      .map(([key, monthActualities]) => {
        const [month, year] = key.split(' ');
        return {month, year: parseInt(year, 10), actualities: monthActualities};
      })
      .sort((a, b) => {
        const dateA = new Date(a.year, MONTH_NUMBERS[a.month.toLowerCase()]);
        const dateB = new Date(b.year, MONTH_NUMBERS[b.month.toLowerCase()]);
        return dateB.getTime() - dateA.getTime();
      });
  }
}
