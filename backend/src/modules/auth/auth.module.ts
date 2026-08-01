import { Module } from '@nestjs/common';
import { JwtModule } from '@nestjs/jwt';
import { PassportModule } from '@nestjs/passport';
import { AuthController } from './auth.controller';
import { AuthService } from './auth.service';
import { JwtStrategy } from './jwt.strategy';
import { UsersModule } from '../users/users.module';
import { AuditLogModule } from '../audit-log/audit-log.module';

// NOTE: `@nestjs/throttler`'s ThrottlerModule is `@Global()` internally — a
// SECOND `ThrottlerModule.forRootAsync()` registration here (as this module
// used to have) collides with AppModule's global one on the same DI tokens
// (THROTTLER_OPTIONS/storage), silently discarding whichever registers second.
// The login-specific brute-force limit is applied via `@Throttle()` directly
// on AuthController's login route instead — see auth.controller.ts.
@Module({
  imports: [
    UsersModule,
    AuditLogModule,
    PassportModule,
    JwtModule.register({}),
  ],
  controllers: [AuthController],
  providers: [AuthService, JwtStrategy],
  exports: [AuthService],
})
export class AuthModule {}
