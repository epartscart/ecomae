CREATE TABLE `users` (
  `user_id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `email` VARCHAR(120) NOT NULL DEFAULT '',
  `phone` VARCHAR(32) NOT NULL DEFAULT '',
  `email_confirmed` TINYINT(1) NOT NULL DEFAULT 0,
  `email_code_send_lock_expired` INT NOT NULL DEFAULT 0,
  `phone_confirmed` TINYINT(1) NOT NULL DEFAULT 0,
  `phone_code_send_lock_expired` INT NOT NULL DEFAULT 0,
  `password` VARCHAR(255) NOT NULL DEFAULT '',
  `unlocked` TINYINT(1) NOT NULL DEFAULT 1,
  `reg_variant` INT NOT NULL DEFAULT 1,
  `time_registered` INT NOT NULL DEFAULT 0,
  `time_last_visit` INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `users_profiles` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `user_id` INT NOT NULL,
  `data_key` VARCHAR(64) NOT NULL,
  `data_value` TEXT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `users_groups_bind` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `user_id` INT NOT NULL,
  `group_id` INT NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `groups` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `value` VARCHAR(64) NULL,
  `for_guests` TINYINT(1) NOT NULL DEFAULT 0,
  `for_registrated` TINYINT(1) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `reg_variants` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `caption` VARCHAR(64) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `reg_fields` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `name` VARCHAR(64) NOT NULL,
  `caption` VARCHAR(64) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `sessions` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `session` VARCHAR(64) NOT NULL DEFAULT '',
  `user_id` INT NOT NULL DEFAULT 0,
  `time` INT NOT NULL DEFAULT 0,
  `data` TEXT NULL,
  `csrf_guard_key` VARCHAR(64) NOT NULL DEFAULT '',
  `last_activiti_time` INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `sms_api` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `active` TINYINT(1) NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `shop_currencies` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `iso_code` VARCHAR(8) NOT NULL,
  `iso_name` VARCHAR(16) NOT NULL DEFAULT '',
  `caption_short` VARCHAR(32) NOT NULL DEFAULT '',
  `sign` VARCHAR(16) NOT NULL DEFAULT '',
  `rate` DECIMAL(14,6) NOT NULL DEFAULT 1,
  `available` TINYINT(1) NOT NULL DEFAULT 0,
  `order` INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `epc_price_settings` (
  `setting_key` VARCHAR(64) NOT NULL PRIMARY KEY,
  `setting_value` TEXT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
INSERT INTO `groups` (`id`, `value`, `for_guests`, `for_registrated`) VALUES (1, 'grp_guest', 1, 0), (2, 'grp_reg', 0, 1), (3, 'grp_whole', 0, 0);
INSERT INTO `reg_variants` (`id`, `caption`) VALUES (1, 'rv_person');
INSERT INTO `reg_fields` (`name`, `caption`) VALUES ('name', 'rf_name'), ('surname', 'rf_surname'), ('company', NULL);
INSERT INTO `shop_currencies` (`iso_code`, `iso_name`, `caption_short`, `sign`, `rate`, `available`, `order`) VALUES ('784', 'AED', 'Dirham', 'AED', 1, 1, 1);
INSERT INTO `users` (`user_id`, `email`, `phone`, `email_confirmed`, `phone_confirmed`, `reg_variant`) VALUES
  (41, 'buyer@example.com', '+971500000001', 1, 0, 1);
INSERT INTO `sessions` (`id`, `session`, `user_id`, `time`, `data`, `csrf_guard_key`, `last_activiti_time`) VALUES
  (11, 'sess41', 41, 1, '', 'ck41', UNIX_TIMESTAMP());
INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES
  (41, 'name', 'Amal'), (41, 'surname', 'Haddad &amp; Co'), (41, 'company', 'Haddad Parts');
