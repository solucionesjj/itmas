/**
 * Lightweight load smoke-check for the NFR "ingesta < 500ms" (agent.md §14).
 * Fires N concurrent POST /inventory requests against an already-running
 * instance and reports p50/p95/p99 latency — a soft benchmark to eyeball,
 * not a CI gate (shared/sandboxed hardware timing is inherently noisy).
 *
 * Usage: provision a device first (npm run device:provision), then:
 *   API_BASE=http://localhost:3000/api/v1 NODE_API_KEY=<deviceId>.<secret> \
 *     npx ts-node -r tsconfig-paths/register scripts/load-smoke.ts [concurrency]
 */

const API_BASE = process.env.API_BASE ?? 'http://localhost:3000/api/v1';
const NODE_API_KEY = process.env.NODE_API_KEY;
const CONCURRENCY = parseInt(process.argv[2] ?? '50', 10);

if (!NODE_API_KEY) {
  console.error(
    'Set NODE_API_KEY=<deviceId>.<secret> (see npm run device:provision).',
  );
  process.exit(1);
}

function samplePayload(i: number) {
  return {
    hostname: `load-smoke-${i}`,
    category: 'collaborator',
    os: { name: 'Linux', version: '6.0' },
    cpu: { model: 'Test CPU', cores: 4 },
    ram: { totalGB: 16 },
    disks: [{ name: 'sda', sizeGB: 256 }],
    timestamp: new Date(Date.now() - i).toISOString(),
  };
}

async function timedRequest(i: number): Promise<number> {
  const start = performance.now();
  await fetch(`${API_BASE}/inventory`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Node-Api-Key': NODE_API_KEY as string,
    },
    body: JSON.stringify(samplePayload(i)),
  });
  return performance.now() - start;
}

function percentile(sorted: number[], p: number): number {
  const index = Math.min(
    sorted.length - 1,
    Math.floor((p / 100) * sorted.length),
  );
  return sorted[index];
}

async function main() {
  console.log(
    `Firing ${CONCURRENCY} concurrent POST /inventory requests at ${API_BASE}...`,
  );
  const durations = await Promise.all(
    Array.from({ length: CONCURRENCY }, (_, i) => timedRequest(i)),
  );
  const sorted = [...durations].sort((a, b) => a - b);

  console.log(`p50: ${percentile(sorted, 50).toFixed(1)}ms`);
  console.log(`p95: ${percentile(sorted, 95).toFixed(1)}ms`);
  console.log(`p99: ${percentile(sorted, 99).toFixed(1)}ms`);
  console.log(`max: ${sorted[sorted.length - 1].toFixed(1)}ms`);
  console.log(
    'NFR target: p95 < 500ms (agent.md §14) — this is a soft benchmark, not a hard gate.',
  );
}

void main();
