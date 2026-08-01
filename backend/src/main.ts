import { writeFileSync } from 'fs';
import { join } from 'path';
import { NestFactory } from '@nestjs/core';
import { ValidationPipe } from '@nestjs/common';
import { DocumentBuilder, SwaggerModule } from '@nestjs/swagger';
import { json, urlencoded } from 'express';
import helmet from 'helmet';
import { AppModule } from './app.module';
import { AllExceptionsFilter } from './common/filters/all-exceptions.filter';
import { JsonLoggerService } from './common/logger/json-logger.service';

// Explicit, documented body-size cap on the public ingestion API (agent.md
// §6: an unbounded request body is a real DoS surface). No inventory/event
// payload plausibly needs anywhere near this; disabling Nest's built-in
// body parser (`bodyParser: false`) so this explicit limit is the only one
// in effect, rather than layering on top of Nest's own 100kb Express default.
const MAX_REQUEST_BODY_SIZE = '1mb';

async function bootstrap() {
  const app = await NestFactory.create(AppModule, {
    bufferLogs: true,
    bodyParser: false,
  });
  app.use(json({ limit: MAX_REQUEST_BODY_SIZE }));
  app.use(urlencoded({ extended: true, limit: MAX_REQUEST_BODY_SIZE }));

  const logger = await app.resolve(JsonLoggerService);
  logger.setContext('Bootstrap');
  app.useLogger(logger);

  app.use(helmet());
  app.setGlobalPrefix('api/v1');
  app.useGlobalPipes(
    new ValidationPipe({
      whitelist: true,
      forbidNonWhitelisted: true,
      transform: true,
    }),
  );
  // Resolve a separate transient logger instance for the filter — reusing
  // `logger` here would let AllExceptionsFilter's setContext() mutate the
  // app-wide bootstrap logger's context for every subsequent framework log line.
  const filterLogger = await app.resolve(JsonLoggerService);
  app.useGlobalFilters(new AllExceptionsFilter(filterLogger));

  // OpenAPI/Swagger (agent.md §11 Quality Gate: "documentación OpenAPI/Swagger
  // completa"). SwaggerModule.setup()'s path is NOT affected by
  // setGlobalPrefix — it renders exactly at the literal path given, so this
  // serves at /api/docs (not /api/v1/api/docs).
  const swaggerConfig = new DocumentBuilder()
    .setTitle('IT-MAS API')
    .setDescription(
      'Management and Audit System — inventory ingestion, alerting, and portal query API.',
    )
    .setVersion('1.0')
    .addBearerAuth(
      { type: 'http', scheme: 'bearer', bearerFormat: 'JWT' },
      'jwt',
    )
    .addApiKey(
      { type: 'apiKey', name: 'X-Node-Api-Key', in: 'header' },
      'node-api-key',
    )
    .build();
  const swaggerDocument = SwaggerModule.createDocument(app, swaggerConfig);
  SwaggerModule.setup('api/docs', app, swaggerDocument);
  // Static artifact for the "documentación OpenAPI/Swagger completa" gate —
  // regenerated on every boot so it never drifts from the live controllers.
  // Best-effort: a read-only container filesystem shouldn't crash the boot
  // over this, it's a convenience artifact, not a runtime requirement.
  try {
    writeFileSync(
      join(__dirname, '..', 'openapi.json'),
      JSON.stringify(swaggerDocument, null, 2),
    );
  } catch (error) {
    logger.warn('Could not write openapi.json (non-fatal)', {
      error: error instanceof Error ? error.message : String(error),
    });
  }

  const port = process.env.PORT ?? 3000;
  await app.listen(port);
  logger.log(`IT-MAS API listening on port ${port}`);
}
void bootstrap();
