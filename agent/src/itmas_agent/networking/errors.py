from __future__ import annotations

from typing import Optional


class ApiError(Exception):
    """Raised on any non-2xx response or network failure.

    `retryable` distinguishes "queue this and try again later" (network
    error, 429, 5xx) from "this payload itself is wrong, retrying it
    identically will never succeed" (4xx other than 429) — the caller uses
    this to decide whether to enqueue the payload in RetryQueueStore.
    """

    def __init__(
        self, message: str, status_code: Optional[int] = None, retryable: bool = True
    ) -> None:
        super().__init__(message)
        self.status_code = status_code
        self.retryable = retryable
