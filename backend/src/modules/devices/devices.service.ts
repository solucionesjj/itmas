import {
  Injectable,
  NotFoundException,
  UnauthorizedException,
} from '@nestjs/common';
import { randomBytes, randomUUID } from 'crypto';
import * as argon2 from 'argon2';
import { DevicesRepository } from './devices.repository';
import { DeviceDocument } from './device.schema';
import { DeviceCategory } from './device-category.enum';
import { QueryDevicesDto } from './dto/query-devices.dto';
import { DeviceResponse, toDeviceResponse } from './device-response.mapper';
import { PagedResult } from './devices.repository';

export interface ProvisionedDevice {
  deviceId: string;
  apiKey: string;
}

@Injectable()
export class DevicesService {
  constructor(private readonly devicesRepository: DevicesRepository) {}

  findById(deviceId: string): Promise<DeviceDocument | null> {
    return this.devicesRepository.findById(deviceId);
  }

  /** GET /devices (portal, all authenticated roles) — never exposes apiKeyHash. */
  async findAllForPortal(
    query: QueryDevicesDto,
  ): Promise<PagedResult<DeviceResponse>> {
    const paged = await this.devicesRepository.findPaged(
      {
        category: query.category,
        osName: query.osName,
        hostname: query.hostname,
      },
      query.page ?? 1,
      query.limit ?? 20,
    );

    return { ...paged, items: paged.items.map(toDeviceResponse) };
  }

  /**
   * Verifies a node's `<deviceId>.<secret>` API key. Looks the device up by
   * id (O(1)) then verifies the secret against its stored argon2 hash —
   * never reveals whether the failure was an unknown deviceId or a bad
   * secret, mirroring the timing-safe login rejection in AuthService.
   */
  async verifyApiKey(
    deviceId: string,
    secret: string,
  ): Promise<DeviceDocument> {
    const device = await this.devicesRepository.findById(deviceId);
    const apiKeyHash =
      device?.apiKeyHash ?? (await argon2.hash('placeholder-invalid'));
    const valid = await argon2.verify(apiKeyHash, secret).catch(() => false);

    if (!device || !valid) {
      throw new UnauthorizedException('Invalid node API key');
    }

    return device;
  }

  async touchOnIngest(
    deviceId: string,
    data: {
      hostname: string;
      category: DeviceCategory;
      os: { name: string; version: string };
    },
  ): Promise<void> {
    await this.devicesRepository.touchOnIngest(deviceId, data);
  }

  async touchLastSeen(deviceId: string): Promise<void> {
    await this.devicesRepository.touchLastSeen(deviceId);
  }

  /**
   * Used by both the `device:provision` CLI script and `POST /devices`
   * (Administrador only, ADR-0016) — the two entry points share this exact
   * logic rather than each generating their own key/hash, so the one-time-
   * reveal security property (plaintext key never persisted, never
   * retrievable again) holds identically for both.
   */
  async provision(input: {
    hostname: string;
    category: DeviceCategory;
  }): Promise<ProvisionedDevice> {
    const deviceId = randomUUID();
    const secret = randomBytes(32).toString('base64url');
    const apiKeyHash = await argon2.hash(secret);

    await this.devicesRepository.create({
      _id: deviceId,
      hostname: input.hostname,
      category: input.category,
      apiKeyHash,
    });

    return { deviceId, apiKey: `${deviceId}.${secret}` };
  }

  /**
   * Used by both the `device:rotate-key` CLI script and
   * `POST /devices/:id/rotate-key` (Administrador only, ADR-0016).
   * `NotFoundException` (rather than a plain `Error`) lets the REST layer
   * surface a real 404 — the CLI script's `catch` still handles it fine,
   * since `NotFoundException` is still an `Error`.
   */
  async rotateKey(deviceId: string): Promise<ProvisionedDevice> {
    const device = await this.devicesRepository.findById(deviceId);
    if (!device) {
      throw new NotFoundException(`No device found with id ${deviceId}`);
    }

    const secret = randomBytes(32).toString('base64url');
    const apiKeyHash = await argon2.hash(secret);
    await this.devicesRepository.setApiKeyHash(deviceId, apiKeyHash);

    return { deviceId, apiKey: `${deviceId}.${secret}` };
  }
}
