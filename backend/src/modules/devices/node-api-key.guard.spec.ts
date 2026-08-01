import { ExecutionContext, UnauthorizedException } from '@nestjs/common';
import { NodeApiKeyGuard } from './node-api-key.guard';
import { DevicesService } from './devices.service';

function buildContext(header?: string): {
  context: ExecutionContext;
  request: { header: jest.Mock; deviceId?: string };
} {
  const request = {
    header: jest.fn().mockReturnValue(header),
  } as { header: jest.Mock; deviceId?: string };

  const context = {
    switchToHttp: () => ({
      getRequest: () => request,
    }),
  } as unknown as ExecutionContext;

  return { context, request };
}

describe('NodeApiKeyGuard', () => {
  let devicesService: { verifyApiKey: jest.Mock };
  let guard: NodeApiKeyGuard;

  beforeEach(() => {
    devicesService = { verifyApiKey: jest.fn() };
    guard = new NodeApiKeyGuard(devicesService as unknown as DevicesService);
  });

  it('rejects when the header is missing', async () => {
    const { context } = buildContext(undefined);
    await expect(guard.canActivate(context)).rejects.toThrow(
      UnauthorizedException,
    );
    expect(devicesService.verifyApiKey).not.toHaveBeenCalled();
  });

  it('rejects a malformed key with no separator', async () => {
    const { context } = buildContext('not-a-valid-key');
    await expect(guard.canActivate(context)).rejects.toThrow(
      UnauthorizedException,
    );
    expect(devicesService.verifyApiKey).not.toHaveBeenCalled();
  });

  it('rejects a key with an empty deviceId or secret half', async () => {
    const { context: emptyDeviceId } = buildContext('.secret-only');
    await expect(guard.canActivate(emptyDeviceId)).rejects.toThrow(
      UnauthorizedException,
    );

    const { context: emptySecret } = buildContext('device-id-only.');
    await expect(guard.canActivate(emptySecret)).rejects.toThrow(
      UnauthorizedException,
    );
    expect(devicesService.verifyApiKey).not.toHaveBeenCalled();
  });

  it('rejects when the device service throws (unknown device or bad secret)', async () => {
    devicesService.verifyApiKey.mockRejectedValue(
      new UnauthorizedException('Invalid node API key'),
    );
    const { context } = buildContext('device-1.some-secret');

    await expect(guard.canActivate(context)).rejects.toThrow(
      UnauthorizedException,
    );
    expect(devicesService.verifyApiKey).toHaveBeenCalledWith(
      'device-1',
      'some-secret',
    );
  });

  it('attaches deviceId to the request and allows the call through on success', async () => {
    devicesService.verifyApiKey.mockResolvedValue({ _id: 'device-1' });
    const { context, request } = buildContext('device-1.some-secret');

    const result = await guard.canActivate(context);

    expect(result).toBe(true);
    expect(request.deviceId).toBe('device-1');
  });

  it('splits only on the first dot, allowing dots inside the secret', async () => {
    devicesService.verifyApiKey.mockResolvedValue({ _id: 'device-1' });
    const { context } = buildContext('device-1.secret.with.dots');

    await guard.canActivate(context);

    expect(devicesService.verifyApiKey).toHaveBeenCalledWith(
      'device-1',
      'secret.with.dots',
    );
  });
});
