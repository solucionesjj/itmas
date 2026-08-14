# Documentación de itmas-agent

- [ARCHITECTURE.md](ARCHITECTURE.md) — el diseño núcleo/plataforma, por qué Python, por qué LaunchDaemon, por qué la cadencia de tres mecanismos, y cómo implementar Windows/Linux cuando llegue el momento.
- [PERMISSIONS.md](PERMISSIONS.md) — qué dato viene de qué fuente, y por qué ninguno requiere TCC (con la única excepción a verificar empíricamente).
- [INSTALL.md](INSTALL.md) — build, instalación, desinstalación, actualización.
- [CONFIGURATION.md](CONFIGURATION.md) — todos los campos de `config.json` y cómo se almacena la API key.

Para el contrato JSON extendido en sí (la estructura completa que produce el modelo interno), ver `../src/itmas_agent/normalization/extended_schema.py` y `docs/adr/0012-agent-backend-contract-extension-proposal.md` en la raíz del repositorio — esa es la especificación exacta, no duplicada aquí para evitar que las dos copias se desincronicen.
