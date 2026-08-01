import 'reflect-metadata';
import { NestFactory } from '@nestjs/core';
import { AppModule } from '../src/app.module';
import { DevicesService } from '../src/modules/devices/devices.service';
import { DeviceCategory } from '../src/modules/devices/device-category.enum';
import { parseArgs } from './parse-args';

/**
 * Node/device provisioning is intentionally NOT a REST endpoint — no such
 * endpoint exists in the authoritative API contract. Run out-of-band:
 *   npm run device:provision -- --hostname PC-001 --category collaborator
 */
async function main() {
  const args = parseArgs(process.argv.slice(2));
  const hostname = args.hostname;
  const category = args.category as DeviceCategory;

  if (!hostname || !Object.values(DeviceCategory).includes(category)) {
    console.error(
      `Usage: npm run device:provision -- --hostname <name> --category <${Object.values(DeviceCategory).join('|')}>`,
    );
    process.exit(1);
  }

  const app = await NestFactory.createApplicationContext(AppModule, {
    logger: false,
  });

  try {
    const devicesService = app.get(DevicesService);
    const { deviceId, apiKey } = await devicesService.provision({
      hostname,
      category,
    });

    console.log(
      'Device provisioned. Store this API key now — it will not be shown again:',
    );
    console.log(`  deviceId: ${deviceId}`);
    console.log(`  apiKey:   ${apiKey}`);
    console.log('Configure the node to send it as the X-Node-Api-Key header.');
  } finally {
    await app.close();
  }
}

main().catch((error: unknown) => {
  console.error(error);
  process.exit(1);
});
