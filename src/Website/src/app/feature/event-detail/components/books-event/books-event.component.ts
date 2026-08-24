import {Component, input} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {ProductSectionEnum} from "../../../../shared/enums/productSection.enum";

@Component({
    selector: 'app-books-event',
    templateUrl: './books-event.component.html',
    styleUrl: './books-event.component.scss',
    standalone: false
})
export class BooksEventComponent {
  vpdEvent = input.required<VpdEventModel>()
  protected readonly ProductSectionEnum = ProductSectionEnum;
}
