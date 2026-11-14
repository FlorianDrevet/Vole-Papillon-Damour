import {Component, input, ChangeDetectionStrategy} from '@angular/core';
import {ActualityModel} from "../../../../shared/models/actuality.model";

@Component({
    selector: 'app-actuality-by-month',
    templateUrl: './actuality-by-month.component.html',
    styleUrl: './actuality-by-month.component.scss',
    changeDetection: ChangeDetectionStrategy.Eager,
    standalone: false
})
export class ActualityByMonthComponent {
  Actualities = input<ActualityModel[]>([])
  Title = input.required<string>()
}
