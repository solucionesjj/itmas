import { UnauthorizedException } from '@nestjs/common';
import { DevicesService } from './devices.service';
import { DeviceCategory } from './device-category.enum';

jest.mock('argon2', () => ({
  hash: jest.fn((value: string) => Promise.resolve(`hashed:${value}`)),
  verify: jest.fn((hash: string, secret: string) =>
    Promise.resolve(hash === `hashed:${secret}`),
  ),
}));

describe('DevicesService.verifyApiKey', () => {
  function makeDevice(overrides: Record<string, unknown> = {}) {
    return {
      _id: 'device-1',
      apiKeyHash: 'hashed:correct-secret',
      ...overrides,
    };
  }

  it('resolves the device when the secret matches its stored hash', async () => {
    const findById = jest.fn().mockResolvedValue(makeDevice());
    const service = new DevicesService({ findById } as never);

    const device = await service.verifyApiKey('device-1', 'correct-secret');

    expect(device._id).toBe('device-1');
  });

  it('rejects with the same generic error for an unknown deviceId as for a wrong secret', async () => {
    const findById = jest.fn().mockResolvedValue(null);
    const service = new DevicesService({ findById } as never);

    await expect(
      service.verifyApiKey('unknown-device', 'anything'),
    ).rejects.toThrow(UnauthorizedException);
  });

  it('rejects a wrong secret for a real device', async () => {
    const findById = jest.fn().mockResolvedValue(makeDevice());
    const service = new DevicesService({ findById } as never);

    await expect(
      service.verifyApiKey('device-1', 'wrong-secret'),
    ).rejects.toThrow(UnauthorizedException);
  });
});

describe('DevicesService.provision / rotateKey', () => {
  it('provisions a device with a hashed key and returns the plaintext key once', async () => {
    const create = jest.fn().mockResolvedValue(undefined);
    const service = new DevicesService({ create } as never);

    const result = await service.provision({
      hostname: 'PC-001',
      category: DeviceCategory.COLLABORATOR,
    });

    expect(result.apiKey).toContain(result.deviceId);

    expect(create).toHaveBeenCalledWith(
      expect.objectContaining({
        hostname: 'PC-001',
        category: DeviceCategory.COLLABORATOR,
        // eslint-disable-next-line @typescript-eslint/no-unsafe-assignment -- expect.stringMatching()'s return type is loosely typed by Jest, not a real safety issue.
        apiKeyHash: expect.stringMatching(/^hashed:/),
      }),
    );
  });

  it('rotates an existing device key without touching hostname/category', async () => {
    const findById = jest.fn().mockResolvedValue({ _id: 'device-1' });
    const setApiKeyHash = jest.fn().mockResolvedValue(undefined);
    const service = new DevicesService({ findById, setApiKeyHash } as never);

    const result = await service.rotateKey('device-1');

    expect(result.deviceId).toBe('device-1');
    expect(setApiKeyHash).toHaveBeenCalledWith(
      'device-1',
      expect.stringMatching(/^hashed:/),
    );
  });

  it('throws when rotating a key for a device that does not exist', async () => {
    const findById = jest.fn().mockResolvedValue(null);
    const service = new DevicesService({ findById } as never);

    await expect(service.rotateKey('missing-device')).rejects.toThrow();
  });
});

describe('DevicesService.findAllForPortal', () => {
  function makeDevice(overrides: Record<string, unknown> = {}) {
    return {
      _id: 'device-1',
      hostname: 'PC-001',
      category: DeviceCategory.COLLABORATOR,
      os: { name: 'Windows', version: '11' },
      lastSeen: new Date('2026-01-01T00:00:00.000Z'),
      apiKeyHash: 'super-secret-hash',
      ...overrides,
    };
  }

  it('strips apiKeyHash from every returned item and forwards filters/pagination', async () => {
    const findPaged = jest.fn().mockResolvedValue({
      items: [makeDevice()],
      total: 1,
      page: 2,
      limit: 10,
    });
    const service = new DevicesService({ findPaged } as never);

    const result = await service.findAllForPortal({
      category: DeviceCategory.COLLABORATOR,
      osName: 'Win',
      hostname: 'PC',
      page: 2,
      limit: 10,
    });

    expect(findPaged).toHaveBeenCalledWith(
      { category: DeviceCategory.COLLABORATOR, osName: 'Win', hostname: 'PC' },
      2,
      10,
    );
    expect(result.items[0]).not.toHaveProperty('apiKeyHash');
    expect(result.items[0]).toMatchObject({
      _id: 'device-1',
      hostname: 'PC-001',
      category: DeviceCategory.COLLABORATOR,
    });
    expect(result.total).toBe(1);
  });

  it('defaults to page 1 / limit 20 when not supplied', async () => {
    const findPaged = jest
      .fn()
      .mockResolvedValue({ items: [], total: 0, page: 1, limit: 20 });
    const service = new DevicesService({ findPaged } as never);

    await service.findAllForPortal({});

    expect(findPaged).toHaveBeenCalledWith(
      { category: undefined, osName: undefined, hostname: undefined },
      1,
      20,
    );
  });
});
