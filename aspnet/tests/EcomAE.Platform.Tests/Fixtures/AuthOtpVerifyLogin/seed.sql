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
CREATE TABLE `users` (
  `user_id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `email` VARCHAR(120) NOT NULL DEFAULT '',
  `phone` VARCHAR(32) NOT NULL DEFAULT '',
  `email_confirmed` TINYINT(1) NOT NULL DEFAULT 0,
  `password` VARCHAR(255) NOT NULL DEFAULT '',
  `unlocked` TINYINT(1) NOT NULL DEFAULT 1,
  `reg_variant` INT NOT NULL DEFAULT 1,
  `time_registered` INT NOT NULL DEFAULT 0,
  `time_last_visit` INT NOT NULL DEFAULT 0,
  `ip_address` VARCHAR(45) NOT NULL DEFAULT '',
  `admin_created` TINYINT(1) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `users_profiles` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `user_id` INT NOT NULL,
  `data_key` VARCHAR(64) NOT NULL,
  `data_value` TEXT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `groups` (
  `id` INT UNSIGNED NOT NULL PRIMARY KEY,
  `value` VARCHAR(64) NOT NULL DEFAULT '',
  `for_registrated` TINYINT(1) NOT NULL DEFAULT 0,
  `for_backend` TINYINT(1) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
INSERT INTO `groups` (`id`, `value`, `for_registrated`, `for_backend`) VALUES (2, 'Customers', 1, 0), (3, 'Admins', 0, 1), (5, 'Managers', 0, 1);
CREATE TABLE `users_groups_bind` (
  `user_id` INT NOT NULL,
  `group_id` INT NOT NULL,
  UNIQUE KEY `bind` (`user_id`, `group_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `reg_variants` (
  `id` INT UNSIGNED NOT NULL PRIMARY KEY,
  `order` INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
INSERT INTO `reg_variants` (`id`, `order`) VALUES (4, 2), (7, 1);
CREATE TABLE `sessions` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `session` VARCHAR(64) NOT NULL DEFAULT '',
  `user_id` INT NOT NULL DEFAULT 0,
  `time` INT NOT NULL DEFAULT 0,
  `data` TEXT NULL,
  `type` INT NOT NULL DEFAULT 0,
  `contact_type` VARCHAR(16) NOT NULL DEFAULT '',
  `csrf_guard_key` VARCHAR(64) NOT NULL DEFAULT '',
  `last_activiti_time` INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `shop_carts` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `user_id` INT NOT NULL DEFAULT 0,
  `session_id` INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
