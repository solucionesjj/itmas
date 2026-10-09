#!/bin/sh
# Builds the ITMas device agent for every supported platform inside Docker (no local SDK needed, ADR-0010 §8)
# and leaves the packages in agent/dist/<version>/ and agent/dist/latest/, each with a SHA256SUMS file.
# nginx serves agent/dist at /downloads/agent/ (SPEC-0002 RF-18).
# Usage: ./scripts/build-agent.sh [--skip-tests]
set -eu

cd "$(dirname "$0")/.."

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' agent/Directory.Build.props)"
if [ -z "$version" ]; then
  echo "Agent version not found in agent/Directory.Build.props" >&2
  exit 1
fi

if [ "${1:-}" != "--skip-tests" ]; then
  echo "Running agent tests..."
  docker build --target test agent
fi

echo "Building agent $version..."
rm -rf "agent/dist/$version"
docker build --target artifacts --output "type=local,dest=agent/dist/$version" agent

rm -rf agent/dist/latest
cp -R "agent/dist/$version" agent/dist/latest

echo "Packages:"
(cd "agent/dist/$version" && cat SHA256SUMS)
