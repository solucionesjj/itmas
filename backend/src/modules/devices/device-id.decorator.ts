import { createParamDecorator, ExecutionContext } from '@nestjs/common';
import { Request } from 'express';

export const DeviceId = createParamDecorator(
  (_data: unknown, ctx: ExecutionContext): string => {
    const request = ctx
      .switchToHttp()
      .getRequest<Request & { deviceId: string }>();
    return request.deviceId;
  },
);
