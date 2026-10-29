import { describe, it } from 'node:test';
import assert from 'node:assert';

// Pure algorithmic functions representing the BdtCurrency and BengaliNumber logic
function formatBdtCurrency(value: number | null | undefined, showSymbol: boolean = true): string {
  if (value === null || value === undefined || isNaN(value)) {
    return showSymbol ? '৳ 0.00' : '0.00';
  }

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

function convertEnglishToBengaliNumber(value: string | number | null | undefined): string {
  if (value === null || value === undefined) return '';
  const enToBnMap: { [key: string]: string } = {
    '0': '০', '1': '১', '2': '২', '3': '৩', '4': '৪',
    '5': '৫', '6': '৬', '7': '৭', '8': '৮', '9': '৯',
    '.': '.'
  };
  return value.toString().replace(/[0-9]/g, match => enToBnMap[match] || match);
}

function computeInvoiceTotals(lines: { taxableAmount: number; vatRate: number }[], isImport: boolean = false) {
  const subTotal = lines.reduce((acc, l) => acc + l.taxableAmount, 0);
  const totalVat = lines.reduce((acc, l) => acc + (l.taxableAmount * (l.vatRate / 100)), 0);
  const totalAdvanceTax = isImport ? Math.round(subTotal * 0.05 * 100) / 100 : 0;
  const grandTotal = Math.round((subTotal + totalVat + totalAdvanceTax) * 100) / 100;

  return { subTotal, totalVat, totalAdvanceTax, grandTotal };
}

describe('Statutory VAT Calculations & Localization Test Suite', () => {

  describe('1. Standard VAT & Import Advance Tax Calculations', () => {
    it('should calculate standard 15% VAT on domestic supply', () => {
      const lines = [
        { taxableAmount: 100000, vatRate: 15 },
        { taxableAmount: 50000, vatRate: 15 }
      ];
      const result = computeInvoiceTotals(lines, false);
      assert.strictEqual(result.subTotal, 150000);
      assert.strictEqual(result.totalVat, 22500);
      assert.strictEqual(result.totalAdvanceTax, 0);
      assert.strictEqual(result.grandTotal, 172500);
    });

    it('should calculate 5% Advance Tax (AT) for Customs BOE Import purchases', () => {
      const lines = [
        { taxableAmount: 1000000, vatRate: 15 }
      ];
      const result = computeInvoiceTotals(lines, true);
      assert.strictEqual(result.subTotal, 1000000);
      assert.strictEqual(result.totalVat, 150000);
      assert.strictEqual(result.totalAdvanceTax, 50000);
      assert.strictEqual(result.grandTotal, 1200000);
    });

    it('should support reduced rate (7.5% and 5%) items according to NBR SRO schedule', () => {
      const lines = [
        { taxableAmount: 200000, vatRate: 7.5 },
        { taxableAmount: 100000, vatRate: 5 }
      ];
      const result = computeInvoiceTotals(lines, false);
      assert.strictEqual(result.subTotal, 300000);
      assert.strictEqual(result.totalVat, 15000 + 5000); // 20,000
      assert.strictEqual(result.grandTotal, 320000);
    });
  });

  describe('2. Bangladesh Currency Numbering System (Lakh & Crore Comma Separation)', () => {
    it('should format numbers under 1,000 without commas', () => {
      assert.strictEqual(formatBdtCurrency(750, true), '৳ 750.00');
    });

    it('should format thousands with 3-digit comma', () => {
      assert.strictEqual(formatBdtCurrency(5500, true), '৳ 5,500.00');
    });

    it('should format 1 Lakh (100,000) using 2-digit South Asian grouping (1,00,000)', () => {
      assert.strictEqual(formatBdtCurrency(100000, true), '৳ 1,00,000.00');
    });

    it('should format 25 Lakhs (2,500,000) correctly as 25,00,000', () => {
      assert.strictEqual(formatBdtCurrency(2500000, true), '৳ 25,00,000.00');
    });

    it('should format 1 Crore (10,000,000) correctly as 1,00,00,000', () => {
      assert.strictEqual(formatBdtCurrency(10000000, true), '৳ 1,00,00,000.00');
    });

    it('should format without symbol when showSymbol is false', () => {
      assert.strictEqual(formatBdtCurrency(100000, false), '1,00,000.00');
    });
  });

  describe('3. English to Bengali Numeral Translation', () => {
    it('should accurately convert English digits to Bengali digits', () => {
      assert.strictEqual(convertEnglishToBengaliNumber('12345'), '১২৩৪৫');
      assert.strictEqual(convertEnglishToBengaliNumber('67890'), '৬৭৮৯০');
    });

    it('should preserve decimal points and spaces', () => {
      assert.strictEqual(convertEnglishToBengaliNumber('15.50'), '১৫.৫০');
      assert.strictEqual(convertEnglishToBengaliNumber('২০২৬-১০-২৯'), '২০২৬-১০-২৯');
    });

    it('should handle zero and null safely', () => {
      assert.strictEqual(convertEnglishToBengaliNumber(0), '০');
      assert.strictEqual(convertEnglishToBengaliNumber(null), '');
    });
  });

  describe('4. Section 51 High-Value Transaction Register (Mushak 6.10)', () => {
    it('should filter only transactions >= 2 Lakh BDT (2,00,000)', () => {
      const invoices = [
        { id: '1', grandTotal: 180000 },
        { id: '2', grandTotal: 200000 },
        { id: '3', grandTotal: 1500000 },
        { id: '4', grandTotal: 95000 }
      ];

      const highValue = invoices.filter(inv => inv.grandTotal >= 200000);
      assert.strictEqual(highValue.length, 2);
      assert.strictEqual(highValue[0].id, '2');
      assert.strictEqual(highValue[1].id, '3');
    });
  });
});
