import { IsNotEmpty, IsString } from 'class-validator';

export class AuthorizeSecurityGroupRuleDto {
  @IsString()
  @IsNotEmpty()
  observation!: string;
}
