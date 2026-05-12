import {Pipe, PipeTransform} from '@angular/core';

@Pipe({
    name: 'price',
    standalone: false
})
export class PricePipe implements PipeTransform {

  transform(value: number, currency: string = '€'): string {
    return value.toFixed(2) + currency;
  }
}
