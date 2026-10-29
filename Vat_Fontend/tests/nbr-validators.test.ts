import { describe, it } from 'node:test';
import assert from 'node:assert';
import { NbrValidators } from '../src/app/shared/validators/nbr-validators.ts';

describe('NBR Statutory Validators Test Suite (জাতীয় রাজস্ব বোর্ড সংবিধিবদ্ধ ভ্যালিডেটর পরীক্ষা)', () => {
  
  describe('1. NBR 13-digit BIN Validation (বিআইএন নম্বর যাচাইকরণ)', () => {
    const binValidator = NbrValidators.bin();
    const requiredBinValidator = NbrValidators.bin(true);

    it('should pass with valid 13-digit unformatted BIN', () => {
      const result = binValidator({ value: '0019283740101' } as any);
      assert.strictEqual(result, null);
    });

    it('should pass with valid 13-digit hyphen-formatted BIN (001928374-0101)', () => {
      const result = binValidator({ value: '001928374-0101' } as any);
      assert.strictEqual(result, null);
    });

    it('should pass when value is null or empty and validator is optional', () => {
      assert.strictEqual(binValidator({ value: '' } as any), null);
      assert.strictEqual(binValidator({ value: null } as any), null);
    });

    it('should fail when value is empty and required flag is true', () => {
      const result = requiredBinValidator({ value: '' } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['required']);
    });

    it('should fail with invalid length (less than 13 digits)', () => {
      const result = binValidator({ value: '001928374' } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['invalidBin']);
      assert.strictEqual(result?.['invalidBin'].value, '001928374');
    });

    it('should fail with letters inside BIN', () => {
      const result = binValidator({ value: '001928374ABCD' } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['invalidBin']);
    });

    it('should auto-format raw 13 digits into standard NBR BIN display (001234567-0101)', () => {
      const formatted = NbrValidators.formatBin('0012345670101');
      assert.strictEqual(formatted, '001234567-0101');
    });

    it('should handle partial BIN typing smoothly without crashing', () => {
      assert.strictEqual(NbrValidators.formatBin('00123'), '00123');
      assert.strictEqual(NbrValidators.formatBin(''), '');
    });
  });

  describe('2. NBR 8-digit HS Code Validation (এইচএস কোড যাচাইকরণ)', () => {
    const hsValidator = NbrValidators.hsCode();
    const requiredHsValidator = NbrValidators.hsCode(true);

    it('should pass with valid 8-digit dotted HS Code (3901.20.00)', () => {
      const result = hsValidator({ value: '3901.20.00' } as any);
      assert.strictEqual(result, null);
    });

    it('should pass with valid 8-digit unformatted HS Code (39012000)', () => {
      const result = hsValidator({ value: '39012000' } as any);
      assert.strictEqual(result, null);
    });

    it('should fail with 6-digit HS Code (sub-heading only)', () => {
      const result = hsValidator({ value: '3901.20' } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['invalidHsCode']);
    });

    it('should fail with non-numeric characters', () => {
      const result = hsValidator({ value: '3901.XX.00' } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['invalidHsCode']);
    });

    it('should fail when empty and required', () => {
      const result = requiredHsValidator({ value: '' } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['required']);
    });

    it('should auto-format raw 8 digits into standard WCO/NBR HS Code display (3901.20.00)', () => {
      const formatted = NbrValidators.formatHsCode('39012000');
      assert.strictEqual(formatted, '3901.20.00');
    });
  });

  describe('3. Section 46(1)(e) Banking Channel Compliance (ধারা ৪৬(১)(ঙ) ব্যাংকিং পরিশোধ বাধ্যবাধকতা)', () => {
    it('should pass when invoice total is under threshold (< 1,00,000 BDT) even if bank ref is empty', () => {
      let currentTotal = 45000;
      const validator = NbrValidators.section46Banking(() => currentTotal, 100000);
      const result = validator({ value: '' } as any);
      assert.strictEqual(result, null);
    });

    it('should fail when invoice total is >= 1,00,000 BDT and bank ref is missing', () => {
      let currentTotal = 150000;
      const validator = NbrValidators.section46Banking(() => currentTotal, 100000);
      const result = validator({ value: '' } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['section46BankingRequired']);
      assert.strictEqual(result?.['section46BankingRequired'].threshold, 100000);
      assert.strictEqual(result?.['section46BankingRequired'].currentTotal, 150000);
    });

    it('should pass when invoice total is >= 1,00,000 BDT and bank ref is provided', () => {
      let currentTotal = 150000;
      const validator = NbrValidators.section46Banking(() => currentTotal, 100000);
      const result = validator({ value: 'BEFTN-BRAC-20261029-0081' } as any);
      assert.strictEqual(result, null);
    });

    it('should support custom threshold (e.g. 2,00,000 BDT for Section 51 Mushak 6.10)', () => {
      let currentTotal = 150000;
      const validator200k = NbrValidators.section46Banking(() => currentTotal, 200000);
      assert.strictEqual(validator200k({ value: '' } as any), null);

      currentTotal = 250000;
      assert.notStrictEqual(validator200k({ value: '' } as any), null);
      assert.strictEqual(validator200k({ value: 'RTGS-EBL-001' } as any), null);
    });
  });

  describe('4. Mushak 6.3 Vehicle Tracking Validation (বিধি ৪০(১) যানবাহন বিবরণ)', () => {
    const vehicleValidator = NbrValidators.vehicleNo(true);

    it('should pass with valid vehicle description and license number', () => {
      const result = vehicleValidator({ value: 'Covered Van (Dhaka Metro-U-14-8921)' } as any);
      assert.strictEqual(result, null);
    });

    it('should fail when empty', () => {
      const result = vehicleValidator({ value: '' } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['required']);
    });

    it('should fail when too short (< 4 chars)', () => {
      const result = vehicleValidator({ value: 'DM' } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['invalidVehicle']);
    });
  });

  describe('5. Positive Number & Monetary Rate Validation', () => {
    const qtyValidator = NbrValidators.positiveNumber(1);
    const rateValidator = NbrValidators.positiveNumber(0, true);

    it('should pass for positive integers >= 1', () => {
      assert.strictEqual(qtyValidator({ value: 50 } as any), null);
      assert.strictEqual(qtyValidator({ value: 1 } as any), null);
    });

    it('should fail for quantity < 1', () => {
      const result = qtyValidator({ value: 0 } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['minPositive']);
    });

    it('should fail for negative values', () => {
      const result = qtyValidator({ value: -10 } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['minPositive']);
    });

    it('should pass for zero rate when allowZero is true', () => {
      assert.strictEqual(rateValidator({ value: 0 } as any), null);
      assert.strictEqual(rateValidator({ value: 140.50 } as any), null);
    });

    it('should fail for non-numeric text', () => {
      const result = qtyValidator({ value: 'abc' } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['notANumber']);
    });
  });

  describe('6. Challan / Invoice Reference Validation', () => {
    const challanValidator = NbrValidators.challanNumber(true);

    it('should pass with standard statutory challan string', () => {
      assert.strictEqual(challanValidator({ value: 'BEN-INV-8911' } as any), null);
      assert.strictEqual(challanValidator({ value: 'CH-DHK-2026-00142' } as any), null);
    });

    it('should fail when empty', () => {
      const result = challanValidator({ value: '' } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['required']);
    });

    it('should fail when too short (< 3 chars)', () => {
      const result = challanValidator({ value: 'AB' } as any);
      assert.notStrictEqual(result, null);
      assert.ok(result?.['invalidChallan']);
    });
  });
});
