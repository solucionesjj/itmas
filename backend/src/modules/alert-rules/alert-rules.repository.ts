import { Injectable } from '@nestjs/common';
import { InjectModel } from '@nestjs/mongoose';
import { Model } from 'mongoose';
import { AlertRule, AlertRuleDocument } from './alert-rule.schema';
import { AlertRuleType } from './alert-rule-type.enum';
import { isDuplicateKeyError } from '../../common/mongo/duplicate-key.util';

export interface CreateAlertRuleResult {
  created: boolean;
  rule?: AlertRuleDocument;
}

@Injectable()
export class AlertRulesRepository {
  constructor(
    @InjectModel(AlertRule.name)
    private readonly alertRuleModel: Model<AlertRuleDocument>,
  ) {}

  findAll(): Promise<AlertRuleDocument[]> {
    return this.alertRuleModel.find().exec();
  }

  findById(id: string): Promise<AlertRuleDocument | null> {
    return this.alertRuleModel.findById(id).exec();
  }

  findByType(type: AlertRuleType): Promise<AlertRuleDocument | null> {
    return this.alertRuleModel.findOne({ type }).exec();
  }

  findEnabledByType(type: AlertRuleType): Promise<AlertRuleDocument | null> {
    return this.alertRuleModel.findOne({ type, enabled: true }).exec();
  }

  /** Used only by the bootstrap seed — safe to call every startup. */
  async createIfMissing(data: AlertRule): Promise<CreateAlertRuleResult> {
    try {
      const rule = await this.alertRuleModel.create(data);
      return { created: true, rule };
    } catch (error) {
      if (isDuplicateKeyError(error)) {
        return { created: false };
      }
      throw error;
    }
  }

  create(data: AlertRule): Promise<AlertRuleDocument> {
    return this.alertRuleModel.create(data);
  }

  async update(
    id: string,
    changes: Partial<Pick<AlertRule, 'enabled' | 'config'>>,
  ): Promise<AlertRuleDocument | null> {
    return this.alertRuleModel
      .findByIdAndUpdate(
        id,
        { $set: { ...changes, updatedAt: new Date() } },
        { returnDocument: 'after' },
      )
      .exec();
  }
}
