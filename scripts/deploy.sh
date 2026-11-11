#!/usr/bin/env bash
# Bash 3.2 compatible (the shell shipped with macOS).
set -euo pipefail
umask 077

deploy_dir=${DEPLOY_DIR:-"$HOME/Serwer/penny-path/deploy"}
repository=${IMAGE_REPOSITORY:-ghcr.io/pawelkam/pennypath}
health_timeout=${DEPLOY_HEALTH_TIMEOUT:-120}
case "$health_timeout" in
    ''|*[!0-9]*) echo "Invalid health timeout" >&2; exit 2 ;;
esac
if [ "$health_timeout" -lt 1 ] || [ "$health_timeout" -gt 120 ]; then
    echo "Health timeout must be between 1 and 120 seconds" >&2
    exit 2
fi

valid_image() {
    local digest
    case "$1" in
        "$repository"@sha256:*) digest=${1#"$repository"@sha256:} ;;
        *) return 1 ;;
    esac
    [ "${#digest}" -eq 64 ] || return 1
    case "$digest" in *[!0-9a-f]*) return 1 ;; esac
}

if [ "$#" -ne 1 ]; then
    echo "Usage: bash deploy.sh <image@sha256:digest|rollback>" >&2
    exit 2
fi
requested=$1
rollback=false
if [ "$requested" = rollback ]; then rollback=true; fi
if [ "$requested" != rollback ] && ! valid_image "$requested"; then
    echo "Expected an immutable image digest from $repository" >&2
    exit 2
fi
if [ ! -f "$deploy_dir/compose.yaml" ] || [ ! -f "$deploy_dir/.env" ]; then
    echo "Initialize $deploy_dir with compose.yaml, .env and the database password first" >&2
    exit 2
fi
cd "$deploy_dir"
if ! mkdir .deploy-lock 2>/dev/null; then
    echo "Another deployment holds $deploy_dir/.deploy-lock" >&2
    exit 1
fi

current=''
previous=''
changed=false
committed=false
compose() {
    docker compose --project-name "${COMPOSE_PROJECT_NAME:-pennypath}" \
        --env-file .env -f compose.yaml "$@"
}

service_container() {
    local service=$1 containers
    # Compose ps --all includes one-off `run` containers such as old migrators.
    containers=$(docker ps --all --quiet --no-trunc \
        --filter "label=com.docker.compose.project=${COMPOSE_PROJECT_NAME:-pennypath}" \
        --filter "label=com.docker.compose.service=$service" \
        --filter 'label=com.docker.compose.oneoff=False') || return 1
    case "$containers" in
        *$'\n'*)
            echo "Multiple service containers found for $service; reconcile them before deploying" >&2
            return 1
            ;;
    esac
    printf '%s' "$containers"
}

wait_healthy() {
    local expected=$1 container status image address port code deadline
    deadline=$((SECONDS + health_timeout))
    while [ "$SECONDS" -lt "$deadline" ]; do
        container=$(service_container app) || return 1
        if [ -n "$container" ]; then
            status=$(docker inspect --format '{{.State.Health.Status}}' "$container" 2>/dev/null) || status=unknown
            image=$(docker inspect --format '{{.Config.Image}}' "$container" 2>/dev/null) || image=unknown
            if [ "$status" = healthy ] && [ "$image" = "$expected" ]; then
                address=$(compose port app 8080) || return 1
                port=${address##*:}
                code=$(curl --silent --output /dev/null --write-out '%{http_code}' \
                    --max-time 3 "http://127.0.0.1:$port/health") || code=000
                [ "$code" != 200 ] || return 0
            fi
        fi
        sleep 2
    done
    return 1
}

finish() {
    local result=$?
    trap - EXIT INT TERM
    if [ "$changed" = true ] && [ "$committed" = false ]; then
        if [ -n "$current" ]; then
            echo "Deployment failed; restoring $current" >&2
            export APP_IMAGE=$current
            if compose up -d --no-deps --pull never app && wait_healthy "$current"; then
                echo "Previous application restored" >&2
            else
                echo "Rollback failed; inspect the app logs and retry the current digest" >&2
            fi
        else
            echo "First deployment failed; stopping the unhealthy app" >&2
            compose stop app || true
        fi
        result=1
    fi
    # Only files owned by this invocation; PostgreSQL is never stopped or recreated.
    if [ -f .release.env.tmp ]; then unlink .release.env.tmp; fi
    rmdir .deploy-lock
    exit "$result"
}
trap finish EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

if [ -f release.env ]; then
    current=$(sed -n 's/^APP_IMAGE=//p' release.env)
    previous=$(sed -n 's/^PREVIOUS_IMAGE=//p' release.env)
    if ! valid_image "$current" || { [ -n "$previous" ] && ! valid_image "$previous"; }; then
        echo "Invalid release.env; refusing to change the application" >&2
        exit 1
    fi
fi
if [ "$requested" = rollback ]; then
    if [ -z "$previous" ]; then
        echo "No previous release is recorded" >&2
        exit 1
    fi
    requested=$previous
fi
export APP_IMAGE=$requested
compose config --quiet

# Reject untracked/manual changes so a rollback cannot silently choose the wrong image.
container=$(service_container app)
if [ -n "$container" ]; then
    actual=$(docker inspect --format '{{.Config.Image}}' "$container")
    if [ -z "$current" ] || [ "$actual" != "$current" ]; then
        echo "Running app differs from release.env; reconcile it before deploying" >&2
        exit 1
    fi
fi
database=$(service_container postgres)
if [ -z "$database" ] || [ "$(docker inspect --format '{{.State.Health.Status}}' "$database")" != healthy ]; then
    echo "Start a healthy PostgreSQL service before deploying" >&2
    exit 1
fi

# Download before touching the running app; retain the cached current image for rollback.
docker pull --platform linux/arm64 "$requested"
if [ -n "$current" ]; then docker image inspect "$current" >/dev/null; fi
# A failed migration leaves the current app and release.env intact. Rollback only
# swaps the image: an older image must never try to migrate a newer schema.
if [ "$rollback" = false ]; then
    compose run --rm --no-deps --pull never app --migrate
fi
changed=true
compose up -d --no-deps --pull never app
if ! wait_healthy "$requested"; then
    echo "Application did not become healthy within $health_timeout seconds" >&2
    exit 1
fi
if [ "$requested" = "$current" ]; then
    next_previous=$previous
else
    next_previous=$current
fi
printf 'APP_IMAGE=%s\nPREVIOUS_IMAGE=%s\n' "$requested" "$next_previous" > .release.env.tmp
mv .release.env.tmp release.env
committed=true
echo "Deployed $requested"
