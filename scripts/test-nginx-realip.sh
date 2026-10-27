#!/bin/sh
# Repeatable test of the real-IP and scheme contract of the nginx template (ADR-0018 "Comprobaciones del contrato",
# SPEC-0009 RF-28 and CA-25). Needs Docker. It renders frontend/nginx/*.template inside the nginx image, puts a stub
# upstream (which echoes the X-Forwarded-* headers it receives) behind it on a throwaway network, and checks what
# the "API" would see. The client has a fixed address, so the result does not depend on how Docker NATs loopback.
# Usage: ./scripts/test-nginx-realip.sh   (NGINX_TEMPLATES_DIR overrides the directory with the templates)
set -eu

cd "$(dirname "$0")/.."

image="${NGINX_TEST_IMAGE:-nginxinc/nginx-unprivileged:stable-alpine}"
templates="${NGINX_TEMPLATES_DIR:-$PWD/frontend/nginx}"
id="itmas3-realip-$$"
subnet="172.31.77.0/24"
client_ip="172.31.77.50"
other_ip="172.31.77.51"
untrusted_range="203.0.113.0/24"
tmp="$(mktemp -d)"
failures=0
checks=0

cleanup() {
  docker rm -f "$id-echo" "$id-fe-subnet" "$id-fe-untrusted" "$id-fe-single" "$id-client" "$id-other" >/dev/null 2>&1 || true
  docker network rm "$id" >/dev/null 2>&1 || true
  rm -rf "$tmp"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM HUP

docker image inspect "$image" >/dev/null 2>&1 || docker pull "$image" >/dev/null

cat > "$tmp/echo.conf" <<'EOF'
server {
    listen 8080;
    location / {
        default_type text/plain;
        return 200 "xff=$http_x_forwarded_for proto=$http_x_forwarded_proto\n";
    }
}
EOF

docker network create --subnet "$subnet" "$id" >/dev/null
docker run -d --name "$id-echo" --network "$id" -v "$tmp/echo.conf:/etc/nginx/conf.d/default.conf:ro" "$image" >/dev/null

# $1 name suffix, $2 EDGE_TRUSTED_PROXY_FROM
start_frontend() {
  docker run -d --name "$id-fe-$1" --network "$id" \
    -v "$templates/default.conf.template:/etc/nginx/templates/default.conf.template:ro" \
    -v "$templates/security-headers.inc.template:/etc/nginx/templates/security-headers.inc.template:ro" \
    -e API_UPSTREAM="http://$id-echo:8080" \
    -e ENTRA_AUTHORITY_ORIGIN="https://login.example.invalid" \
    -e EDGE_TRUSTED_PROXY_FROM="$2" \
    "$image" >/dev/null
}

start_frontend subnet "$subnet"
start_frontend untrusted "$untrusted_range"
start_frontend single "$client_ip/32"
docker run -d --name "$id-client" --network "$id" --ip "$client_ip" --entrypoint sleep "$image" 600 >/dev/null
docker run -d --name "$id-other" --network "$id" --ip "$other_ip" --entrypoint sleep "$image" 600 >/dev/null

wait_ready() {
  tries=0
  until docker exec "$id-client" wget -q --spider "http://$id-fe-$1:8080/healthz" >/dev/null 2>&1; do
    tries=$((tries + 1))
    if [ "$tries" -ge 30 ]; then
      echo "FAIL: frontend $1 did not become ready" >&2
      docker logs "$id-fe-$1" >&2 || true
      exit 1
    fi
    sleep 1
  done
}
wait_ready subnet
wait_ready untrusted
wait_ready single

# $1 frontend suffix, $2 client container suffix ("client" or "other"), $3 path, $4 X-Forwarded-For (or empty), $5 X-Forwarded-Proto (or empty)
request() {
  set -- "$1" "$2" "$3" "$4" "$5"
  if [ -n "$4" ] && [ -n "$5" ]; then
    docker exec "$id-$2" wget -qO- --header "X-Forwarded-For: $4" --header "X-Forwarded-Proto: $5" "http://$id-fe-$1:8080$3"
  elif [ -n "$4" ]; then
    docker exec "$id-$2" wget -qO- --header "X-Forwarded-For: $4" "http://$id-fe-$1:8080$3"
  elif [ -n "$5" ]; then
    docker exec "$id-$2" wget -qO- --header "X-Forwarded-Proto: $5" "http://$id-fe-$1:8080$3"
  else
    docker exec "$id-$2" wget -qO- "http://$id-fe-$1:8080$3"
  fi
}

# $1 description, $2 expected "xff proto", then the arguments of request()
check() {
  description="$1"
  expected="$2"
  shift 2
  checks=$((checks + 1))
  got="$(request "$@" | sed -n 's/^xff=\([^ ]*\) proto=\(.*\)$/\1 \2/p')"
  if [ "$got" = "$expected" ]; then
    echo "PASS: $description"
  else
    echo "FAIL: $description (expected '$expected', got '$got')"
    failures=$((failures + 1))
  fi
}

# Table of ADR-0018 with the proxy in the trusted range.
check "trusted: X-Forwarded-For and https honoured" "203.0.113.9 https" subnet client /api/v1/x "203.0.113.9" "https"
check "trusted: the 16 MB route honours them too" "203.0.113.9 https" subnet client /api/v1/sql-audit-snapshots "203.0.113.9" "https"
check "trusted: no headers gives the connection and http" "$client_ip http" subnet client /api/v1/x "" ""
check "trusted: only the last address of X-Forwarded-For is used" "203.0.113.9 http" subnet client /api/v1/x "1.2.3.4, 203.0.113.9" ""
check "trusted: a scheme other than https falls back to the connection" "203.0.113.9 http" subnet client /api/v1/x "203.0.113.9" "javascript"
# real_ip_recursive off: an address of the trusted range inside the header is NOT skipped (with "on" nginx would walk past it to 203.0.113.9).
check "trusted: the last address wins even if it is inside the trusted range" "$other_ip http" subnet client /api/v1/x "203.0.113.9, $other_ip" ""
# Outside the trusted range nothing is honoured (header, nor scheme).
check "untrusted: X-Forwarded-For ignored and scheme not honoured" "$client_ip http" untrusted client /api/v1/x "203.0.113.9" "https"
check "untrusted: the 16 MB route ignores them too" "$client_ip http" untrusted client /api/v1/sql-audit-snapshots "203.0.113.9" "https"
# Narrowed to a single address (the gateway of edge in production, SPEC-0009 RF-28).
check "single address: the trusted one is honoured" "203.0.113.9 https" single client /api/v1/x "203.0.113.9" "https"
check "single address: another host of the same subnet is not" "$other_ip http" single other /api/v1/x "203.0.113.9" "https"

echo "$((checks - failures)) of $checks checks passed."
[ "$failures" -eq 0 ]
