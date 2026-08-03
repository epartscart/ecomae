#!/usr/bin/env bash
# Non-interactive push of local final-gate smoke branch using GH_TOKEN / GITHUB_TOKEN.
# Never prompts for username/password (GitHub rejects account passwords).
# Never prints the token. Never invents smoke JSON. Never removes PHP.
set -euo pipefail

CANDIDATES=("${ECOMAE_REPO:-}" /opt/ecomae-aspnet-source /root/ecomae /opt/ecomae)
BRANCH="${ECOMAE_SMOKE_BRANCH:-cursor/final-gate-staging-smoke-7b3b}"
REMOTE_URL_DEFAULT="https://github.com/epartscart/ecomae.git"

find_repo() {
  local candidate
  for candidate in "${CANDIDATES[@]}"; do
    if [[ -n "$candidate" && -d "$candidate/.git" && -f "$candidate/scripts/cloudpanel_capture_final_gate_artifacts.sh" ]]; then
      printf '%s\n' "$candidate"
      return 0
    fi
  done
  return 1
}

printf '%s\n' '== Push final-gate smoke branch (token, non-interactive) =='

TOKEN="${GH_TOKEN:-${GITHUB_TOKEN:-}}"
if [[ -z "$TOKEN" ]]; then
  printf 'BLOCKED: set GH_TOKEN (or GITHUB_TOKEN) with repo scope. GitHub no longer accepts account passwords.\n' >&2
  printf '\nCreate a classic PAT: https://github.com/settings/tokens (scope: repo)\n' >&2
  printf 'Then on this server (paste the REAL token value — not the literal ghp_... placeholder):\n' >&2
  printf '  export GH_TOKEN='\''ghp_YOUR_REAL_TOKEN'\''\n' >&2
  printf '  bash scripts/cloudpanel_push_final_gate_smoke.sh\n' >&2
  printf '  unset GH_TOKEN\n' >&2
  printf '\nOr export a bundle for a machine that already has auth:\n' >&2
  printf '  bash scripts/cloudpanel_export_final_gate_smoke_bundle.sh\n' >&2
  exit 2
fi
# Reject docs placeholders that operators sometimes paste literally.
if [[ "$TOKEN" == "ghp_..." || "$TOKEN" == "github_pat_..." || "$TOKEN" == *'...' || "$TOKEN" == *YOUR_REAL* ]]; then
  printf 'BLOCKED: GH_TOKEN looks like a documentation placeholder, not a real PAT.\n' >&2
  printf 'Create one at https://github.com/settings/tokens (classic, scope: repo),\n' >&2
  printf 'then: export GH_TOKEN='\''ghp_<paste the full token here>'\''\n' >&2
  printf 'Do not paste the token into chat logs.\n' >&2
  exit 2
fi
if [[ ! "$TOKEN" =~ ^(ghp_|github_pat_) ]]; then
  printf 'WARN: GH_TOKEN does not start with ghp_ or github_pat_ — push may fail.\n' >&2
fi

REPO="$(find_repo || true)"
if [[ -z "$REPO" ]]; then
  printf 'ERROR: ecomae repo not found.\n' >&2
  exit 1
fi
cd "$REPO"

if ! git rev-parse --verify "$BRANCH" >/dev/null 2>&1; then
  printf 'ERROR: local branch %s not found. Run capture + commit first.\n' "$BRANCH" >&2
  exit 1
fi
git checkout "$BRANCH" >/dev/null

required=(
  "docs/migration/evidence/decommission/staging-smoke/price-lookup-aspnet.json"
  "docs/migration/evidence/decommission/staging-smoke/catalog-status-aspnet.json"
  "docs/migration/evidence/decommission/staging-smoke/surface-digests-aspnet.json"
)
for path in "${required[@]}"; do
  if ! git cat-file -e "HEAD:$path" 2>/dev/null; then
    printf 'ERROR: %s missing from HEAD on %s\n' "$path" "$BRANCH" >&2
    exit 1
  fi
done

printf 'Repo: %s\n' "$REPO"
printf 'Branch: %s (%s)\n' "$BRANCH" "$(git rev-parse --short HEAD)"
printf 'Using token auth (value not printed).\n'

# Never fall back to interactive username/password prompts.
export GIT_TERMINAL_PROMPT=0
# Prefer credential via URL for this one push only (does not rewrite origin).
# x-access-token works for classic PATs and fine-grained tokens.
PUSH_URL="https://x-access-token:${TOKEN}@github.com/epartscart/ecomae.git"
# Allow override when remote is a fork (still never print token).
if [[ -n "${ECOMAE_SMOKE_PUSH_URL:-}" ]]; then
  # Operator may pass https://x-access-token:TOKEN@host/org/repo.git themselves.
  PUSH_URL="$ECOMAE_SMOKE_PUSH_URL"
fi

set +e
git push -u "$PUSH_URL" "HEAD:refs/heads/${BRANCH}"
rc=$?
set -e

# Drop token from this shell's exported env when invoked via `source` is not our job;
# clear local copy.
TOKEN=""
PUSH_URL=""
unset TOKEN PUSH_URL 2>/dev/null || true

if [[ "$rc" -ne 0 ]]; then
  printf '\nFAIL: git push exited %s (token rejected, missing repo scope, or SSO not authorized).\n' "$rc" >&2
  printf 'Check: PAT has repo scope; for org SSO authorize the token; branch name allowed.\n' >&2
  printf 'Fallback: bash scripts/cloudpanel_export_final_gate_smoke_bundle.sh\n' >&2
  exit "$rc"
fi

printf '\nOK pushed %s\n' "$BRANCH"
printf 'Open a PR into main for the smoke artifacts, then add RELEASE_OWNER_APPROVAL.md only after human approval.\n'
printf 'Do NOT remove PHP.\n'
printf 'Suggested: unset GH_TOKEN\n'
