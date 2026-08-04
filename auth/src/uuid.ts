/**
 * A UUIDv7 generator, so identities Better Auth mints sort by creation time and
 * land in `public.users` as real Postgres `uuid` values — the same shape and the
 * same index locality the .NET side gets from `Guid.CreateVersion7()`.
 *
 * Layout per RFC 9562: 48 bits of Unix milliseconds, 4 bits of version, 12 bits
 * random, 2 bits of variant, 62 bits random.
 */
export function uuidv7(): string {
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);

  const milliseconds = Date.now();
  bytes[0] = Math.floor(milliseconds / 2 ** 40) & 0xff;
  bytes[1] = Math.floor(milliseconds / 2 ** 32) & 0xff;
  bytes[2] = Math.floor(milliseconds / 2 ** 24) & 0xff;
  bytes[3] = Math.floor(milliseconds / 2 ** 16) & 0xff;
  bytes[4] = Math.floor(milliseconds / 2 ** 8) & 0xff;
  bytes[5] = milliseconds & 0xff;

  bytes[6] = 0x70 | (bytes[6]! & 0x0f); // version 7
  bytes[8] = 0x80 | (bytes[8]! & 0x3f); // variant 10

  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');

  return [
    hex.slice(0, 8),
    hex.slice(8, 12),
    hex.slice(12, 16),
    hex.slice(16, 20),
    hex.slice(20),
  ].join('-');
}
