# CloudPanel Missing Repository Recovery

Use this when the CloudPanel shell prints:

```text
ECOMAE repo not found on this server. Clone it first, then re-run this block.
```

That message means the ASP.NET migration repository is not deployed on the server yet. The server cannot run `scripts/preflight_aspnet_production.sh` until the repository is cloned or copied there.

## Paste-safe clone block

Set the two values first. Use the approved Git URL and release branch or tag from your deployment process.

```bash
export ECOMAE_GIT_URL="<REPO_URL>"
export ECOMAE_GIT_REF="<APPROVED_BRANCH_OR_TAG>"
```

Then run:

```bash
if [ -z "$ECOMAE_GIT_URL" ] || [ "$ECOMAE_GIT_URL" = "<REPO_URL>" ]; then
  echo "Set ECOMAE_GIT_URL to the real repository URL first" >&2
  exit 1
fi
if [ -z "$ECOMAE_GIT_REF" ] || [ "$ECOMAE_GIT_REF" = "<APPROVED_BRANCH_OR_TAG>" ]; then
  echo "Set ECOMAE_GIT_REF to the approved branch or tag first" >&2
  exit 1
fi
mkdir -p /opt
cd /opt
if [ ! -d ecomae-aspnet-source/.git ]; then
  git clone "$ECOMAE_GIT_URL" ecomae-aspnet-source
fi
cd /opt/ecomae-aspnet-source
git fetch --all --tags --prune
git checkout "$ECOMAE_GIT_REF"
test -f scripts/preflight_aspnet_production.sh && echo "preflight script found in $(pwd)"
```

## Continue after clone

```bash
bash tests/aspnet_migration/run_detailed_foundation_tests.sh
bash scripts/preflight_aspnet_production.sh
```

If preflight reports missing .NET, install the .NET SDK/runtime required by `aspnet/global.json` before running deploy.
