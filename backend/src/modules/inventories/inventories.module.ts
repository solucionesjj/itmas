import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { Inventory, InventorySchema } from './inventory.schema';
import { InventoriesRepository } from './inventories.repository';
import { InventoryDiffService } from './inventory-diff.service';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: Inventory.name, schema: InventorySchema },
    ]),
  ],
  providers: [InventoriesRepository, InventoryDiffService],
  exports: [InventoriesRepository, InventoryDiffService],
})
export class InventoriesModule {}
