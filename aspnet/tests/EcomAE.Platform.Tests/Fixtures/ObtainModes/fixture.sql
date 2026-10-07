CREATE TABLE shop_offices (
  id INT NOT NULL PRIMARY KEY,
  caption VARCHAR(255) NOT NULL DEFAULT '',
  city VARCHAR(255) NOT NULL DEFAULT '',
  address VARCHAR(255) NOT NULL DEFAULT '',
  timetable VARCHAR(255) NOT NULL DEFAULT '',
  phone VARCHAR(64) NOT NULL DEFAULT '',
  coordinates VARCHAR(64) NOT NULL DEFAULT ''
);
CREATE TABLE lang_text_strings (
  str_key VARCHAR(128) NOT NULL PRIMARY KEY,
  is_error INT NOT NULL DEFAULT 0,
  same VARCHAR(8) NULL
);
CREATE TABLE lang_text_strings_translation (
  str_key VARCHAR(128) NOT NULL,
  lang_code VARCHAR(8) NOT NULL,
  value TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS `epc_carrier_shipments` (
  `id` INT UNSIGNED NOT NULL AUTO_INCREMENT,
  `order_id` INT UNSIGNED NOT NULL,
  `carrier_code` VARCHAR(32) NOT NULL,
  `service_code` VARCHAR(64) DEFAULT NULL,
  `tracking_number` VARCHAR(64) DEFAULT NULL,
  `label_url` VARCHAR(512) DEFAULT NULL,
  `status` VARCHAR(32) NOT NULL DEFAULT 'draft',
  `weight_kg` DECIMAL(8,3) NOT NULL DEFAULT 0,
  `cost` DECIMAL(12,2) NOT NULL DEFAULT 0,
  `currency` VARCHAR(8) NOT NULL DEFAULT 'AED',
  `recipient_json` TEXT,
  `raw_json` TEXT,
  `shipped_at` INT UNSIGNED NOT NULL DEFAULT 0,
  `time_created` INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `order_id` (`order_id`)
);
INSERT INTO shop_offices (id, caption, city, address, timetable, phone, coordinates) VALUES
  (1, 'off_cap', 'city_dxb', 'addr_1', 'tt_1', '+971 4 <b>123</b>', '25.13, 55.22');
INSERT INTO lang_text_strings (str_key, is_error, same) VALUES ('3507', 0, NULL);
INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
  ('3507', 'en', 'How to obtain'),
  ('4418', 'en', 'Office info'),
  ('3376', 'en', 'Address'),
  ('4445', 'en', 'Show on map'),
  ('4446', 'en', 'Timetable'),
  ('1312', 'en', 'Phone'),
  ('om_office', 'en', 'Pickup <b>office</b>'),
  ('om_carriers', 'en', 'Courier & "delivery"'),
  ('city_dxb', 'en', 'Dubai'),
  ('addr_1', 'en', 'Al Quoz 3, "Block" A'),
  ('tt_1', 'en', 'Mon-Fri 9-18\nSat 10-14'),
  ('off_cap', 'en', 'Main office');
INSERT INTO epc_carrier_shipments (order_id, carrier_code, tracking_number, label_url, status, cost, currency) VALUES
  (41, 'aramex', 'AR123', '', 'created', 49.25, 'AED'),
  (41, 'dhl', 'DH<9>', 'https://labels.example/l?a=1&b=2', 'shipped', 1234.5, 'USD');
