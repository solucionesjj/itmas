import {
  IsBoolean,
  IsEmail,
  IsEnum,
  IsOptional,
  IsString,
  Matches,
  MinLength,
} from 'class-validator';
import { UserRole } from '../user-role.enum';
import {
  PASSWORD_POLICY_MESSAGE,
  PASSWORD_POLICY_REGEX,
} from '../../auth/dto/change-password.dto';

export class UpdateUserDto {
  @IsOptional()
  @IsEmail()
  email?: string;

  @IsOptional()
  @IsEnum(UserRole)
  role?: UserRole;

  @IsOptional()
  @IsBoolean()
  active?: boolean;

  // Admin password reset — forces mustChangePassword:true and invalidates the
  // user's outstanding refresh tokens (see UsersRepository.applyAdminUpdate).
  @IsOptional()
  @IsString()
  @MinLength(8)
  @Matches(PASSWORD_POLICY_REGEX, { message: PASSWORD_POLICY_MESSAGE })
  password?: string;
}
