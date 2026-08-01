import { createParamDecorator, ExecutionContext } from '@nestjs/common';
import { Request } from 'express';
import { DeviceCategory } from './device-category.enum';

export const DeviceCategoryParam = createParamDecorator(
  (_data: unknown, ctx: ExecutionContext): DeviceCategory | undefined => {
    const request = ctx
      .switchToHttp()
      .getRequest<Request & { deviceCategory?: DeviceCategory }>();
    return request.deviceCategory;
  },
);
