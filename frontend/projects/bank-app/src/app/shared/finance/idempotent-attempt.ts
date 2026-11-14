/**
 * Hands out the Idempotency-Key for a money movement. A retry of the same request keeps its key, so a response
 * lost on the way back cannot move the money twice. A changed request is a different movement and gets a new key,
 * because the server refuses a key that comes back with other details.
 */
export class IdempotentAttempt<TRequest> {
  private current: { request: string; key: string } | null = null;

  constructor(private readonly newKey: () => string = () => crypto.randomUUID()) {}

  keyFor(request: TRequest): string {
    const fingerprint = JSON.stringify(request);
    if (this.current?.request !== fingerprint) this.current = { request: fingerprint, key: this.newKey() };
    return this.current.key;
  }

  /** The movement went through; the next one is new even if it looks identical. */
  finish(): void {
    this.current = null;
  }
}
