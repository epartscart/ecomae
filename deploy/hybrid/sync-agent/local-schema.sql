-- Site-side hybrid change-log. Control-plane only — not a copy of shop tables.
CREATE TABLE IF NOT EXISTS epc_hybrid_sync_outbox (
  id BIGINT NOT NULL AUTO_INCREMENT,
  envelope_id VARCHAR(80) NOT NULL,
  entity_type VARCHAR(80) NOT NULL,
  entity_id VARCHAR(80) NOT NULL,
  version BIGINT NOT NULL,
  payload_json MEDIUMTEXT NOT NULL,
  payload_hash CHAR(64) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'queued',
  created_at DATETIME NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_hybrid_outbox (envelope_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS epc_hybrid_sync_inbox (
  id BIGINT NOT NULL AUTO_INCREMENT,
  envelope_id VARCHAR(80) NOT NULL,
  entity_type VARCHAR(80) NOT NULL,
  entity_id VARCHAR(80) NOT NULL,
  version BIGINT NOT NULL,
  payload_json MEDIUMTEXT NOT NULL,
  payload_hash CHAR(64) NOT NULL,
  status VARCHAR(20) NOT NULL DEFAULT 'received',
  created_at DATETIME NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_hybrid_inbox (envelope_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS epc_hybrid_sync_conflicts (
  id BIGINT NOT NULL AUTO_INCREMENT,
  envelope_id VARCHAR(80) NOT NULL,
  entity_type VARCHAR(80) NOT NULL,
  entity_id VARCHAR(80) NOT NULL,
  reason VARCHAR(80) NOT NULL,
  detail TEXT NULL,
  queued_at DATETIME NOT NULL,
  resolution VARCHAR(20) NOT NULL DEFAULT 'queued',
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS epc_hybrid_sync_cursor (
  id BIGINT NOT NULL AUTO_INCREMENT,
  direction VARCHAR(40) NOT NULL,
  last_version BIGINT NOT NULL DEFAULT 0,
  last_envelope_id VARCHAR(80) NOT NULL DEFAULT '',
  updated_at DATETIME NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uq_hybrid_local_cursor (direction)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
