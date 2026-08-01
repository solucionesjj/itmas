import { AlertEngineService } from './alert-engine.service';
import { AlertRuleType } from '../alert-rules/alert-rule-type.enum';
import { AlertStatus } from '../alerts/alert-status.enum';
import { DeviceCategory } from '../devices/device-category.enum';
import { AccessEventLevel } from '../access-events/access-event-level.enum';
import { AccessEventAction } from '../access-events/access-event-action.enum';

interface CreatedAlert {
  type: AlertRuleType;
  deviceId: string;
  status: AlertStatus;
  detail: Record<string, unknown>;
}

function buildService() {
  const alertRulesRepository = {
    findEnabledByType: jest.fn(),
  };
  const alertsRepository = {
    create: jest.fn<Promise<CreatedAlert>, [CreatedAlert]>(),
  };
  const configService = {
    get: jest.fn().mockReturnValue('UTC'),
  };
  const logger = {
    setContext: jest.fn(),
    log: jest.fn(),
  };

  const service = new AlertEngineService(
    alertRulesRepository as never,
    alertsRepository as never,
    configService as never,
    logger as never,
  );

  return { service, alertRulesRepository, alertsRepository, logger };
}

describe('AlertEngineService', () => {
  describe('evaluateResourceChange', () => {
    it('does nothing when no resource_change rule exists', async () => {
      const { service, alertRulesRepository, alertsRepository } =
        buildService();
      alertRulesRepository.findEnabledByType.mockResolvedValue(null);

      await service.evaluateResourceChange('device-1', ['ram']);

      expect(alertsRepository.create).not.toHaveBeenCalled();
    });

    it('does nothing when the changed resource is not in the rule config', async () => {
      const { service, alertRulesRepository, alertsRepository } =
        buildService();
      alertRulesRepository.findEnabledByType.mockResolvedValue({
        config: { resources: ['cpu'] },
      });

      await service.evaluateResourceChange('device-1', ['ram']);

      expect(alertsRepository.create).not.toHaveBeenCalled();
    });

    it('creates an alert when a changed resource is enabled in the rule', async () => {
      const { service, alertRulesRepository, alertsRepository } =
        buildService();
      alertRulesRepository.findEnabledByType.mockResolvedValue({
        config: { resources: ['cpu', 'ram', 'disks'] },
      });

      await service.evaluateResourceChange('device-1', ['ram', 'cpu']);

      expect(alertsRepository.create).toHaveBeenCalledWith(
        expect.objectContaining({
          type: AlertRuleType.RESOURCE_CHANGE,
          deviceId: 'device-1',
          status: AlertStatus.OPEN,
          detail: { changes: ['ram', 'cpu'] },
        }),
      );
    });
  });

  describe('evaluateAccessEvent', () => {
    const baseParams = {
      deviceId: 'device-1',
      category: DeviceCategory.INFRASTRUCTURE,
      level: AccessEventLevel.OS,
      user: 'jdoe',
      action: AccessEventAction.LOGIN,
      timestamp: new Date('2026-01-05T03:00:00.000Z'), // 03:00 UTC — off hours
    };

    it('never fires for a collaborator-category device', async () => {
      const { service, alertRulesRepository, alertsRepository } =
        buildService();
      alertRulesRepository.findEnabledByType.mockResolvedValue({
        config: { habitualHours: { from: '07:00', to: '19:00' } },
      });

      await service.evaluateAccessEvent({
        ...baseParams,
        category: DeviceCategory.COLLABORATOR,
      });

      expect(alertsRepository.create).not.toHaveBeenCalled();
      expect(alertRulesRepository.findEnabledByType).not.toHaveBeenCalled();
    });

    it('never fires for a logout action', async () => {
      const { service, alertsRepository } = buildService();

      await service.evaluateAccessEvent({
        ...baseParams,
        action: AccessEventAction.LOGOUT,
      });

      expect(alertsRepository.create).not.toHaveBeenCalled();
    });

    it('does nothing when no off_hours_access rule exists', async () => {
      const { service, alertRulesRepository, alertsRepository } =
        buildService();
      alertRulesRepository.findEnabledByType.mockResolvedValue(null);

      await service.evaluateAccessEvent(baseParams);

      expect(alertsRepository.create).not.toHaveBeenCalled();
    });

    it('does nothing when the login is inside habitual hours', async () => {
      const { service, alertRulesRepository, alertsRepository } =
        buildService();
      alertRulesRepository.findEnabledByType.mockResolvedValue({
        config: { habitualHours: { from: '07:00', to: '19:00' } },
      });

      await service.evaluateAccessEvent({
        ...baseParams,
        timestamp: new Date('2026-01-05T12:00:00.000Z'),
      });

      expect(alertsRepository.create).not.toHaveBeenCalled();
    });

    it('creates an alert for an infrastructure login outside habitual hours', async () => {
      const { service, alertRulesRepository, alertsRepository } =
        buildService();
      alertRulesRepository.findEnabledByType.mockResolvedValue({
        config: { habitualHours: { from: '07:00', to: '19:00' } },
      });

      await service.evaluateAccessEvent(baseParams);

      expect(alertsRepository.create).toHaveBeenCalledTimes(1);
      const created = alertsRepository.create.mock.calls[0][0];
      expect(created.type).toBe(AlertRuleType.OFF_HOURS_ACCESS);
      expect(created.deviceId).toBe('device-1');
      expect(created.status).toBe(AlertStatus.OPEN);
      expect(created.detail.user).toBe('jdoe');
    });

    it('respects an overnight-wrapping habitual range', async () => {
      const { service, alertRulesRepository, alertsRepository } =
        buildService();
      alertRulesRepository.findEnabledByType.mockResolvedValue({
        config: { habitualHours: { from: '22:00', to: '06:00' } },
      });

      // 03:00 UTC is within a 22:00-06:00 overnight range.
      await service.evaluateAccessEvent(baseParams);

      expect(alertsRepository.create).not.toHaveBeenCalled();
    });
  });
});
