import { ExecutionContext, ForbiddenException } from '@nestjs/common';
import { Reflector } from '@nestjs/core';
import { RolesGuard } from './roles.guard';
import { UserRole } from '../../modules/users/user-role.enum';
import { AuthenticatedUser } from '../../modules/auth/authenticated-user.interface';

function buildContext(user?: AuthenticatedUser): ExecutionContext {
  return {
    switchToHttp: () => ({
      getRequest: () => ({ user }),
    }),
    getHandler: () => ({}),
    getClass: () => ({}),
  } as unknown as ExecutionContext;
}

describe('RolesGuard', () => {
  let reflector: Reflector;
  let guard: RolesGuard;

  beforeEach(() => {
    reflector = new Reflector();
    guard = new RolesGuard(reflector);
  });

  it('denies by default when no @Roles() metadata is declared', () => {
    jest.spyOn(reflector, 'getAllAndOverride').mockReturnValue(undefined);
    const context = buildContext({
      sub: '1',
      username: 'a',
      role: UserRole.USER,
      mustChangePassword: false,
    });

    expect(() => guard.canActivate(context)).toThrow(ForbiddenException);
  });

  it('denies when the user role is not in the required list', () => {
    jest
      .spyOn(reflector, 'getAllAndOverride')
      .mockReturnValue([UserRole.ADMINISTRATOR]);
    const context = buildContext({
      sub: '1',
      username: 'a',
      role: UserRole.USER,
      mustChangePassword: false,
    });

    expect(() => guard.canActivate(context)).toThrow(ForbiddenException);
  });

  it('denies when there is no authenticated user on the request', () => {
    jest
      .spyOn(reflector, 'getAllAndOverride')
      .mockReturnValue([UserRole.AUDITOR]);
    const context = buildContext(undefined);

    expect(() => guard.canActivate(context)).toThrow(ForbiddenException);
  });

  it('allows when the user role matches one of the required roles', () => {
    jest
      .spyOn(reflector, 'getAllAndOverride')
      .mockReturnValue([UserRole.AUDITOR, UserRole.ADMINISTRATOR]);
    const context = buildContext({
      sub: '1',
      username: 'a',
      role: UserRole.AUDITOR,
      mustChangePassword: false,
    });

    expect(guard.canActivate(context)).toBe(true);
  });
});
