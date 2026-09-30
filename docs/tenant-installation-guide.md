# Tenant installation guide

This guide describes the intended cloud-led installation flow and the currently
available safe tranche. It keeps the PHP installer and PHP fallback intact until
the ASP.NET installer has passed live dual-sample, security, rollback, and human
acceptance gates.

## 1. Choose the deployment kind in the cloud

1. Sign in to the BOS/Control Plane as an administrator with the `bos` capability.
2. Create the tenant in the cloud control plane.
3. Select exactly one deployment kind:
   - `cloud` — the tenant runs in the hosted environment;
   - `on-premises` — the customer runs the application and database locally.
4. Record the tenant key. Use a stable lowercase key such as `customer-a`.
5. Do not enter an on-premises database password into the cloud manifest. The
   cloud must not receive or display the customer's local database credentials.

The deployment kind is part of the tenant installation identity and must remain
visible to operators.

## 2. Generate the on-premises setup manifest

The current guarded endpoint is:

```text
POST /bos/tenant-installations/manifest
```

It requires a BOS administrator session on an allowed Super-CP host. Submit a
short-lived enrollment request:

```json
{
  "tenantKey": "customer-a",
  "cloudBaseUrl": "https://control.ecomae.com",
  "packageVersion": "2026.09.1",
  "enrollmentRequestId": "one-time-request-id",
  "expiresAt": "2026-10-01T00:00:00+00:00"
}
```

The response contains:

- tenant key;
- cloud base URL;
- enrollment URL;
- package version;
- expiry;
- `deploymentKind: on-premises`.

It does not contain database credentials, a private signing key, or a reusable
cloud bearer token. Treat the enrollment request id as single-use and discard it
after redemption or expiry.

## 3. Prepare the customer server

Before running the local setup:

1. Use a supported Linux host with Docker and Docker Compose v2.
2. Confirm at least 4 GB RAM, 2 CPU cores, and 50 GB free disk for the current
   PHP installer baseline; production sizing may be higher.
3. Confirm outbound HTTPS access to the cloud control-plane URL.
4. Do not open an inbound administration port for cloud control.
5. Obtain the license key through the normal licensing process.
6. Copy the generated manifest to the server through an approved secure transfer.

The manifest is configuration metadata, not a replacement for license activation.

## 4. Run the current on-premises installer

The existing supported installer remains:

```bash
cd /path/to/ecomae/deploy/on-premises
./install.sh \
  --license 'LICENSE_KEY' \
  --domain 'erp.customer.example' \
  --dir '/opt/ecomae'
```

The installer currently:

1. checks Docker, Git, CPU, RAM, and disk;
2. creates the installation directory;
3. copies the deployment pack;
4. fetches the application reference;
5. writes local environment, database, PHP, Nginx, and TLS configuration;
6. starts the application, database, Redis, and scheduled-worker services;
7. runs the PHP license activation and setup wizard;
8. performs the local health check.

Never paste a database password, license key, or enrollment secret into a
public issue, pull request, or chat message.

## 5. Enroll the local installation

The ASP.NET control-plane tranche defines the next local step:

1. The local setup process reads the expiring manifest.
2. It creates an installation id locally.
3. It sends the manifest's one-time enrollment request over outbound HTTPS.
4. The cloud redeems the request once and returns an installation credential.
5. The local agent stores the credential in protected local storage.
6. The cloud records the tenant key, deployment kind, installation id, and
   enrollment time.

Enrollment redemption and durable storage are intentionally not enabled by the
current PR yet. Until that tranche lands, the manifest is a contract artifact and
the existing PHP activation/health paths remain authoritative.

## 6. Keep the cloud link alive

After enrollment, the local agent should send an authenticated heartbeat at a
bounded interval. The heartbeat should report only operational metadata:

- tenant key;
- installation id;
- software/package version;
- deployment kind;
- local health state;
- last successful synchronization time;
- timestamp.

The cloud should queue tenant-scoped control commands for the outbound agent to
consume. It must not require inbound network access to the customer system.
Every command needs authorization, an idempotency key, an audit record, and a
deny-by-default operation allow-list.

## 7. Synchronize data safely

Synchronization is two-way, but it is not unrestricted database replication.
Each envelope must include:

- envelope id;
- tenant key;
- installation id;
- entity type and entity id;
- monotonic version;
- direction (`cloud-to-onpremises` or `onpremises-to-cloud`);
- SHA-256 payload hash;
- creation timestamp.

Before applying an envelope, the transport must validate enrollment, tenant
scope, installation scope, replay status, authorization, version/conflict
policy, and audit requirements. Begin with an allow-list of explicitly approved
entities; do not replicate credentials, license material, or arbitrary tables.

## 8. Verify the installation

The operator verifies, in order:

1. local containers/services are healthy;
2. the local license is active;
3. the local tenant key matches the cloud record;
4. deployment kind is `on-premises`;
5. the cloud sees a recent heartbeat;
6. a no-write synchronization handshake succeeds;
7. a small approved entity sample compares identically in both directions;
8. audit records exist for enrollment and synchronization;
9. PHP fallback remains reachable for rollback.

Do not mark the tenant `Ready` from a manifest download alone.

## 9. Rollback and support

If enrollment, heartbeat, or synchronization fails:

1. stop the new control-plane agent;
2. preserve local logs and the installation id;
3. revoke the enrollment credential from the cloud;
4. keep local ERP operation on the existing PHP-supported path;
5. do not delete the local database;
6. retry only after the failure code and tenant scope are reviewed.

The existing PHP installer, license activation, setup wizard, health check, and
backup remain the fallback until formal cutover evidence closes those gates.
