import {Component, OnInit, signal} from '@angular/core';
import {AxiosService} from "../../shared/services/axios.service";
import {ActualityModel} from "../../shared/models/actuality.model";
import {MethodEnum} from "../../shared/enums/method.enum";
import { groupBy } from 'lodash';

@Component({
    selector: 'app-actuality-page',
    templateUrl: './actuality-page.component.html',
    standalone: false
})
export class ActualityPageComponent implements OnInit{
  actualities = signal<ActualityModel[]>([])
  isLoading = signal(true);
  groupedActualities = signal<{ month: string, year: number, actualities: ActualityModel[] }[]>([]);

  constructor(private axiosService: AxiosService) {
  }

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

  ngOnInit(): void {
    this.axiosService.request(MethodEnum.GET, 'actuality/all', {}).then(actus => {
      this.actualities.set(actus);
      this.groupedActualities.set(this.groupByMonth(actus));
      this.isLoading.set(false);
    })
  }

  private groupByMonth(actus: ActualityModel[]): { month: string, year: number, actualities: ActualityModel[] }[] {
    const grouped = groupBy(actus, (actuality) => {
      const date = new Date(actuality.date);
      return date.toLocaleString('fr-FR', { month: 'long', year: 'numeric' });
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

    // Trier les groupes par date, du plus récent au plus ancien
    groupedArray.sort((a, b) => {
      const dateA = new Date(a.year, this.getNumberMonth(a.month));
      const dateB = new Date(b.year, this.getNumberMonth(b.month));
      return dateB.getTime() - dateA.getTime();
    });

    return groupedArray;
  }

}
