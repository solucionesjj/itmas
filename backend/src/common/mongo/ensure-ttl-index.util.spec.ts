import { ensureTtlIndex } from './ensure-ttl-index.util';

function mockCollection(indexesResult: unknown) {
  return {
    indexes: jest.fn().mockImplementation(() => {
      if (indexesResult instanceof Error) {
        return Promise.reject(indexesResult);
      }
      return Promise.resolve(indexesResult);
    }),
    dropIndex: jest.fn().mockResolvedValue(undefined),
    createIndex: jest.fn().mockResolvedValue(undefined),
  };
}

describe('ensureTtlIndex', () => {
  it('creates the index when the collection has no indexes yet (fresh install)', async () => {
    const collection = mockCollection([]);

    await ensureTtlIndex(collection as never, 'timestamp', 86400);

    expect(collection.dropIndex).not.toHaveBeenCalled();
    expect(collection.createIndex).toHaveBeenCalledWith(
      { timestamp: 1 },
      { expireAfterSeconds: 86400 },
    );
  });

  it('treats a "ns does not exist" error as no existing indexes, not a crash', async () => {
    const collection = mockCollection(new Error('ns does not exist'));

    await ensureTtlIndex(collection as never, 'timestamp', 86400);

    expect(collection.createIndex).toHaveBeenCalledWith(
      { timestamp: 1 },
      { expireAfterSeconds: 86400 },
    );
  });

  it('re-throws unrelated collection.indexes() errors', async () => {
    const collection = mockCollection(new Error('connection reset'));

    await expect(
      ensureTtlIndex(collection as never, 'timestamp', 86400),
    ).rejects.toThrow('connection reset');
  });

  it('does nothing when the index already exists with the same expireAfterSeconds', async () => {
    const collection = mockCollection([
      { name: 'timestamp_1', key: { timestamp: 1 }, expireAfterSeconds: 86400 },
    ]);

    await ensureTtlIndex(collection as never, 'timestamp', 86400);

    expect(collection.dropIndex).not.toHaveBeenCalled();
    expect(collection.createIndex).not.toHaveBeenCalled();
  });

  it('drops and recreates the index when expireAfterSeconds changed', async () => {
    const collection = mockCollection([
      { name: 'timestamp_1', key: { timestamp: 1 }, expireAfterSeconds: 86400 },
    ]);

    await ensureTtlIndex(collection as never, 'timestamp', 172800);

    expect(collection.dropIndex).toHaveBeenCalledWith('timestamp_1');
    expect(collection.createIndex).toHaveBeenCalledWith(
      { timestamp: 1 },
      { expireAfterSeconds: 172800 },
    );
  });

  it('ignores compound indexes that merely include the field', async () => {
    const collection = mockCollection([
      { name: 'deviceId_1_timestamp_-1', key: { deviceId: 1, timestamp: -1 } },
    ]);

    await ensureTtlIndex(collection as never, 'timestamp', 86400);

    expect(collection.dropIndex).not.toHaveBeenCalled();
    expect(collection.createIndex).toHaveBeenCalledWith(
      { timestamp: 1 },
      { expireAfterSeconds: 86400 },
    );
  });
});
