/** Minimal `--flag value` parser for the device provisioning/rotation CLI scripts. */
export function parseArgs(argv: string[]): Record<string, string> {
  const result: Record<string, string> = {};
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg.startsWith('--')) {
      const key = arg.slice(2);
      const value = argv[i + 1];
      result[key] = value;
      i++;
    }
  }
  return result;
}
