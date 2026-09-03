import {Component, input} from '@angular/core';
import {VpdEventModel} from "../../../../shared/models/vpdEvent.model";
import {BackgroundColorEnum} from "../../../../shared/enums/backgroundColor.enum";

@Component({
    selector: 'app-books-event',
    templateUrl: './books-event.component.html',
    standalone: false
})
export class BooksEventComponent {

  vpdEvent = input.required<VpdEventModel>()
  protected readonly BackgroundColorEnum = BackgroundColorEnum;
}
