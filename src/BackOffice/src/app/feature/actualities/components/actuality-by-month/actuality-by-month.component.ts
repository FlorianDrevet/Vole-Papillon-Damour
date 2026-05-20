import {Component, input, output} from '@angular/core';
import {ActualityModel} from "../../../../shared/models/actuality.model";

@Component({
    selector: 'app-actuality-by-month',
    templateUrl: './actuality-by-month.component.html',
    styleUrl: './actuality-by-month.component.scss',
    standalone: false
})
export class ActualityByMonthComponent {
  Actualities = input<ActualityModel[]>([])
  Title = input.required<string>()
  actualityDeleted = output<string>()
  actualityUpdated = output<ActualityModel>()

  deleteActuality(id: string): void {
    this.actualityDeleted.emit(id)
  }

  updateActuality(actuality: ActualityModel): void {
    this.actualityUpdated.emit(actuality)
  }
}
