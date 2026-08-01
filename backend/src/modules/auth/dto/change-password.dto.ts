import { IsString, Matches, MinLength } from 'class-validator';

// Complexity policy (agent.md §6.4): min 8 chars, at least one uppercase,
// one lowercase and one digit. Adjust here if the policy changes — single
// source of truth for password complexity.
export const PASSWORD_POLICY_REGEX = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+$/;
export const PASSWORD_POLICY_MESSAGE =
  'Password must contain at least one uppercase letter, one lowercase letter and one digit';

export class ChangePasswordDto {
  @IsString()
  currentPassword!: string;

  @IsString()
  @MinLength(8)
  @Matches(PASSWORD_POLICY_REGEX, { message: PASSWORD_POLICY_MESSAGE })
  newPassword!: string;
}
