import { Injectable, LoggerService, LogLevel, Scope } from '@nestjs/common';

interface LogEntry {
  timestamp: string;
  level: LogLevel;
  context?: string;
  message: string;
  [key: string]: unknown;
}

/**
 * Structured JSON logger. Never pass password/token/passwordHash values in
 * `meta` — nothing here redacts them, callers must keep secrets out.
 */
@Injectable({ scope: Scope.TRANSIENT })
export class JsonLoggerService implements LoggerService {
  private context?: string;

  setContext(context: string) {
    this.context = context;
  }

  // Nest's internal framework logger calls `.log(message, context)` etc. with a
  // plain string context (not our own `{ field: value }` meta convention) — the
  // last string in optionalParams is treated as a one-off context override so
  // both calling conventions are handled without corrupting the log entry.
  private write(level: LogLevel, message: unknown, optionalParams: unknown[]) {
    let meta: Record<string, unknown> | undefined;
    let contextOverride: string | undefined;
    for (const param of optionalParams) {
      if (typeof param === 'string') {
        contextOverride = param;
      } else if (param && typeof param === 'object' && !Array.isArray(param)) {
        meta = { ...meta, ...(param as Record<string, unknown>) };
      }
    }

    const entry: LogEntry = {
      timestamp: new Date().toISOString(),
      level,
      context: contextOverride ?? this.context,
      message: typeof message === 'string' ? message : JSON.stringify(message),
      ...meta,
    };
    process.stdout.write(`${JSON.stringify(entry)}\n`);
  }

  log(message: unknown, ...optionalParams: unknown[]) {
    this.write('log', message, optionalParams);
  }

  error(message: unknown, ...optionalParams: unknown[]) {
    this.write('error', message, optionalParams);
  }

  warn(message: unknown, ...optionalParams: unknown[]) {
    this.write('warn', message, optionalParams);
  }

  debug(message: unknown, ...optionalParams: unknown[]) {
    this.write('debug', message, optionalParams);
  }

  verbose(message: unknown, ...optionalParams: unknown[]) {
    this.write('verbose', message, optionalParams);
  }
}
