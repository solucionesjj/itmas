import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { AppModule } from '../src/app.module';
import { DevicesService } from '../src/modules/devices/devices.service';
import { parseArgs } from './parse-args';

/**
 * Node/device key rotation is intentionally NOT a REST endpoint — no such
 * endpoint exists in the authoritative API contract. Run out-of-band:
 *   npm run device:rotate-key -- --device-id <id>
 */
async function main() {
  const args = parseArgs(process.argv.slice(2));
  const deviceId = args['device-id'];

  if (!deviceId) {
    console.error('Usage: npm run device:rotate-key -- --device-id <id>');
    process.exit(1);
  }

  const app = await NestFactory.createApplicationContext(AppModule, {
    logger: false,
  });

  try {
    const devicesService = app.get(DevicesService);
    const { apiKey } = await devicesService.rotateKey(deviceId);

    console.log(
      'Device key rotated. Store this API key now — it will not be shown again:',
    );
    console.log(`  deviceId: ${deviceId}`);
    console.log(`  apiKey:   ${apiKey}`);
    console.log('The previous key for this device no longer works.');
  } finally {
    await app.close();
  }
}

main().catch((error: unknown) => {
  console.error(error);
  process.exit(1);
});
