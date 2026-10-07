CREATE TABLE sessions (
  id INT NOT NULL PRIMARY KEY,
  session VARCHAR(64) NOT NULL,
  user_id INT NOT NULL,
  type INT NOT NULL DEFAULT 0,
  csrf_guard_key VARCHAR(64) NOT NULL DEFAULT ''
);
CREATE TABLE users_profiles (
  user_id INT NOT NULL,
  data_key VARCHAR(64) NOT NULL,
  data_value VARCHAR(255) NOT NULL
);
CREATE TABLE shop_orders (
  id INT NOT NULL PRIMARY KEY,
  user_id INT NOT NULL
);
CREATE TABLE shop_orders_items_statuses_ref (
  id INT NOT NULL PRIMARY KEY,
  check_for_return INT NOT NULL DEFAULT 0
);
CREATE TABLE shop_orders_items (
  id INT NOT NULL PRIMARY KEY,
  order_id INT NOT NULL,
  product_type INT NOT NULL DEFAULT 2,
  product_id INT NOT NULL DEFAULT 0,
  status INT NOT NULL DEFAULT 0,
  price DECIMAL(10,2) NOT NULL DEFAULT 0,
  count_need INT NOT NULL DEFAULT 0,
  t2_name VARCHAR(255) NULL,
  t2_article VARCHAR(64) NULL,
  t2_manufacturer VARCHAR(64) NULL,
  t2_time_to_exe INT NOT NULL DEFAULT 0,
  t2_time_to_exe_guaranteed INT NOT NULL DEFAULT 0,
  t2_office_id INT NOT NULL DEFAULT 0
);
CREATE TABLE shop_orders_returns_statuses (
  id INT NOT NULL PRIMARY KEY,
  caption VARCHAR(64) NOT NULL,
  color VARCHAR(16) NOT NULL DEFAULT ''
);
CREATE TABLE shop_orders_returns (
  id INT NOT NULL PRIMARY KEY,
  status_id INT NULL,
  user_id INT NULL,
  `sum` DECIMAL(12,2) NULL
);
CREATE TABLE shop_orders_returns_reasons (
  id INT NOT NULL PRIMARY KEY,
  caption VARCHAR(255) NOT NULL
);
CREATE TABLE shop_orders_returns_items (
  id INT NOT NULL PRIMARY KEY,
  comment VARCHAR(255) NULL,
  reason_id INT NULL,
  return_id INT NULL,
  item_id INT NULL,
  count_need VARCHAR(32) NULL,
  return_success INT NULL
);
CREATE TABLE shop_orders_returns_items_images (
  id INT NOT NULL PRIMARY KEY,
  return_item_id INT NOT NULL,
  image VARCHAR(512) NOT NULL
);
CREATE TABLE shop_orders_messages (
  id INT NOT NULL PRIMARY KEY,
  order_id INT NOT NULL DEFAULT 0,
  return_id INT NOT NULL DEFAULT 0,
  is_customer INT NOT NULL DEFAULT 0,
  `read` INT NOT NULL DEFAULT 0,
  text TEXT NOT NULL
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
INSERT INTO sessions (id, session, user_id, csrf_guard_key) VALUES
  (1, 'tok-7', 7, 'csrf-seven'),
  (2, 'tok-8', 8, 'csrf-eight');
INSERT INTO users_profiles (user_id, data_key, data_value) VALUES
  (7, 'name', 'Amir'), (7, 'surname', 'Khan'), (8, 'name', 'Lea');
INSERT INTO shop_orders (id, user_id) VALUES (40, 7), (41, 8), (42, 7);
INSERT INTO shop_orders_items_statuses_ref (id, check_for_return) VALUES (10, 1), (20, 0);
INSERT INTO shop_orders_items (id, order_id, status, price, count_need, t2_name, t2_article, t2_manufacturer, t2_time_to_exe, t2_time_to_exe_guaranteed, t2_office_id) VALUES
  (90, 40, 10, 15.00, 5, 'Oil filter "XL" & <seal>', 'OC90', 'MAHLE', 2, 4, 3),
  (91, 40, 10, 1234.5, 2, 'Brake pad/set', 'P-91', 'Brembo', 3, 3, 3),
  (92, 40, 20, 9.99, 1, 'Wiper', 'W92', 'Bosch', 1, 1, 3),
  (93, 40, 10, 7.10, 3, 'Spark plug', 'SP93', 'NGK', 1, 2, 3),
  (94, 40, 10, 0.10, 3, 'Clip', 'C94', NULL, 0, 0, 3),
  (95, 41, 10, 50.00, 1, 'Mirror', 'M95', 'Valeo', 5, 5, 4),
  (96, 42, 10, 1000000.25, 1, 'Engine', 'E96', 'Toyota', 9, 12, 5),
  (97, 41, 10, 3.00, 1, 'Bulb', 'B97', 'Osram', 1, 1, 4);
INSERT INTO shop_orders_returns_statuses (id, caption, color) VALUES
  (1, 'New', '#fcf8e3'), (2, 'Closed "done"', '#dff0d8');
INSERT INTO shop_orders_returns (id, status_id, user_id, `sum`) VALUES
  (1, 1, 7, 21.60), (2, 2, 7, 1000000.25), (3, 1, 7, 0), (4, 1, 8, 50.00);
INSERT INTO shop_orders_returns_reasons (id, caption) VALUES
  (1, 'Wrong part'), (2, 'Damaged <box> & "worn"');
INSERT INTO shop_orders_returns_items (id, comment, reason_id, return_id, item_id, count_need, return_success) VALUES
  (11, 'Does not fit <b>OEM</b>', 1, 1, 93, '3', NULL),
  (12, 'Broken', 2, 1, 94, '3', 1),
  (13, NULL, 99, 2, 96, '1', 0),
  (14, 'Ghost', 1, 2, 999, '1', 1),
  (15, 'Other', 1, 4, 95, '1', NULL);
INSERT INTO shop_orders_returns_items_images (id, return_item_id, image) VALUES
  (1, 11, '/tmp/ecomae_returns_fixture_photo.png'),
  (2, 11, '/tmp/ecomae_returns_fixture_missing.png');
INSERT INTO shop_orders_messages (id, order_id, return_id, is_customer, `read`, text) VALUES
  (1, 0, 1, 0, 0, 'Please send photos'),
  (2, 0, 1, 0, 0, 'Reminder'),
  (3, 0, 1, 1, 0, 'Sent'),
  (4, 0, 2, 0, 1, 'Closed');
INSERT INTO lang_text_strings (str_key, is_error, same) VALUES ('4583', 0, NULL);
INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
  ('4582', 'en', 'Sign in to see your returns'),
  ('4583', 'en', 'Search "returns"'),
  ('2081', 'en', 'Status'),
  ('3244', 'en', 'Order'),
  ('3811', 'en', 'Total'),
  ('3802', 'en', 'Return request'),
  ('3803', 'en', 'Return status'),
  ('3498', 'en', 'Order items'),
  ('2070', 'en', 'Manufacturer'),
  ('2071', 'en', 'Article'),
  ('2102', 'en', 'Name'),
  ('2751', 'en', 'Price'),
  ('3251', 'en', 'Sum'),
  ('3804', 'en', 'Return accepted'),
  ('3805', 'en', 'Return rejected'),
  ('3806', 'en', 'Under review'),
  ('3807', 'en', 'Reason'),
  ('4581', 'en', 'Comment'),
  ('5691', 'en', 'Return not found'),
  ('4548', 'en', 'Messages'),
  ('4549', 'en', 'New message'),
  ('3211', 'en', 'Send'),
  ('4550', 'en', 'You'),
  ('3565', 'en', 'Manager'),
  ('3566', 'en', 'No messages'),
  ('3567', 'en', 'Enter a message'),
  ('3568', 'en', 'Message not sent'),
  ('5684', 'en', 'Returns are disabled'),
  ('5685', 'en', 'These items cannot be returned'),
  ('4579', 'en', 'Choose a reason'),
  ('4580', 'en', 'Photos'),
  ('3571', 'en', 'Comment'),
  ('4527', 'en', 'Request a return'),
  ('4924', 'en', 'Only PNG, JPEG or BMP'),
  ('4925', 'en', 'Each photo up to 5 MB'),
  ('4926', 'en', 'All photos up to 15 MB'),
  ('3127', 'en', 'Fill in all fields'),
  ('ret_note', 'en', 'A 10% restocking fee applies');
