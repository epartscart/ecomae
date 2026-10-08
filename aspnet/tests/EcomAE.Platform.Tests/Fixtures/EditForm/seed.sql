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
  `reg_variant` INT NULL DEFAULT 1,
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
  `caption` VARCHAR(64) NULL,
  `order` INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `reg_fields` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `main_flag` TINYINT(1) NOT NULL DEFAULT 0,
  `name` VARCHAR(64) NOT NULL,
  `caption` VARCHAR(64) NULL,
  `show_for` TEXT NULL,
  `required_for` TEXT NULL,
  `maxlen` INT NULL,
  `regexp` VARCHAR(255) NULL,
  `widget_type` VARCHAR(32) NULL,
  `widget_options` TEXT NULL,
  `order` INT NOT NULL DEFAULT 0
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
INSERT INTO `groups` (`id`, `value`, `for_guests`, `for_registrated`) VALUES (1, 'grp_guest', 1, 0), (2, 'grp_reg', 0, 1);
INSERT INTO `reg_variants` (`id`, `caption`, `order`) VALUES (1, 'rv_person', 1);
INSERT INTO `reg_fields` (`main_flag`, `name`, `caption`, `show_for`, `required_for`, `maxlen`, `regexp`, `widget_type`, `widget_options`, `order`) VALUES
  (1, 'email', 'rf_email', '[1,2]', '[1,2]', 120, '^.+@.+$', 'text', '{}', 0),
  (0, 'surname', 'rf_surname', '[1,2]', '[]', 64, '', 'text', '{}', 2),
  (0, 'name', 'rf_name', '[1,2]', '[1]', 64, '^[A-Za-z ]+$', 'text', '{"a":1}', 1),
  (0, 'company', NULL, '["2"]', '[2]', NULL, NULL, 'text', NULL, 3),
  (0, 'inn', 'rf_inn', '[2]', '[2]', 12, '^[0-9]{10,12}$', 'text', '[]', 4);
INSERT INTO `users` (`user_id`, `email`, `phone`, `email_confirmed`, `phone_confirmed`, `password`, `reg_variant`) VALUES
  (41, 'buyer@example.com', '+971500000001', 1, 0, 'old-hash', 1),
  (42, 'other@example.com', '', 1, 0, 'other-hash', 1);
INSERT INTO `sessions` (`id`, `session`, `user_id`, `time`, `data`, `csrf_guard_key`, `last_activiti_time`) VALUES
  (11, 'sess41', 41, 1, '', 'ck41', UNIX_TIMESTAMP());
INSERT INTO `users_profiles` (`user_id`, `data_key`, `data_value`) VALUES
  (41, 'name', 'Amal'), (41, 'surname', 'Haddad &amp; Co'), (42, 'name', 'Other');
