import {
  ArgumentsHost,
  Catch,
  ExceptionFilter,
  HttpException,
  HttpStatus,
  Inject,
} from '@nestjs/common';
import { Request, Response } from 'express';
import { JsonLoggerService } from '../logger/json-logger.service';

interface ErrorBody {
  error: {
    code: string;
    message: string;
    requestId: string;
  };
}

interface RawHttpError {
  status?: number;
  statusCode?: number;
}

function isRawHttpError(exception: unknown): exception is RawHttpError {
  if (typeof exception !== 'object' || exception === null) {
    return false;
  }
  const status = (exception as RawHttpError).status;
  const statusCode = (exception as RawHttpError).statusCode;
  const value = status ?? statusCode;
  return typeof value === 'number' && value >= 400 && value < 600;
}

// Deliberately generic, safe-to-expose text — never the raw middleware
// error's own message (which can include internal path/library details).
function rawHttpErrorMessage(status: number): string {
  // Plain numeric literals, not HttpStatus enum members: `status` here is a
  // number pulled off a raw (non-Nest) error, and comparing it against enum
  // members trips @typescript-eslint/no-unsafe-enum-comparison — whose
  // autofixer then strips any `as HttpStatus` cast meant to satisfy it,
  // fighting itself. Numeric literals sidestep the conflict entirely.
  switch (status) {
    case 413: // HttpStatus.PAYLOAD_TOO_LARGE
      return 'Request payload is too large';
    case 400: // HttpStatus.BAD_REQUEST
      return 'The request could not be parsed';
    default:
      return 'The request could not be processed';
  }
}

@Catch()
export class AllExceptionsFilter implements ExceptionFilter {
  constructor(
    @Inject(JsonLoggerService) private readonly logger: JsonLoggerService,
  ) {
    this.logger.setContext(AllExceptionsFilter.name);
  }

  catch(exception: unknown, host: ArgumentsHost) {
    const ctx = host.switchToHttp();
    const response = ctx.getResponse<Response>();
    const request = ctx.getRequest<Request>();
    const requestId = request.requestId ?? 'unknown';

    let status = HttpStatus.INTERNAL_SERVER_ERROR;
    let code = 'INTERNAL_ERROR';
    let message = 'An unexpected error occurred';

    if (exception instanceof HttpException) {
      status = exception.getStatus();
      const body = exception.getResponse();
      code = HttpStatus[status] ?? 'ERROR';
      if (typeof body === 'string') {
        message = body;
      } else if (typeof body === 'object' && body !== null) {
        const maybeMessage = (body as Record<string, unknown>).message;
        message = Array.isArray(maybeMessage)
          ? maybeMessage.join('; ')
          : ((maybeMessage as string) ?? exception.message);
      }
    } else if (isRawHttpError(exception)) {
      // A raw (non-Nest) error thrown by underlying middleware — e.g.
      // Express's body-parser PayloadTooLargeError for an oversized request —
      // still carries a real HTTP status; without this branch it would fall
      // through to a misleading generic 500 instead of e.g. 413.
      status = exception.status ?? exception.statusCode ?? status;
      code = HttpStatus[status] ?? 'ERROR';
      message = rawHttpErrorMessage(status);
    }

    this.logger.error(message, {
      requestId,
      path: request.originalUrl,
      method: request.method,
      status,
      // Never log request/response bodies here: may contain passwords/tokens.
    });

    const responseBody: ErrorBody = {
      error: { code, message, requestId },
    };
    response.status(status).json(responseBody);
  }
}
