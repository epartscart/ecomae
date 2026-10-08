CREATE TABLE `epc_portal_tenants` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `site_key` VARCHAR(64) NOT NULL DEFAULT '',
  `hostname` VARCHAR(190) NOT NULL DEFAULT '',
  `trade_name` VARCHAR(190) NOT NULL DEFAULT '',
  `is_demo` TINYINT(1) NOT NULL DEFAULT 0,
  `db_name` VARCHAR(64) NOT NULL DEFAULT '',
  `db_user` VARCHAR(64) NOT NULL DEFAULT '',
  `db_password` VARCHAR(255) NOT NULL DEFAULT '',
  `industry_code` VARCHAR(32) NOT NULL DEFAULT 'auto_parts',
  `status` VARCHAR(24) NOT NULL DEFAULT 'live',
  `hub_name` VARCHAR(120) NOT NULL DEFAULT '',
  `from_email` VARCHAR(120) NOT NULL DEFAULT '',
  `notes` VARCHAR(500) NOT NULL DEFAULT '',
  `intro_json` TEXT NULL,
  `hosted_on` VARCHAR(24) NOT NULL DEFAULT 'client',
  `erp_only_shared` TINYINT(1) NOT NULL DEFAULT 0,
  `created_at` INT NOT NULL DEFAULT 0,
  `updated_at` INT NOT NULL DEFAULT 0,
  UNIQUE KEY `site_key` (`site_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
INSERT INTO `epc_portal_tenants` (`site_key`, `hostname`, `trade_name`, `is_demo`, `db_name`, `db_user`, `db_password`) VALUES
  ('acme', 'acme.example', 'Acme Parts', 0, '@DB@', 'ecomae', '@PW@'),
  ('demo_acme', 'demo-acme.example', 'Acme Demo', 1, '@DB@', 'ecomae', '@PW@'),
  ('nodb', 'nodb.example', 'No Db', 0, '', '', '');
