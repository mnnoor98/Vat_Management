import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'bdtCurrency',
  standalone: true
})
export class BdtCurrencyPipe implements PipeTransform {
  transform(value: number | null | undefined, showSymbol: boolean = true, compact: boolean = false): string {
    if (value === null || value === undefined || isNaN(value)) {
      return showSymbol ? '৳ 0.00' : '0.00';
    }

    if (compact) {
      if (Math.abs(value) >= 10000000) { // 1 Crore
        return `${showSymbol ? '৳ ' : ''}${(value / 10000000).toFixed(2)} Cr`;
      }
      if (Math.abs(value) >= 1000000) { // 10 Lakh / 1 Million
        return `${showSymbol ? '৳ ' : ''}${(value / 1000000).toFixed(2)}M`;
      }
      if (Math.abs(value) >= 100000) { // 1 Lakh
        return `${showSymbol ? '৳ ' : ''}${(value / 100000).toFixed(2)}L`;
      }
    }

    // Standard South Asian numbering format (Lakh / Crore)
    const parts = value.toFixed(2).split('.');
    let integerPart = parts[0];
    const decimalPart = parts[1];

    const isNegative = integerPart.startsWith('-');
    if (isNegative) {
      integerPart = integerPart.substring(1);
    }

    let lastThree = integerPart.substring(integerPart.length - 3);
    const otherNumbers = integerPart.substring(0, integerPart.length - 3);

    if (otherNumbers !== '') {
      lastThree = ',' + lastThree;
    }
    const formattedOther = otherNumbers.replace(/\B(?=(\d{2})+(?!\d))/g, ',');
    const formattedNumber = `${isNegative ? '-' : ''}${formattedOther}${lastThree}.${decimalPart}`;

    return showSymbol ? `৳ ${formattedNumber}` : formattedNumber;
  }
}
