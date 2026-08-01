import { Module } from '@nestjs/common';
import { MongooseModule } from '@nestjs/mongoose';
import { AccessEvent, AccessEventSchema } from './access-event.schema';
import { AccessEventsRepository } from './access-events.repository';

@Module({
  imports: [
    MongooseModule.forFeature([
      { name: AccessEvent.name, schema: AccessEventSchema },
    ]),
  ],
  providers: [AccessEventsRepository],
  exports: [AccessEventsRepository],
})
export class AccessEventsModule {}
