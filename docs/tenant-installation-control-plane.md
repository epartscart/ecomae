# Tenant installation and control-plane contract

This is the first implementation tranche for guided cloud and on-premises tenant
installation. It establishes the contract; it does not claim that database
replication, remote command execution, or production cutover is complete.

For the operator-facing procedure, see
`docs/tenant-installation-guide.md`.

## Installation flow

Every tenant starts in the cloud control plane:

1. `Requested` — an operator creates the tenant and selects `Cloud` or `OnPremises`.
2. `ProvisioningCloudTenant` — the cloud allocates the tenant registry record and
   tenant-scoped resources.
3. `PackageReady` — an expiring setup manifest is generated for an on-premises tenant.
4. `AwaitingLocalExecution` — the operator downloads and runs the setup package on
   the customer system.
5. `EnrollingInstallation` — the local installation makes an outbound HTTPS request
   to the cloud enrollment endpoint.
6. `Synchronizing` — the initial tenant-scoped synchronization is authorized and
   replay-safe.
7. `Ready` — the cloud can display the deployment kind, installation identity,
   heartbeat, and last synchronization timestamps.

The progress percentages are part of the contract: 0, 20, 40, 50, 70, 85, and
100. Failed installations return to 0% with a stable failure code and operator
message; credentials must never be placed in that message.

## On-premises package boundary

The guarded BOS operator endpoint
`POST /bos/tenant-installations/manifest` now generates the manifest contract
after the operator session and Super-CP host gate pass. It intentionally reports
`writes=0` in this tranche: durable tenant installation records, one-time request
redemption, and package archive delivery still require the next persistence slice.

The generated manifest contains only:

- tenant key;
- cloud HTTPS base URL;
- enrollment endpoint;
- one-time enrollment request id;
- package version;
- expiry timestamp;
- deployment kind.

It must not contain a database password, cloud API secret, reusable bearer token,
or private signing key. The enrollment request id is single-use and expires. The
cloud service must store only a hash after issuance and return the raw value only
in the authorized download response.

## Persistent cloud link

The local installation uses outbound HTTPS polling/heartbeat. The cloud does not
need an inbound administrative port on the customer network. A future control
command is queued in the cloud, scoped to the tenant and installation id, and
consumed by the local agent after authentication and authorization. Commands are
audited, idempotent, and denied by default.

## Two-way synchronization boundary

Synchronization envelopes are tenant-scoped and include an envelope id, installation
id, entity identity, monotonic version, direction, and SHA-256 payload hash.
Before applying an envelope, the future transport must enforce:

- enrollment authentication;
- tenant and installation scope;
- replay/idempotency checks;
- monotonic version or explicit conflict policy;
- authorization for the entity type and operation;
- audit logging without payload secrets.

This tranche validates the envelope shape only. It intentionally does not write
business data or claim that arbitrary database replication is safe.

## Existing fallback

The PHP on-premises installer, license activation, setup wizard, health checks, and
backup remain authoritative until live dual-sample, security, rollback, and human
acceptance evidence exists. The existing `deploy/on-premises` package is therefore
not removed or silently replaced.
