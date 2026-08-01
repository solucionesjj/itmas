import { IsEmail, IsEnum, IsString, Matches, MinLength } from 'class-validator';
import { UserRole } from '../user-role.enum';
import {
  PASSWORD_POLICY_MESSAGE,
  PASSWORD_POLICY_REGEX,
} from '../../auth/dto/change-password.dto';

export class CreateUserDto {
  @IsString()
  @MinLength(1)
  username!: string;

  @IsEmail()
  email!: string;

  // Admin-supplied initial password — the created user always gets
  // mustChangePassword:true regardless of what's set here (see users.service.ts).
  @IsString()
  @MinLength(8)
  @Matches(PASSWORD_POLICY_REGEX, { message: PASSWORD_POLICY_MESSAGE })
  password!: string;

  @IsEnum(UserRole)
  role!: UserRole;
}
