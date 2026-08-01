import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { Alert, AlertSchema } from './alert.schema';
import { AlertsRepository } from './alerts.repository';
import { AlertsService } from './alerts.service';
import { AlertsController } from './alerts.controller';
import { AuditLogModule } from '../audit-log/audit-log.module';

@Module({
  imports: [
    MongooseModule.forFeature([{ name: Alert.name, schema: AlertSchema }]),
    AuditLogModule,
  ],
  controllers: [AlertsController],
  providers: [AlertsRepository, AlertsService],
  exports: [AlertsRepository, AlertsService],
})
export class AlertsModule {}
