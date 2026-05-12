import {Component, input} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {BackgroundColorEnum} from "../../../../shared/enums/backgroundColor.enum";
import {ProductSectionEnum} from "../../../../shared/enums/productSection.enum";

@Component({
  selector: 'app-books-event',
  templateUrl: './books-event.component.html',
  styleUrl: './books-event.component.scss'
})
export class BooksEventComponent {

  vpdEvent = input.required<VpdEventModel>()
  protected readonly BackgroundColorEnum = BackgroundColorEnum;
  protected readonly ProductSectionEnum = ProductSectionEnum;
}
