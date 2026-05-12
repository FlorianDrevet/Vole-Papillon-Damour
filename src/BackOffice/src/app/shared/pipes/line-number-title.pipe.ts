import {Pipe, PipeTransform} from '@angular/core';
import {NumberLineEnum} from "../enums/numberLine.enum";

@Pipe({
  name: 'lineNumberTitle'
})
export class LineNumberTitlePipe implements PipeTransform {

  transform(value: NumberLineEnum): string {
    console.log("VALUE", value);
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
