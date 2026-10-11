#!/usr/bin/env bash
# Dry-run by default. Removes old ASP.NET release stamps only.
# Keeps current + KEEP_PREVIOUS newest stamps. Does not touch PHP, source,
# MariaDB, CloudPanel snapshots, or /opt/ecomae-aspnet-source.
set -euo pipefail

ROOT="${ECOMAE_ASPNET_RELEASE_ROOT:-/var/www/ecomae-aspnet}"
KEEP_PREVIOUS="${KEEP_PREVIOUS:-1}"
CONFIRM="${CONFIRM:-}"
SKIP_HEALTH="${SKIP_HEALTH:-0}"
SELFTEST="${1:-}"

selftest() {
	local tmp
	tmp="$(mktemp -d)"
	mkdir -p "$tmp/releases/20260101000000" "$tmp/releases/20260201000000" "$tmp/releases/20260301000000"
	printf 'a' >"$tmp/releases/20260101000000/f"
	printf 'bb' >"$tmp/releases/20260201000000/f"
	printf 'ccc' >"$tmp/releases/20260301000000/f"
	ln -s "$tmp/releases/20260301000000" "$tmp/current"
	ECOMAE_ASPNET_RELEASE_ROOT="$tmp" SKIP_HEALTH=1 KEEP_PREVIOUS=1 CONFIRM= \
		bash "$0" >"$tmp/dry.txt"
	grep -q 'KEEP 20260301000000' "$tmp/dry.txt"
	grep -q 'KEEP 20260201000000' "$tmp/dry.txt"
	grep -q 'PRUNE 20260101000000' "$tmp/dry.txt"
	[[ -d "$tmp/releases/20260101000000" ]]
	ECOMAE_ASPNET_RELEASE_ROOT="$tmp" SKIP_HEALTH=1 KEEP_PREVIOUS=1 CONFIRM=I_HAVE_READ_THE_LIST \
		bash "$0" >"$tmp/rm.txt"
	[[ ! -d "$tmp/releases/20260101000000" ]]
	[[ -d "$tmp/releases/20260201000000" ]]
	[[ -d "$tmp/releases/20260301000000" ]]
	[[ "$(readlink -f "$tmp/current")" == "$(readlink -f "$tmp/releases/20260301000000")" ]]
	rm -rf "$tmp"
	printf 'selftest ok\n'
	exit 0
}

if [[ "$SELFTEST" == "--self-test" ]]; then
	selftest
fi

if ! [[ "$KEEP_PREVIOUS" =~ ^[0-9]+$ ]]; then
	printf 'KEEP_PREVIOUS must be an integer\n' >&2
	exit 2
fi

printf '== EcomAE ASP.NET release prune ==\n'
printf 'root=%s keep_previous=%s confirm=%s utc=%s\n' \
	"$ROOT" "$KEEP_PREVIOUS" "${CONFIRM:-dry-run}" "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
printf 'This script never deletes PHP, source, databases, or CloudPanel snapshots.\n\n'

if [[ ! -d "$ROOT/releases" ]]; then
	printf 'missing %s/releases\n' "$ROOT" >&2
	exit 2
fi

if [[ ! -L "$ROOT/current" && ! -d "$ROOT/current" ]]; then
	printf 'missing current symlink at %s/current\n' "$ROOT" >&2
	exit 2
fi

CURRENT="$(readlink -f "$ROOT/current")"
CURRENT_STAMP="$(basename "$CURRENT")"
if [[ ! -d "$CURRENT" ]]; then
	printf 'current target is missing: %s\n' "$CURRENT" >&2
	exit 2
fi

if [[ "$SKIP_HEALTH" != "1" ]]; then
	code="$(curl -fsS -o /dev/null -w '%{http_code}' --max-time 8 http://127.0.0.1:5100/health || true)"
	if [[ "$code" != "200" ]]; then
		printf 'refuse: /health is %s (want 200). Set SKIP_HEALTH=1 only for a self-test.\n' "${code:-FAIL}" >&2
		exit 2
	fi
	printf 'health=200 current=%s\n' "$CURRENT_STAMP"
fi

mapfile -t STAMPS < <(find "$ROOT/releases" -mindepth 1 -maxdepth 1 -type d -printf '%f\n' | sort -r)
if [[ ${#STAMPS[@]} -eq 0 ]]; then
	printf 'no release stamps\n' >&2
	exit 2
fi

KEEP=()
PRUNE=()
kept_prev=0
for stamp in "${STAMPS[@]}"; do
	if [[ "$stamp" == "$CURRENT_STAMP" ]]; then
		KEEP+=("$stamp")
		continue
	fi
	if (( kept_prev < KEEP_PREVIOUS )); then
		KEEP+=("$stamp")
		kept_prev=$((kept_prev + 1))
		continue
	fi
	PRUNE+=("$stamp")
done

if [[ ${#KEEP[@]} -eq 0 ]]; then
	printf 'refuse: keep list is empty\n' >&2
	exit 2
fi

printf '\n-- keep --\n'
for stamp in "${KEEP[@]}"; do
	du -sh "$ROOT/releases/$stamp" 2>/dev/null | awk -v s="$stamp" '{print "KEEP", s, $1}'
done

printf '\n-- prune --\n'
if [[ ${#PRUNE[@]} -eq 0 ]]; then
	printf 'nothing to prune\n'
	exit 0
fi
for stamp in "${PRUNE[@]}"; do
	du -sh "$ROOT/releases/$stamp" 2>/dev/null | awk -v s="$stamp" '{print "PRUNE", s, $1}'
done
printf 'prune_count=%s keep_count=%s\n' "${#PRUNE[@]}" "${#KEEP[@]}"

if [[ "$CONFIRM" != "I_HAVE_READ_THE_LIST" ]]; then
	printf '\nDry-run only. Re-run with CONFIRM=I_HAVE_READ_THE_LIST to delete the PRUNE lines.\n'
	printf 'Example:\n  KEEP_PREVIOUS=1 CONFIRM=I_HAVE_READ_THE_LIST bash %s\n' "$0"
	exit 0
fi

printf '\n-- deleting prune list --\n'
for stamp in "${PRUNE[@]}"; do
	if [[ "$stamp" == "$CURRENT_STAMP" ]]; then
		printf 'refuse: will not delete current %s\n' "$stamp" >&2
		exit 2
	fi
	if [[ "$(readlink -f "$ROOT/releases/$stamp")" == "$CURRENT" ]]; then
		printf 'refuse: stamp resolves to current\n' >&2
		exit 2
	fi
	rm -rf --one-file-system "$ROOT/releases/$stamp"
	printf 'removed %s\n' "$stamp"
done

printf 'done. current still %s\n' "$(readlink -f "$ROOT/current")"
if [[ "$SKIP_HEALTH" != "1" ]]; then
	curl -fsS -o /dev/null -w 'health=%{http_code}\n' --max-time 8 http://127.0.0.1:5100/health || true
	curl -fsS -o /dev/null -w 'ready=%{http_code}\n' --max-time 8 http://127.0.0.1:5100/ready || true
fi
