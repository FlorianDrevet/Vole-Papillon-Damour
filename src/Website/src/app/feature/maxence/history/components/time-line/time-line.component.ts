import { Component } from '@angular/core';

interface HistoryChapter {
  year: number;
  title: string;
}

@Component({
    selector: 'app-time-line',
    templateUrl: './time-line.component.html',
    standalone: false
})
export class TimeLineComponent {
  readonly chapters: HistoryChapter[] = [
    { year: 2004, title: 'Sa naissance' },
    { year: 2005, title: 'Maxence a 1 an' },
    { year: 2006, title: 'Maxence a 2 ans' },
    { year: 2007, title: 'Maxence a 3 ans' },
    { year: 2008, title: 'Maxence a 4 ans' },
    { year: 2009, title: 'Maxence a 5 ans' },
    { year: 2010, title: 'Maxence a 6 ans' },
    { year: 2011, title: 'Maxence a 7 ans' },
    { year: 2012, title: 'Maxence a 8 ans' },
    { year: 2013, title: 'Maxence a 9 ans' },
    { year: 2014, title: 'Maxence a 10 ans' },
    { year: 2015, title: 'Maxence a 10 ans' },
    { year: 2016, title: 'Maxence a 11 ans' },
  ];
}
