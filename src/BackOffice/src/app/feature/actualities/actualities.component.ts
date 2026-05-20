import {Component, inject, OnInit, signal} from '@angular/core';
import {ActualityModel} from "../../shared/models/actuality.model";
import {AxiosService} from "../../shared/services/axios.service";
import {MethodEnum} from "../../shared/enums/method.enum";
import {groupBy} from 'lodash';
import {MatDialog} from "@angular/material/dialog";
import {
  CreateUpdateActualityDialogComponent
} from "../../shared/components/dialogs/create-update-actuality-dialog/create-update-actuality-dialog.component";


@Component({
    selector: 'app-actualities',
    templateUrl: './actualities.component.html',
    styleUrl: './actualities.component.scss',
    standalone: false
})
export class ActualitiesComponent implements OnInit {
    actualities = signal<ActualityModel[]>([])
    isLoading = signal(true);
    groupedActualities = signal<{ month: string, year: number, actualities: ActualityModel[] }[]>([]);

  private monthMap: { [key: string]: number } = {
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

  getNumberMonth(monthName: string): number {
    return this.monthMap[monthName.toLowerCase()];
  }

    constructor(private axiosService: AxiosService) {
    }

    ngOnInit(): void {
        this.axiosService.request$(MethodEnum.GET, 'actuality/all', {}).then(actus => {
            this.actualities.set(actus);
            this.groupedActualities.set(this.groupByMonth(actus));
            this.isLoading.set(false);
        })
    }

    actualityDeleted($event: string): void {
        if ($event) {
            this.actualities.set(this.actualities().filter(actuality => actuality.id !== $event));
            this.groupedActualities.set(this.groupByMonth(this.actualities()));
        }
    }

  private groupByMonth(actus: ActualityModel[]): { month: string, year: number, actualities: ActualityModel[] }[] {
    const grouped = groupBy(actus, (actuality) => {
      const date = new Date(actuality.date);
      return date.toLocaleString('fr-FR', {month: 'long', year: 'numeric'});
    });

    const groupedArray = [];

    for (const key in grouped) {
      if (grouped.hasOwnProperty(key)) {
        const [month, year] = key.split(' ');
        groupedArray.push({
          month: month,
          year: parseInt(year),
          actualities: grouped[key]
        });
      }
    }

    groupedArray.sort((a, b) => {
      const dateA = new Date(a.year, this.getNumberMonth(a.month));
      const dateB = new Date(b.year, this.getNumberMonth(b.month));
      return dateB.getTime() - dateA.getTime();
    });

    return groupedArray;
  }

    readonly dialog = inject(MatDialog);

    openDialogCreation(): void {
        const dialogRef = this.dialog.open(CreateUpdateActualityDialogComponent, {
            "maxWidth": "100vw",
            "width": "fit-content",
            "height": "fit-content",
        });

        dialogRef.afterClosed().subscribe(result => {
            if (result !== null) {
                this.actualities.update(x => [result, ...x].sort((a, b) => {
                    return new Date(b.date).getTime() - new Date(a.date).getTime();
                }));
                this.groupedActualities.set(this.groupByMonth(this.actualities()));
            }
        });
    }

  actualityUpdated($event: ActualityModel) {
    this.isLoading.set(true);
    this.actualities.set(
      this.actualities()
        .map(actuality => actuality.id === $event.id ? $event : actuality)
        .sort((a, b) => {
          return new Date(b.date).getTime() - new Date(a.date).getTime()
        })
    );
    this.groupedActualities.set(this.groupByMonth(this.actualities()));
    this.isLoading.set(false);
  }
}
