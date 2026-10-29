import type { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Bangladesh National Board of Revenue (NBR) Official Formats & Validators
 * - VAT BIN: 13 digits (e.g. 001234567-0101 or 0012345670101)
 * - HS Code: 8 digits (e.g. 3901.20.00 or 39012000)
 * - Section 46(1)(e) Banking Channel Compliance: Mandatory for invoices >= 1,00,000 BDT
 * - Mushak 6.3 Vehicle Number: Transport tracking prior to dispatch
 * - Challan Number: Mandatory alphanumeric statutory invoice reference
 */
export class NbrValidators {
  /**
   * Validates NBR 13-digit Business Identification Number (BIN)
   */
  static bin(required: boolean = false): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const val = control.value;
      if (!val || val === '') {
        return required ? { required: { message: 'বিআইএন নম্বর প্রদান আবশ্যক (BIN required)' } } : null;
      }
      const clean = String(val).replace(/[\s-]/g, '');
      const valid = /^\d{13}$/.test(clean);
      return valid 
        ? null 
        : { invalidBin: { value: val, message: 'বিআইএন অবশ্যই ১৩ সংখ্যার হতে হবে (13-digit NBR BIN required)' } };
    };
  }

  /**
   * Validates NBR 8-digit Customs & VAT Harmonized System (HS) Code
   */
  static hsCode(required: boolean = false): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const val = control.value;
      if (!val || val === '') {
        return required ? { required: { message: 'এইচএস কোড প্রদান আবশ্যক (HS Code required)' } } : null;
      }
      const clean = String(val).replace(/[\s.]/g, '');
      const valid = /^\d{8}$/.test(clean);
      return valid 
        ? null 
        : { invalidHsCode: { value: val, message: 'এইচএস কোড অবশ্যই ৮ সংখ্যার হতে হবে (8-digit HS Code e.g. 3901.20.00)' } };
    };
  }

  /**
   * Validates Section 46(1)(e) Banking Channel Payment Reference
   * In Bangladesh VAT Act 2012, if invoice gross amount >= threshold (default 100,000 BDT),
   * banking payment reference is mandatory for claiming input tax rebate.
   */
  static section46Banking(getTotalFn: () => number, threshold: number = 100000): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const currentTotal = getTotalFn();
      const val = control.value ? String(control.value).trim() : '';

      if (currentTotal >= threshold && !val) {
        return {
          section46BankingRequired: {
            threshold,
            currentTotal,
            message: `১,০০,০০০ টাকার অধিক চালানে ধারা ৪৬(১)(ঙ) অনুযায়ী ব্যাংক পেমেন্ট রেফারেন্স (BEFTN/Cheque) বাধ্যতামূলক।`
          }
        };
      }
      return null;
    };
  }

  /**
   * Validates positive number (quantity, rate, value)
   */
  static positiveNumber(min: number = 0.01, allowZero: boolean = false): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const val = control.value;
      if (val === null || val === undefined || val === '') {
        return null;
      }
      const num = Number(val);
      if (isNaN(num)) {
        return { notANumber: { message: 'সঠিক সংখ্যা প্রদান করুন (Must be a valid number)' } };
      }
      if (allowZero && num >= 0) {
        return null;
      }
      if (num < min) {
        return { 
          minPositive: { 
            min, 
            actual: num, 
            message: `মান কমপক্ষে ${min} বা তার বেশি হতে হবে (Value must be at least ${min})` 
          } 
        };
      }
      return null;
    };
  }

  /**
   * Validates Vehicle Type and Registration Number for Mushak 6.3 Goods Removal
   */
  static vehicleNo(required: boolean = true): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const val = control.value ? String(control.value).trim() : '';
      if (!val) {
        return required 
          ? { required: { message: 'পরিবহন যানবাহনের বিবরণ ও নম্বর আবশ্যক (Vehicle details required for 6.3)' } } 
          : null;
      }
      if (val.length < 4) {
        return { 
          invalidVehicle: { 
            message: 'যানবাহন বিবরণ অত্যন্ত সংক্ষিপ্ত (কমপক্ষে ৪ অক্ষর হতে হবে, যেমন: ঢাকা মেট্রো-ট-১২-৩৪৫৬)' 
          } 
        };
      }
      return null;
    };
  }

  /**
   * Validates Challan or Invoice Reference Number
   */
  static challanNumber(required: boolean = true): ValidatorFn {
    return (control: AbstractControl): ValidationErrors | null => {
      const val = control.value ? String(control.value).trim() : '';
      if (!val) {
        return required ? { required: { message: 'চালান নম্বর প্রদান আবশ্যক (Challan / Invoice number required)' } } : null;
      }
      if (val.length < 3) {
        return { invalidChallan: { message: 'চালান নম্বর কমপক্ষে ৩ অক্ষরের হতে হবে (At least 3 characters)' } };
      }
      return null;
    };
  }

  /**
   * Formats raw digits into standard NBR BIN display (001234567-0101)
   */
  static formatBin(raw: string | number): string {
    if (!raw) return '';
    const clean = String(raw).replace(/\D/g, '').slice(0, 13);
    if (clean.length > 9) {
      return `${clean.slice(0, 9)}-${clean.slice(9)}`;
    }
    return clean;
  }

  /**
   * Formats raw digits into standard WCO/NBR HS Code display (3901.20.00)
   */
  static formatHsCode(raw: string | number): string {
    if (!raw) return '';
    const clean = String(raw).replace(/\D/g, '').slice(0, 8);
    if (clean.length > 6) {
      return `${clean.slice(0, 4)}.${clean.slice(4, 6)}.${clean.slice(6)}`;
    } else if (clean.length > 4) {
      return `${clean.slice(0, 4)}.${clean.slice(4)}`;
    }
    return clean;
  }

  /**
   * Input event handler for auto-masking BIN field on the fly
   */
  static handleBinInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input) {
      input.value = NbrValidators.formatBin(input.value);
    }
  }

  /**
   * Input event handler for auto-masking HS Code field on the fly
   */
  static handleHsCodeInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input) {
      input.value = NbrValidators.formatHsCode(input.value);
    }
  }
}
