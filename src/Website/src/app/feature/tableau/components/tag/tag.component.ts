import {Component, Input} from '@angular/core';

@Component({
    selector: 'app-tag',
    templateUrl: './tag.component.html',
    styleUrl: './tag.component.scss',
    standalone: false
})
export class TagComponent {
  @Input() public value: string = '';
  @Input() public isBingo: boolean = false;
}
