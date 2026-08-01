import { JwtPayload } from '../models/auth.models';

export function decodeJwt(token: string): JwtPayload | null {
  const parts = token.split('.');
  if (parts.length !== 3) {
    return null;
  }

  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '=');
    const json = decodeURIComponent(
      atob(padded)
        .split('')
        .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
        .join('')
    );
    return JSON.parse(json) as JwtPayload;
  } catch {
    return null;
  }
}

export function isJwtExpired(payload: JwtPayload | null): boolean {
  if (!payload) {
    return true;
  }
  return payload.exp * 1000 <= Date.now();
}
