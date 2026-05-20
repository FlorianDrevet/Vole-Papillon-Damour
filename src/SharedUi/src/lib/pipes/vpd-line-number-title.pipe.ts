import { Pipe, PipeTransform } from '@angular/core';
import { NumberLineEnum } from '../enums/number-line.enum';

@Pipe({
  name: 'lineNumberTitle',
  standalone: false,
})
export class VpdLineNumberTitlePipe implements PipeTransform {
  transform(value: NumberLineEnum): string {
    switch (value) {
      case NumberLineEnum.CARTONPLEIN:
        return 'Carton plein';
      case NumberLineEnum.ONELINE:
        return '1 ligne X 2';
      case NumberLineEnum.TWOLINE:
        return '2 lignes';
    }
  }
}
