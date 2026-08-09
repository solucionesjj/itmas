"""Non-secret agent configuration — everything EXCEPT the node API key,
which lives in the platform's CredentialStore (System Keychain on macOS),
never in this plain JSON file. See Resources/config.example.json.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
from typing import Optional, Union

from itmas_agent.persistence.json_store import read_json

_DEFAULT_PUBLIC_IP_URL = "https://api.ipify.org?format=text"


class ConfigurationError(Exception):
    pass


@dataclass
class AgentConfiguration:
    api_base_url: str
    category: str  # 'collaborator' | 'infrastructure'
    scheduled_hour: int = 9
    scheduled_minute: int = 0
    public_ip_lookup_url: str = _DEFAULT_PUBLIC_IP_URL
    tls_ca_bundle_path: Optional[str] = None  # custom trust anchor for a self-signed backend

    @property
    def tls_verify(self) -> Union[bool, str]:
        return self.tls_ca_bundle_path if self.tls_ca_bundle_path else True


def load_configuration(path: Path) -> AgentConfiguration:
    data = read_json(path)
    if not data:
        raise ConfigurationError(f"configuration file not found or unreadable: {path}")
    try:
        return AgentConfiguration(
            api_base_url=data["apiBaseUrl"],
            category=data["category"],
            scheduled_hour=data.get("scheduledHour", 9),
            scheduled_minute=data.get("scheduledMinute", 0),
            public_ip_lookup_url=data.get("publicIpLookupUrl", _DEFAULT_PUBLIC_IP_URL),
            tls_ca_bundle_path=data.get("tlsCaBundlePath"),
        )
    except KeyError as exc:
        raise ConfigurationError(f"missing required configuration key: {exc}") from None
