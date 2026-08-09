from __future__ import annotations

import json
import logging

from itmas_agent.logging_setup import SecretRedactionFilter, configure_logging

_SECRET = "6a776b57-uuid.super-secret-value"


def test_secret_is_redacted_from_the_message():
    log_filter = SecretRedactionFilter()
    log_filter.add_secret(_SECRET)
    record = logging.LogRecord(
        name="test", level=logging.INFO, pathname="", lineno=0,
        msg=f"sending request with key {_SECRET}", args=(), exc_info=None,
    )

    log_filter.filter(record)

    assert _SECRET not in record.getMessage()
    assert "[REDACTED]" in record.getMessage()


def test_secret_is_redacted_even_when_interpolated_via_args():
    log_filter = SecretRedactionFilter()
    log_filter.add_secret(_SECRET)
    record = logging.LogRecord(
        name="test", level=logging.INFO, pathname="", lineno=0,
        msg="sending request with key %s", args=(_SECRET,), exc_info=None,
    )

    log_filter.filter(record)

    assert _SECRET not in record.getMessage()


def test_no_secrets_registered_leaves_messages_unchanged():
    log_filter = SecretRedactionFilter()
    record = logging.LogRecord(
        name="test", level=logging.INFO, pathname="", lineno=0,
        msg="ordinary message", args=(), exc_info=None,
    )

    log_filter.filter(record)

    assert record.getMessage() == "ordinary message"


def test_add_secret_ignores_none_and_empty_string():
    log_filter = SecretRedactionFilter()
    log_filter.add_secret(None)
    log_filter.add_secret("")

    assert log_filter._secrets == []


def test_configure_logging_writes_json_lines_and_redacts(tmp_path):
    log_path = tmp_path / "agent.log"
    logger, redaction_filter = configure_logging(log_path)
    redaction_filter.add_secret(_SECRET)

    logger.info(f"about to call the API with key {_SECRET}")
    for handler in logger.handlers:
        handler.flush()

    contents = log_path.read_text().strip()
    assert _SECRET not in contents
    parsed = json.loads(contents)
    assert parsed["level"] == "INFO"
    assert "[REDACTED]" in parsed["message"]
