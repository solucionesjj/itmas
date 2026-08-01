import { MiddlewareConsumer, Module, NestModule } from '@nestjs/common';
import { APP_GUARD } from '@nestjs/core';
import { ConfigModule, ConfigService } from '@nestjs/config';
import { MongooseModule } from '@nestjs/mongoose';
import { ThrottlerGuard, ThrottlerModule } from '@nestjs/throttler';
import configuration from './config/configuration';
import { validateEnv } from './config/env.validation';
import { CommonModule } from './common/common.module';
import { RequestIdMiddleware } from './common/middleware/request-id.middleware';
import { UsersModule } from './modules/users/users.module';
import { AuditLogModule } from './modules/audit-log/audit-log.module';
import { AuthModule } from './modules/auth/auth.module';
import { HealthModule } from './modules/health/health.module';
import { DevicesModule } from './modules/devices/devices.module';
import { InventoriesModule } from './modules/inventories/inventories.module';
import { AccessEventsModule } from './modules/access-events/access-events.module';
import { IngestionModule } from './modules/ingestion/ingestion.module';
import { AlertRulesModule } from './modules/alert-rules/alert-rules.module';
import { AlertsModule } from './modules/alerts/alerts.module';
import { AlertEngineModule } from './modules/alert-engine/alert-engine.module';
import { StatsModule } from './modules/stats/stats.module';
import { ReportsModule } from './modules/reports/reports.module';

@Module({
  imports: [
    ConfigModule.forRoot({
      isGlobal: true,
      load: [configuration],
      validate: validateEnv,
    }),
    MongooseModule.forRootAsync({
      imports: [ConfigModule],
      useFactory: () => ({ uri: process.env.MONGO_URI }),
    }),
    // Rate limiting (agent.md §6.8) — the ONLY ThrottlerModule registration in
    // the app. `@nestjs/throttler`'s ThrottlerModule is internally `@Global()`,
    // so a second forRootAsync() call anywhere else (this used to live split
    // across AuthModule too) collides on the same DI tokens and one silently
    // clobbers the other — do not add another registration elsewhere.
    // Two named profiles, BOTH applied to every route by default via the
    // global APP_GUARD below: 'default' is the generous API-wide limit;
    // 'login' is the strict brute-force limit, meant only for POST
    // /auth/login — every other route opts out of 'login' via
    // @SkipThrottle({ login: true }) (see auth.controller.ts and every other
    // controller) and falls back to just 'default'.
    ThrottlerModule.forRootAsync({
      imports: [ConfigModule],
      inject: [ConfigService],
      useFactory: (configService: ConfigService) => [
        {
          name: 'default',
          ttl:
            configService.getOrThrow<number>('apiRateLimit.windowSec') * 1000,
          limit: configService.getOrThrow<number>('apiRateLimit.max'),
        },
        {
          name: 'login',
          ttl:
            configService.getOrThrow<number>('loginRateLimit.windowSec') * 1000,
          limit: configService.getOrThrow<number>('loginRateLimit.max'),
        },
      ],
    }),
    CommonModule,
    UsersModule,
    AuditLogModule,
    AuthModule,
    HealthModule,
    DevicesModule,
    InventoriesModule,
    AccessEventsModule,
    IngestionModule,
    AlertRulesModule,
    AlertsModule,
    AlertEngineModule,
    StatsModule,
    ReportsModule,
  ],
  providers: [{ provide: APP_GUARD, useClass: ThrottlerGuard }],
})
export class AppModule implements NestModule {
  configure(consumer: MiddlewareConsumer) {
    consumer.apply(RequestIdMiddleware).forRoutes('*');
  }
}
