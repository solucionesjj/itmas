"""Structured (JSON-lines) logging with automatic secret redaction.

The node API key must never appear in any log line, even if a caller
accidentally interpolates it into a message. `SecretRedactionFilter` runs
before formatting and rewrites the already-resolved message, so a redacted
value never survives regardless of how it entered the log call.

The redaction filter is created empty and secrets are registered via
`add_secret()` once known (e.g. right after reading the API key from
Keychain) — logging must be able to start before the key is available, but
every log line emitted from that point forward is still protected.
"""

from __future__ import annotations

import json
import logging
import sys
from pathlib import Path
from typing import Optional

_REDACTED = "[REDACTED]"


class SecretRedactionFilter(logging.Filter):
    def __init__(self, secrets: Optional[list[str]] = None) -> None:
        super().__init__()
        self._secrets = [s for s in (secrets or []) if s]

    def add_secret(self, secret: Optional[str]) -> None:
        if secret and secret not in self._secrets:
            self._secrets.append(secret)

    def filter(self, record: logging.LogRecord) -> bool:
        message = record.getMessage()
        for secret in self._secrets:
            message = message.replace(secret, _REDACTED)
        record.msg = message
        record.args = ()
        return True


class JsonLinesFormatter(logging.Formatter):
    def format(self, record: logging.LogRecord) -> str:
        payload = {
            "timestamp": self.formatTime(record, "%Y-%m-%dT%H:%M:%S%z"),
            "level": record.levelname,
            "logger": record.name,
            "message": record.getMessage(),
        }
        if record.exc_info:
            payload["exception"] = self.formatException(record.exc_info)
        return json.dumps(payload)


def configure_logging(log_file: Optional[Path] = None) -> tuple[logging.Logger, SecretRedactionFilter]:
    logger = logging.getLogger("itmas_agent")
    logger.setLevel(logging.INFO)
    logger.handlers.clear()

    handler: logging.Handler
    if log_file:
        log_file.parent.mkdir(parents=True, exist_ok=True)
        handler = logging.FileHandler(log_file)
    else:
        handler = logging.StreamHandler(sys.stdout)
    handler.setFormatter(JsonLinesFormatter())

    redaction_filter = SecretRedactionFilter()
    handler.addFilter(redaction_filter)

    logger.addHandler(handler)
    logger.propagate = False
    return logger, redaction_filter
