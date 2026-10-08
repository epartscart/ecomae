CREATE TABLE `users` (
  `user_id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `email` VARCHAR(120) NOT NULL DEFAULT '',
  `phone` VARCHAR(32) NOT NULL DEFAULT '',
  `email_confirmed` TINYINT(1) NOT NULL DEFAULT 0,
  `phone_confirmed` TINYINT(1) NOT NULL DEFAULT 0,
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
CREATE TABLE `users_options` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `session_id` INT NOT NULL DEFAULT 0,
  `data` TEXT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `sessions` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `session` VARCHAR(64) NOT NULL DEFAULT '',
  `user_id` INT NOT NULL DEFAULT 0,
  `time` INT NOT NULL DEFAULT 0,
  `data` TEXT NULL,
  `csrf_guard_key` VARCHAR(64) NOT NULL DEFAULT '',
  `last_activiti_time` INT NOT NULL DEFAULT 0,
  `2fa_code` VARCHAR(16) NULL,
  `2fa_attempts` INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
CREATE TABLE `shop_carts` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT PRIMARY KEY,
  `user_id` INT NOT NULL DEFAULT 0,
  `session_id` INT NOT NULL DEFAULT 0
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
INSERT INTO `users` (`user_id`, `email`, `phone`, `email_confirmed`, `phone_confirmed`, `password`, `unlocked`) VALUES
  (41, 'buyer@example.com', '+971500000001', 1, 1, '$2y$04$ycxhzOEmpcjx3ttv0sveIO/XrDCEDTeKD2ehU81VsdMS/IS7Iun6S', 1),
  (42, 'legacy@example.com', '', 1, 0, 'ee999d68ff0f146c0c750738319ae843', 1),
  (43, 'pending@example.com', '', 0, 0, '$2y$04$ycxhzOEmpcjx3ttv0sveIO/XrDCEDTeKD2ehU81VsdMS/IS7Iun6S', 1),
  (44, 'locked@example.com', '', 1, 0, '$2y$04$ycxhzOEmpcjx3ttv0sveIO/XrDCEDTeKD2ehU81VsdMS/IS7Iun6S', 0);
INSERT INTO `sessions` (`id`, `session`, `user_id`, `time`, `data`, `csrf_guard_key`, `last_activiti_time`) VALUES
  (9, 'guestsess', 0, 1, '', 'ck1', UNIX_TIMESTAMP() - 60),
  (10, 'oldguest', 0, 1, '', 'ck0', 1000),
  (11, 'oldbuyer', 41, 1, '', 'ck9', 1000);
INSERT INTO `users_options` (`id`, `session_id`) VALUES (1, 10), (2, 11), (3, 9);
INSERT INTO `shop_carts` (`id`, `user_id`, `session_id`) VALUES (1, 0, 9), (2, 0, 9), (3, 0, 8);
