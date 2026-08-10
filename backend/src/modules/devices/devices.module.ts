import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { Device, DeviceSchema } from './device.schema';
import { DevicesRepository } from './devices.repository';
import { DevicesService } from './devices.service';
import { NodeApiKeyGuard } from './node-api-key.guard';
import { DevicesController } from './devices.controller';
import { AuditLogModule } from '../audit-log/audit-log.module';

@Module({
  imports: [
    MongooseModule.forFeature([{ name: Device.name, schema: DeviceSchema }]),
    // ADR-0016: POST /devices and POST /devices/:id/rotate-key record an
    // audit log entry, same convention as users/alert-rules.
    AuditLogModule,
  ],
  controllers: [DevicesController],
  providers: [DevicesRepository, DevicesService, NodeApiKeyGuard],
  exports: [DevicesService, DevicesRepository, NodeApiKeyGuard],
})
export class DevicesModule {}
