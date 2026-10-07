CREATE TABLE sessions (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL DEFAULT 0, csrf_guard_key VARCHAR(64) NOT NULL DEFAULT '', `2fa_session` VARCHAR(64) NULL);
INSERT INTO sessions (session, user_id, type, csrf_guard_key) VALUES ('cs', 5, 0, 'ck');
CREATE TABLE shop_users_accounting (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, time INT NOT NULL DEFAULT 0, income TINYINT NOT NULL DEFAULT 1, amount DECIMAL(10,2) NOT NULL, operation_code INT NULL, active TINYINT NOT NULL, order_id INT NOT NULL DEFAULT 0, office_id INT NULL, pay_orders VARCHAR(64) NULL);
INSERT INTO shop_users_accounting (id, user_id, amount, active, office_id, pay_orders) VALUES (40, 5, 150.00, 0, 1, '300'), (41, 5, 12.50, 0, 1, NULL), (42, 5, 99.00, 1, 1, NULL);
CREATE TABLE shop_payment_systems (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL DEFAULT '', handler VARCHAR(64) NOT NULL, anable TINYINT NOT NULL DEFAULT 0, active TINYINT NOT NULL DEFAULT 0, parameters_values TEXT NULL);
INSERT INTO shop_payment_systems (id, handler, anable, active, parameters_values) VALUES
(1, 'stripe', 1, 1, '{"demo_mode":true,"currency":"AED"}'),
(2, 'paypal', 1, 0, '{"demo_mode":"","currency":"USD"}'),
(3, 'nowpayments', 1, 0, '{"demo_mode":"1","allowed_coins":"btc, usdttrc20,XRP,doge,btc","ipn_secret":"sek"}');
CREATE TABLE lang_languages (id INT NOT NULL PRIMARY KEY, lang_code VARCHAR(8) NOT NULL, active TINYINT NOT NULL, is_default TINYINT NOT NULL);
INSERT INTO lang_languages (id, lang_code, active, is_default) VALUES (1, 'en', 1, 1);
CREATE TABLE lang_text_strings (str_key VARCHAR(64) NOT NULL, is_error TINYINT NOT NULL DEFAULT 0, same VARCHAR(8) NULL);
INSERT INTO lang_text_strings (str_key) VALUES ('4338'), ('4350'), ('4355');
CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NOT NULL);
INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES ('4338', 'en', 'Balance top-up'), ('4350', 'en', 'Payment for order "<300>"'), ('4355', 'en', 'Payment received & credited');
