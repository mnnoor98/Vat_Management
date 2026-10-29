import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'bengaliNumber',
  standalone: true
})
export class BengaliNumberPipe implements PipeTransform {
  private readonly enToBnMap: { [key: string]: string } = {
    '0': '০', '1': '১', '2': '২', '3': '৩', '4': '৪',
    '5': '৫', '6': '৬', '7': '৭', '8': '৮', '9': '৯',
    '.': '.'
  };

  transform(value: string | number | null | undefined): string {
    if (value === null || value === undefined) return '';
    const str = value.toString();
    return str.replace(/[0-9]/g, match => this.enToBnMap[match] || match);
  }
}
