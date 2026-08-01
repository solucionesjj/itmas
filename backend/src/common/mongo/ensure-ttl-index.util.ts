import { Collection } from 'mongoose';

interface IndexDescription {
  name?: string;
  key: Record<string, number>;
  expireAfterSeconds?: number;
}

/**
 * Creates (or recreates) a TTL index on `field`, purging documents older than
 * `expireAfterSeconds`. Mongo errors if you try to create an index with the
 * same keys but a different `expireAfterSeconds` than one that already
 * exists — so on every boot we check the existing index's current value and
 * drop+recreate only when the configured retention actually changed, letting
 * an operator adjust the *_RETENTION_DAYS env vars across deploys without the
 * next boot crashing on an index conflict.
 */
export async function ensureTtlIndex(
  collection: Collection,
  field: string,
  expireAfterSeconds: number,
): Promise<void> {
  // `collection.indexes()` throws ("ns does not exist") on a brand-new
  // database before the collection has ever been written to — a fresh
  // install, or every test suite's fresh mongodb-memory-server. Treat that
  // as "no existing index yet" rather than letting boot crash on it.
  const existing = (await collection.indexes().catch((error: unknown) => {
    const message = error instanceof Error ? error.message : String(error);
    if (message.includes('ns does not exist')) {
      return [];
    }
    throw error;
  })) as IndexDescription[];
  const match = existing.find(
    (index) => Object.keys(index.key).length === 1 && field in index.key,
  );

  if (match && match.expireAfterSeconds === expireAfterSeconds) {
    return;
  }

  if (match) {
    await collection.dropIndex(match.name as string);
  }

  await collection.createIndex({ [field]: 1 }, { expireAfterSeconds });
}
