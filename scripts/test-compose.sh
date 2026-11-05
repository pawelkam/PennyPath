#!/usr/bin/env bash
# Run on an ARM64 Docker host after loading the application as pennypath:ci.
# All containers, state and database data belong to a disposable Compose project.
set -euo pipefail
root_dir=$(cd "$(dirname "$0")/.." && pwd)
export DEPLOY_DIR
DEPLOY_DIR=$(mktemp -d "${TMPDIR:-/tmp}/pennypath-compose.XXXXXX")
export COMPOSE_PROJECT_NAME="pennypath-test-${GITHUB_RUN_ID:-$$}-${GITHUB_RUN_ATTEMPT:-1}"
export IMAGE_REPOSITORY=localhost:5000/pennypath
registry_name="$COMPOSE_PROJECT_NAME-registry"
export APP_IMAGE=pennypath:ci

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
printf 'POSTGRES_PASSWORD_FILE=./postgres-password\nAPP_PORT=18080\n' > "$DEPLOY_DIR/.env"
openssl rand -hex 32 > "$DEPLOY_DIR/postgres-password"
chmod 600 "$DEPLOY_DIR/postgres-password" "$DEPLOY_DIR/.env"
trap cleanup EXIT

compose config --format json > "$DEPLOY_DIR/config.json"
python3 - "$DEPLOY_DIR/config.json" <<'PY'
import json, sys
config = json.load(open(sys.argv[1]))
db = config['services']['postgres']
assert not db.get('ports'), 'Database port must not be published'
assert db['volumes'][0]['target'] == '/var/lib/postgresql'
assert all(service['restart'] == 'unless-stopped' for service in config['services'].values())
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
python3 "$root_dir/scripts/smoke-test.py" http://127.0.0.1:18080
app_container=$(compose ps -q app)
[ "$(docker inspect --format '{{.Architecture}}' "$first")" = arm64 ]
[ "$(docker exec "$app_container" id -u)" != 0 ]

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
[ "$(compose ps -q postgres)" = "$database_before" ]
[ "$(compose exec -T postgres psql -U pennypath -d pennypath -Atc 'SELECT value FROM deployment_probe')" = survives ]

# A missing image must never replace the healthy app.
container_before=$(compose ps -q app)
missing="$IMAGE_REPOSITORY@sha256:dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"
if bash "$root_dir/scripts/deploy.sh" "$missing"; then echo "Missing-image deploy unexpectedly succeeded" >&2; exit 1; fi
[ "$(compose ps -q app)" = "$container_before" ]
python3 "$root_dir/scripts/smoke-test.py" http://127.0.0.1:18080

# The inherited /health check will fail while this image only sleeps.
docker build --builder default -t pennypath:test-unhealthy - <<'DOCKERFILE'
FROM pennypath:ci
ENTRYPOINT ["sleep", "infinity"]
DOCKERFILE
broken=$(publish_test_image pennypath:test-unhealthy broken)
if DEPLOY_HEALTH_TIMEOUT=20 bash "$root_dir/scripts/deploy.sh" "$broken"; then
    echo "Unhealthy deploy unexpectedly succeeded" >&2
    exit 1
fi
[ "$(docker inspect --format '{{.Config.Image}}' "$(compose ps -q app)")" = "$second" ]
python3 "$root_dir/scripts/smoke-test.py" http://127.0.0.1:18080
bash "$root_dir/scripts/deploy.sh" rollback
[ "$(docker inspect --format '{{.Config.Image}}' "$(compose ps -q app)")" = "$first" ]
[ "$(compose ps -q postgres)" = "$database_before" ]
[ "$(compose exec -T postgres psql -U pennypath -d pennypath -Atc 'SELECT value FROM deployment_probe')" = survives ]
python3 "$root_dir/scripts/smoke-test.py" http://127.0.0.1:18080
echo "ARM64, non-root runtime, PostgreSQL persistence, failed pull and rollback checks passed"
