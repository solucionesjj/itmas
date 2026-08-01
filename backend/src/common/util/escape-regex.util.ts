/**
 * Escapes regex metacharacters so user-supplied search input can be used
 * safely inside a Mongo `$regex` filter — never interpolate raw client input
 * into a RegExp construction (ReDoS / NoSQL-injection-adjacent risk).
 */
export function escapeRegex(input: string): string {
  return input.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
