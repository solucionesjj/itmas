import { Module } from '@nestjs/common';
import { DevicesModule } from '../devices/devices.module';
import { InventoriesModule } from '../inventories/inventories.module';
import { AccessEventsModule } from '../access-events/access-events.module';
import { AlertEngineModule } from '../alert-engine/alert-engine.module';
import { IngestionController } from './ingestion.controller';
import { IngestionService } from './ingestion.service';

@Module({
  imports: [
    DevicesModule,
    InventoriesModule,
    AccessEventsModule,
    AlertEngineModule,
  ],
  controllers: [IngestionController],
  providers: [IngestionService],
})
export class IngestionModule {}
