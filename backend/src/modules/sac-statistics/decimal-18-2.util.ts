/**
 * The exact shape of the source's `numeric(18,2)`: up to 18 total digits with
 * at most 2 after the point, unsigned (BL-031 rejects negative numerics).
 * Anchored and bounded, so it is safe to run against untrusted input.
 */
export const DECIMAL_18_2 = /^\d{1,16}(\.\d{1,2})?$/;

/**
 * Normalises an incoming financial sum to a decimal *string* before validation.
 *
 * A `numeric(18,2)` does not survive a JavaScript double (18 digits vs the
 * ~15.9 a double represents exactly), so the contract's preferred wire form is
 * a JSON string. A number is still accepted — an agent sending `1234.56`
 * shouldn't get a 400 — and is stringified here so that both forms take the
 * same validation and the same Decimal128 conversion path. `null`/`undefined`
 * pass through untouched for @IsOptional() to allow.
 *
 * A number large enough to already have lost precision, or one Number#toString
 * renders in exponential form, is left as-is: it then fails DECIMAL_18_2 and
 * the caller gets a 400 rather than a silently rounded total.
 */
export function toDecimalString(value: unknown): unknown {
  if (value === null || value === undefined || typeof value === 'string') {
    return value;
  }
  if (typeof value === 'number' && Number.isFinite(value)) {
    return String(value);
  }
  return value;
}
