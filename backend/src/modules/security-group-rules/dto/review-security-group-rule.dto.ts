import { IsNotEmpty, IsString } from 'class-validator';

export class ReviewSecurityGroupRuleDto {
  @IsString()
  @IsNotEmpty()
  observation!: string;
}
