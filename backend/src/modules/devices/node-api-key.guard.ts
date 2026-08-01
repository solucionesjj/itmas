import {
  CanActivate,
  ExecutionContext,
  Injectable,
  UnauthorizedException,
} from '@nestjs/common';
import { Request } from 'express';
import { DevicesService } from './devices.service';
import { DeviceCategory } from './device-category.enum';

export const NODE_API_KEY_HEADER = 'x-node-api-key';

declare module 'express-serve-static-core' {
  interface Request {
    deviceId?: string;
    deviceCategory?: DeviceCategory;
  }
}

/**
 * Node/device authentication — entirely separate from JwtAuthGuard/RolesGuard
 * (agent.md §5.4: dual auth, never mixed on one endpoint). Key format is
 * `<deviceId>.<secret>`: the deviceId half gives an O(1) lookup, the secret
 * half is verified against the device's stored argon2 hash.
 */
@Injectable()
export class NodeApiKeyGuard implements CanActivate {
  constructor(private readonly devicesService: DevicesService) {}

  async canActivate(context: ExecutionContext): Promise<boolean> {
    const request = context.switchToHttp().getRequest<Request>();
    const header = request.header(NODE_API_KEY_HEADER);

    if (!header) {
      throw new UnauthorizedException('Missing node API key');
    }

    const separatorIndex = header.indexOf('.');
    if (separatorIndex <= 0 || separatorIndex === header.length - 1) {
      throw new UnauthorizedException('Malformed node API key');
    }

    const deviceId = header.slice(0, separatorIndex);
    const secret = header.slice(separatorIndex + 1);

    const device = await this.devicesService.verifyApiKey(deviceId, secret);
    request.deviceId = device._id;
    // Already fetched by verifyApiKey — avoids a second lookup for callers
    // (e.g. the alert engine's off-hours check) that need the category too.
    request.deviceCategory = device.category;
    return true;
  }
}
