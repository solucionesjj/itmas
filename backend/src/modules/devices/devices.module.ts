import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { Device, DeviceSchema } from './device.schema';
import { DevicesRepository } from './devices.repository';
import { DevicesService } from './devices.service';
import { NodeApiKeyGuard } from './node-api-key.guard';
import { DevicesController } from './devices.controller';

@Module({
  imports: [
    MongooseModule.forFeature([{ name: Device.name, schema: DeviceSchema }]),
  ],
  controllers: [DevicesController],
  providers: [DevicesRepository, DevicesService, NodeApiKeyGuard],
  exports: [DevicesService, DevicesRepository, NodeApiKeyGuard],
})
export class DevicesModule {}
