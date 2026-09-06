import {Component} from '@angular/core';

@Component({
  selector: 'vpd-book-cover-placeholder',
  template: `
    <svg
      class="book-cover-placeholder"
      viewBox="0 0 180 260"
      aria-hidden="true"
      focusable="false">
      <rect class="cover-paper" x="14" y="10" width="142" height="240" rx="3"></rect>
      <path class="cover-spine" d="M14 10h16v240H14z"></path>
      <path class="cover-line" d="M54 66h76M54 78h56M54 90h67"></path>
      <path class="cover-bookmark" d="M54 126h67v74l-33.5-19-33.5 19z"></path>
      <path class="cover-mark" d="M75 150h24M87 138v25"></path>
      <circle class="cover-dot" cx="132" cy="30" r="5"></circle>
    </svg>
  `,
  styles: [`
    :host {
      display: block;
      width: 100%;
      height: 100%;
    }

    .book-cover-placeholder {
      display: block;
      width: 100%;
      height: 100%;
    }

    .cover-paper {
      fill: #f8fcff;
      stroke: #9ec8e2;
      stroke-width: 2;
    }

    .cover-spine {
      fill: #c9e5f4;
    }

    .cover-line {
      fill: none;
      stroke: #5d9ec4;
      stroke-linecap: round;
      stroke-width: 4;
    }

    .cover-bookmark {
      fill: #e7f3fa;
      stroke: #286b99;
      stroke-linejoin: round;
      stroke-width: 2;
    }

    .cover-mark {
      fill: none;
      stroke: #ed8b52;
      stroke-linecap: round;
      stroke-width: 4;
    }

    .cover-dot {
      fill: #ed8b52;
    }
  `],
  standalone: false,
})
export class VpdBookCoverPlaceholderComponent {}
