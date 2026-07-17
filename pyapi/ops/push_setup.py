"""Create the push-device table. Ops-only DDL (never on the request path).

    python -m pyapi.ops.push_setup
"""

from __future__ import annotations

from .. import db

SCHEMA = """
CREATE TABLE IF NOT EXISTS `epc_push_devices` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `token` VARCHAR(512) NOT NULL,
  `platform` VARCHAR(16) NOT NULL DEFAULT 'android',
  `user_id` INT NOT NULL DEFAULT 0,
  `app` VARCHAR(32) NOT NULL DEFAULT 'cp',
  `updated_at` INT NOT NULL DEFAULT 0,
  UNIQUE KEY `token` (`token`(191)),
  KEY `app` (`app`),
  KEY `user` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4
"""


def ensure_schema() -> None:
    db.execute(SCHEMA)


if __name__ == "__main__":
    ensure_schema()
    print("epc_push_devices ready")
