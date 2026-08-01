import { Controller, Get, ServiceUnavailableException } from '@nestjs/common';
import { InjectConnection } from '@nestjs/mongoose';
import { SkipThrottle } from '@nestjs/throttler';
import { ApiTags } from '@nestjs/swagger';
import { Connection, ConnectionStates } from 'mongoose';

@ApiTags('health')
@Controller('health')
@SkipThrottle({ login: true })
export class HealthController {
  constructor(@InjectConnection() private readonly connection: Connection) {}

  @Get()
  check() {
    const mongoConnected =
      this.connection.readyState === ConnectionStates.connected;
    if (!mongoConnected) {
      throw new ServiceUnavailableException('Database connection is not ready');
    }
    return { status: 'ok', mongo: 'connected' };
  }
}
