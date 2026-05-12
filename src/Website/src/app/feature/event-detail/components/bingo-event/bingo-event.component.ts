import {Component, input} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {ProductSectionEnum} from "../../../../shared/enums/productSection.enum";

@Component({
    selector: 'app-bingo-event',
    templateUrl: './bingo-event.component.html',
    styleUrl: './bingo-event.component.scss',
    standalone: false
})
export class BingoEventComponent {
  vpdEvent = input.required<VpdEventModel>()
  protected readonly ProductSectionEnum = ProductSectionEnum;
}
