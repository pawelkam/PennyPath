#!/usr/bin/env bash
# Run on an ARM64 Docker host after loading the application as pennypath:ci.
# All containers, state and database data belong to a disposable Compose project.
set -euo pipefail
command -v docker >/dev/null || { echo "Docker with Compose is required on an ARM64 host" >&2; exit 1; }
docker info >/dev/null
docker compose version >/dev/null
root_dir=$(cd "$(dirname "$0")/.." && pwd)
export LAN_HOST=localhost APP_PORT=18080 HTTPS_PORT=18443
export EDGE_SUBNET=172.30.91.0/24 EDGE_IP_RANGE=172.30.91.128/25 PROXY_IP=172.30.91.2
# Project names do not isolate IPAM pools. Fail before creating any resources.
python3 - "$EDGE_SUBNET" <<'PY'
import ipaddress, json, subprocess, sys
target = ipaddress.ip_network(sys.argv[1])
ids = subprocess.check_output(['docker', 'network', 'ls', '-q'], text=True).split()
networks = json.loads(subprocess.check_output(['docker', 'network', 'inspect', *ids], text=True)) if ids else []
for network in networks:
    for pool in network.get('IPAM', {}).get('Config') or []:
        subnet = pool.get('Subnet')
        if subnet:
            existing = ipaddress.ip_network(subnet, strict=False)
            if target.version == existing.version and target.overlaps(existing):
                sys.exit(f'Test subnet {target} conflicts with Docker network {network["Name"]} ({existing}); no networks changed')
PY
export DEPLOY_DIR
DEPLOY_DIR=$(mktemp -d "${TMPDIR:-/tmp}/pennypath-compose.XXXXXX")
export COMPOSE_PROJECT_NAME="pennypath-test-${GITHUB_RUN_ID:-$$}-${GITHUB_RUN_ATTEMPT:-1}"
export IMAGE_REPOSITORY=localhost:5000/pennypath
registry_name="$COMPOSE_PROJECT_NAME-registry"
export APP_IMAGE=pennypath:ci
export POSTGRES_PASSWORD_FILE="$DEPLOY_DIR/secrets/postgres-password"

compose() {
    docker compose --project-name "$COMPOSE_PROJECT_NAME" \
        --env-file "$DEPLOY_DIR/.env" -f "$DEPLOY_DIR/compose.yaml" "$@"
}
cleanup() {
    result=$?
    trap - EXIT
    if [ "$result" -ne 0 ]; then compose logs --tail 80 || true; fi
    compose down --volumes || true
    docker container rm -f "$registry_name" || true
    # Temporary state intentionally remains available for inspecting CI failures.
    exit "$result"
}
cp "$root_dir/compose.yaml" "$DEPLOY_DIR/compose.yaml"
mkdir "$DEPLOY_DIR/deploy"
cp "$root_dir/deploy/Caddyfile" "$DEPLOY_DIR/deploy/Caddyfile"
mkdir "$DEPLOY_DIR/secrets"
chmod 700 "$DEPLOY_DIR/secrets"
printf 'POSTGRES_PASSWORD_FILE=./secrets/postgres-password\nAPP_PORT=18080\nLAN_HOST=localhost\nHTTPS_PORT=18443\nEDGE_SUBNET=172.30.91.0/24\nEDGE_IP_RANGE=172.30.91.128/25\nPROXY_IP=172.30.91.2\n' > "$DEPLOY_DIR/.env"
(umask 077; openssl rand -hex 32 > "$DEPLOY_DIR/secrets/postgres-password")
chmod 600 "$DEPLOY_DIR/.env"
chmod 444 "$DEPLOY_DIR/secrets/postgres-password"
trap cleanup EXIT

compose config --format json > "$DEPLOY_DIR/config.json"
python3 - "$DEPLOY_DIR/config.json" <<'PY'
import ipaddress, json, sys
from pathlib import Path
from stat import S_IMODE
config = json.load(open(sys.argv[1]))
db = config['services']['postgres']
assert not db.get('ports'), 'Database port must not be published'
assert db['volumes'][0]['target'] == '/var/lib/postgresql'
assert all(service['restart'] == 'unless-stopped' for service in config['services'].values())
secret = Path(config['secrets']['postgres_password']['file'])
assert S_IMODE(secret.parent.stat().st_mode) == 0o700, 'Secret directory must be private'
assert S_IMODE(secret.stat().st_mode) == 0o444, 'Secret must be readable by the non-root app'
assert config['services']['app']['environment']['Database__PasswordFile'] == '/run/secrets/postgres_password'
app = config['services']['app']
proxy = config['services']['proxy']
assert app['ports'][0]['host_ip'] == '127.0.0.1', 'HTTP must remain loopback-only'
assert app['environment']['Proxy__KnownProxy'] == '172.30.91.2'
assert app['environment']['DataProtection__KeysPath'] == '/var/lib/pennypath/keys'
assert proxy['networks']['edge']['ipv4_address'] == '172.30.91.2'
assert config['networks']['edge']['ipam']['config'][0]['subnet'] == '172.30.91.0/24'
edge_pool = config['networks']['edge']['ipam']['config'][0]
dynamic_range = ipaddress.ip_network(edge_pool['ip_range'])
assert str(dynamic_range) == '172.30.91.128/25'
assert dynamic_range.subnet_of(ipaddress.ip_network(edge_pool['subnet']))
assert ipaddress.ip_address(proxy['networks']['edge']['ipv4_address']) not in dynamic_range, 'Proxy IP must be excluded from dynamic allocation'
assert 'default' not in proxy['networks'], 'Proxy must not join the database network'
assert {mount['target'] for mount in proxy['volumes']} == {'/data', '/config', '/etc/caddy/Caddyfile'}
PY
docker run -d --name "$registry_name" -p 127.0.0.1:5000:5000 registry:2
ready=false
for attempt in {1..30}; do
    if curl --fail --silent http://127.0.0.1:5000/v2/ >/dev/null; then ready=true; break; fi
    echo "Waiting for the test registry ($attempt/30)"
    sleep 1
done
[ "$ready" = true ]

publish_test_image() {
    docker tag "$1" "$IMAGE_REPOSITORY:$2"
    docker push "$IMAGE_REPOSITORY:$2" >/dev/null
    docker image inspect --format '{{index .RepoDigests 0}}' "$IMAGE_REPOSITORY:$2"
}
first=$(publish_test_image pennypath:ci first)
export APP_IMAGE=$first
compose up -d --wait --wait-timeout 120 postgres
bash "$root_dir/scripts/deploy.sh" "$first"
# Repeat the real PostgreSQL migrator to prove idempotence.
compose run --rm --no-deps --pull never app --migrate
# The app starts before Caddy: its dynamic address must not consume PROXY_IP.
app_container=$(compose ps -q app)
docker inspect "$app_container" > "$DEPLOY_DIR/app-network.json"
python3 - "$DEPLOY_DIR/app-network.json" <<'PY'
import ipaddress, json, os, sys
container = json.load(open(sys.argv[1]))[0]
network = container['NetworkSettings']['Networks'][os.environ['COMPOSE_PROJECT_NAME'] + '_edge']
address = ipaddress.ip_address(network['IPAddress'])
assert address != ipaddress.ip_address(os.environ['PROXY_IP']), 'App consumed the reserved proxy IP before Caddy started'
assert address in ipaddress.ip_network(os.environ['EDGE_IP_RANGE']), 'App address is outside the dynamic range'
print('App started before Caddy without consuming its static IP')
PY
compose up -d --no-deps proxy
ca_cert="$DEPLOY_DIR/proxy-root.crt"
ready=false
for attempt in {1..30}; do
    if compose exec -T proxy test -s /data/caddy/pki/authorities/local/root.crt; then ready=true; break; fi
    echo "Waiting for the test CA ($attempt/30)"
    sleep 1
done
[ "$ready" = true ]
compose cp proxy:/data/caddy/pki/authorities/local/root.crt "$ca_cert"
ready=false
for attempt in {1..30}; do
    if curl --fail --silent --cacert "$ca_cert" https://localhost:18443/health >/dev/null; then ready=true; break; fi
    echo "Waiting for trusted HTTPS ($attempt/30)"
    sleep 1
done
[ "$ready" = true ]
python3 "$root_dir/scripts/smoke-test.py" http://127.0.0.1:18080
python3 "$root_dir/scripts/smoke-test.py" https://localhost:18443 --ca-cert "$ca_cert"
python3 - "$ca_cert" <<'PY'
import json, ssl, sys, urllib.request
request = urllib.request.Request('https://localhost:18443/api/auth/csrf', headers={
    'X-Forwarded-Proto': 'http', 'X-Forwarded-For': '192.0.2.10', 'Forwarded': 'proto=http'})
with urllib.request.urlopen(request, context=ssl.create_default_context(cafile=sys.argv[1]), timeout=10) as response:
    assert response.status == 200, 'Caddy must replace untrusted forwarding headers'
    assert json.load(response)['requestToken']
PY
session_state="$DEPLOY_DIR/auth-session.json"
python3 "$root_dir/scripts/test-auth.py" https://localhost:18443 --ca-cert "$ca_cert" --state "$session_state"
resume_session() {
    python3 "$root_dir/scripts/test-auth.py" https://localhost:18443 --ca-cert "$ca_cert" --resume "$session_state"
}
app_container=$(compose ps -q app)
[ "$(docker inspect --format '{{.Architecture}}' "$first")" = arm64 ]
[ "$(docker exec "$app_container" id -u)" != 0 ]
docker exec "$app_container" sh -c 'test -r /run/secrets/postgres_password && cat /run/secrets/postgres_password >/dev/null'
docker exec "$app_container" sh -c \
    'test "$(stat -c %u /var/lib/pennypath/keys)" = "$(id -u)" && test "$(stat -c %a /var/lib/pennypath/keys)" = 700 && test -w /var/lib/pennypath/keys'
compose run --rm --no-deps --entrypoint sh app -c \
    "test \"\$(id -u)\" != 0 && test -r /run/secrets/postgres_password && cat /run/secrets/postgres_password >/dev/null"
proxy_before=$(compose ps -q proxy)
ca_before=$(shasum -a 256 "$ca_cert")
compose up -d --no-deps --force-recreate --wait --wait-timeout 120 app
resume_session
# HTTP must not become a way to forge the trusted proxy's HTTPS headers.
python3 - <<'PY'
import json, urllib.error, urllib.request
request = urllib.request.Request('http://127.0.0.1:18080/api/auth/csrf', headers={
    'X-Forwarded-Proto': 'https', 'X-Forwarded-For': '192.0.2.10'})
try:
    response = urllib.request.urlopen(request, timeout=10)
except urllib.error.HTTPError as error:
    response = error
with response:
    assert response.status == 400
    assert json.load(response)['code'] == 'https_required'
PY

# Keep a sentinel through a database recreation and an app deployment.
compose exec -T postgres psql -U pennypath -d pennypath -v ON_ERROR_STOP=1 \
    -c "CREATE TABLE deployment_probe (value text); INSERT INTO deployment_probe VALUES ('survives');"
compose up -d --no-deps --force-recreate --wait --wait-timeout 120 postgres
database_before=$(compose ps -q postgres)
docker build --builder default -t pennypath:test-second - <<'DOCKERFILE'
FROM pennypath:ci
LABEL pennypath.test.release="second"
DOCKERFILE
second=$(publish_test_image pennypath:test-second second)
bash "$root_dir/scripts/deploy.sh" "$second"
resume_session
[ "$(compose ps -q postgres)" = "$database_before" ]
[ "$(compose exec -T postgres psql -U pennypath -d pennypath -Atc 'SELECT value FROM deployment_probe')" = survives ]

# A missing image must never replace the healthy app.
container_before=$(compose ps -q app)
missing="$IMAGE_REPOSITORY@sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"
if bash "$root_dir/scripts/deploy.sh" "$missing"; then echo "Missing-image deploy unexpectedly succeeded" >&2; exit 1; fi
[ "$(compose ps -q app)" = "$container_before" ]
python3 "$root_dir/scripts/smoke-test.py" http://127.0.0.1:18080
resume_session

# A real failed migrator must preserve the exact app container and release record.
docker build --builder default -t pennypath:test-migration-failure - <<'DOCKERFILE'
FROM pennypath:ci
ENTRYPOINT ["sh", "-c", "exit 1"]
DOCKERFILE
bad_migration=$(publish_test_image pennypath:test-migration-failure migration-failure)
cp "$DEPLOY_DIR/release.env" "$DEPLOY_DIR/release-before-migration.env"
if bash "$root_dir/scripts/deploy.sh" "$bad_migration"; then
    echo "Failed-migration deploy unexpectedly succeeded" >&2
    exit 1
fi
[ "$(compose ps -q app)" = "$container_before" ]
cmp "$DEPLOY_DIR/release.env" "$DEPLOY_DIR/release-before-migration.env"
resume_session

# The inherited /health check will fail while this image only sleeps.
docker build --builder default -t pennypath:test-unhealthy - <<'DOCKERFILE'
FROM pennypath:ci
ENTRYPOINT ["sh", "-c", "if [ \"$1\" = --migrate ]; then exit 0; fi; exec sleep infinity", "fixture"]
DOCKERFILE
broken=$(publish_test_image pennypath:test-unhealthy broken)
if DEPLOY_HEALTH_TIMEOUT=20 bash "$root_dir/scripts/deploy.sh" "$broken"; then
    echo "Unhealthy deploy unexpectedly succeeded" >&2
    exit 1
fi
[ "$(docker inspect --format '{{.Config.Image}}' "$(compose ps -q app)")" = "$second" ]
python3 "$root_dir/scripts/smoke-test.py" http://127.0.0.1:18080
resume_session
bash "$root_dir/scripts/deploy.sh" rollback
[ "$(docker inspect --format '{{.Config.Image}}' "$(compose ps -q app)")" = "$first" ]
[ "$(compose ps -q postgres)" = "$database_before" ]
[ "$(compose exec -T postgres psql -U pennypath -d pennypath -Atc 'SELECT value FROM deployment_probe')" = survives ]
python3 "$root_dir/scripts/smoke-test.py" http://127.0.0.1:18080
resume_session
[ "$(compose ps -q proxy)" = "$proxy_before" ]
compose cp proxy:/data/caddy/pki/authorities/local/root.crt "$ca_cert"
[ "$(shasum -a 256 "$ca_cert")" = "$ca_before" ]
# Persistent CA/config volumes survive replacing the proxy itself, too.
compose up -d --no-deps --force-recreate proxy
ready=false
for attempt in {1..30}; do
    if curl --fail --silent --cacert "$ca_cert" https://localhost:18443/health >/dev/null; then ready=true; break; fi
    sleep 1
done
[ "$ready" = true ]
resume_session
compose cp proxy:/data/caddy/pki/authorities/local/root.crt "$ca_cert"
[ "$(shasum -a 256 "$ca_cert")" = "$ca_before" ]
echo "ARM64, non-root secrets/keys, PostgreSQL migrations, trusted HTTPS, session persistence and rollback checks passed"
