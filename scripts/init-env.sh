#!/bin/sh
# Creates .env from .env.example and fills every empty *_PASSWORD with a random value.
# Microsoft Entra ID values must still be completed by hand. Never overwrites an existing .env.
set -eu

cd "$(dirname "$0")/.."

if [ -f .env ]; then
  echo ".env already exists; nothing to do." >&2
  exit 1
fi

umask 077
while IFS= read -r line || [ -n "$line" ]; do
  case "$line" in
    *_PASSWORD=)
      printf '%s%s\n' "$line" "$(openssl rand -hex 24)" ;;
    *)
      printf '%s\n' "$line" ;;
  esac
done < .env.example > .env

echo ".env created with random database passwords (permissions 600)."
echo "Complete ENTRA_TENANT_ID, ENTRA_CLIENT_ID and ENTRA_CLIENT_SECRET to enable sign-in."
