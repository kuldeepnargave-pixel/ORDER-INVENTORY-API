const CHECKOUT_STORAGE_KEY = 'oi_checkout';

interface StoredCheckout {
  fingerprint: string;
  key: string;
}

function createKey(): string {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }
  return `checkout-${Date.now()}-${Math.random().toString(36).slice(2, 12)}`;
}

/**
 * Returns the idempotency key for the current checkout attempt.
 * The same key is reused when the user retries the SAME cart (e.g. after a
 * 409/500 or a page reload on the checkout screen). If the cart changed, a
 * fresh key is generated so the backend never sees a payload mismatch.
 */
export function getCheckoutKey(cartFingerprint: string): string {
  const raw = sessionStorage.getItem(CHECKOUT_STORAGE_KEY);
  if (raw) {
    try {
      const stored = JSON.parse(raw) as StoredCheckout;
      if (stored.fingerprint === cartFingerprint && stored.key) {
        return stored.key;
      }
    } catch {
      // Corrupt entry - fall through and regenerate.
    }
  }

  const key = createKey();
  sessionStorage.setItem(CHECKOUT_STORAGE_KEY, JSON.stringify({ fingerprint: cartFingerprint, key }));
  return key;
}

/** Clear the stored key after a successful (or abandoned) checkout. */
export function clearCheckoutKey(): void {
  sessionStorage.removeItem(CHECKOUT_STORAGE_KEY);
}
