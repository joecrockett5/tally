#!/usr/bin/env bash
# Runs an image against a throwaway Postgres and checks it really works: the
# health endpoint reports the expected version, the SPA is served, the built-in
# healthcheck passes, and a profile can search the seeded CoFID foods.
# Usage: smoke-test.sh IMAGE EXPECTED_VERSION
set -euo pipefail

image="${1:?Usage: smoke-test.sh IMAGE EXPECTED_VERSION}"
expected="${2:?Usage: smoke-test.sh IMAGE EXPECTED_VERSION}"
id="tally-smoke-$$"

cleanup() {
  status=$?
  if [[ $status -ne 0 ]]; then docker logs "$id-app" 2>&1 | tail -n 50 || true; fi
  docker rm -f "$id-app" "$id-db" >/dev/null 2>&1 || true
  docker network rm "$id" >/dev/null 2>&1 || true
  exit $status
}
trap cleanup EXIT

docker network create "$id" >/dev/null
docker run -d --name "$id-db" --network "$id" \
  -e POSTGRES_USER=tally -e POSTGRES_PASSWORD=smoke -e POSTGRES_DB=tally postgres:18-alpine >/dev/null
docker run -d --name "$id-app" --network "$id" -p 127.0.0.1::8080 \
  -e "ConnectionStrings__Tally=Host=$id-db;Port=5432;Database=tally;Username=tally;Password=smoke;Gss Encryption Mode=Disable" \
  -e Tally__OpenFoodFacts__Enabled=false "$image" >/dev/null

url="http://$(docker port "$id-app" 8080/tcp | head -n 1)"

health=""
for _ in $(seq 1 90); do
  health=$(curl -fsS "$url/healthz" 2>/dev/null) && break
  sleep 2
done
want="{\"ok\":true,\"version\":\"$expected\"}"
[[ "$health" == "$want" ]] || { echo "Health check: expected $want, got '$health'" >&2; exit 1; }
echo "Health endpoint: $health"

docker exec "$id-app" dotnet Tally.Api.dll healthcheck
echo "Image healthcheck: passed"

curl -fsS "$url/" | grep -q '<div id="root">' || { echo "SPA not served at /" >&2; exit 1; }
echo "SPA: served"

profile=$(curl -fsS -X POST "$url/api/profiles" -H 'Content-Type: application/json' -d '{"name":"Smoke"}' | jq -r .id)
foods=$(curl -fsS "$url/api/foods/search?q=chicken" -H "X-Profile-Id: $profile" | jq length)
[[ "$foods" -gt 0 ]] || { echo "Food search found nothing: CoFID not seeded?" >&2; exit 1; }
echo "Food search: $foods CoFID results for 'chicken'"
