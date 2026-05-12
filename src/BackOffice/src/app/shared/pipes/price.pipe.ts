import {Pipe, PipeTransform} from '@angular/core';

@Pipe({
  name: 'price'
})
export class PricePipe implements PipeTransform {

  transform(value: number, currency: string = '€'): string {
    return value.toFixed(2) + currency;
  }
}
