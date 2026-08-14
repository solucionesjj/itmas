import { Injectable } from '@nestjs/common';
import { EC2Client } from '@aws-sdk/client-ec2';

/**
 * Sole seam for constructing an EC2Client (ADR-0014) — never `new EC2Client()`
 * inline elsewhere. Tests replace this provider (`overrideProvider`) with a
 * fake so nothing in CI ever makes a real AWS call. Credentials are resolved
 * by the SDK's own default provider chain (env vars or an IAM role) — this
 * factory never touches or stores a credential itself.
 */
@Injectable()
export class AwsEc2ClientFactory {
  create(region: string): EC2Client {
    return new EC2Client({ region });
  }
}
