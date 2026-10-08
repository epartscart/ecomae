CREATE TABLE lang_text_strings (str_key VARCHAR(64) NOT NULL, is_error INT NOT NULL DEFAULT 0, same VARCHAR(8) NULL);
CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NULL) DEFAULT CHARSET=utf8mb4;
INSERT INTO lang_text_strings (str_key) VALUES ('2122'), ('4041'), ('4745'), ('4746'), ('4740'), ('3912'), ('3913'), ('4747'), ('4696'), ('4697'), ('4698'), ('4748'), ('1312'), ('3664'), ('3539'), ('4749'), ('4750'), ('4708'), ('4521'), ('4003'), ('5642'), ('5643'), ('cap_name'), ('cap_company');
INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
('2122', 'en', 'Registration error'), ('4041', 'en', 'Wrong captcha'), ('4745', 'en', 'Accept the user agreement'),
('4746', 'en', 'Too many registrations from your address'), ('3912', 'en', 'Could not create the account'), ('3913', 'en', 'Could not save the profile'),
('4747', 'en', 'Could not bind the group'), ('4696', 'en', 'Confirm e-mail'), ('4697', 'en', 'Notification error'), ('4698', 'en', 'Could not send the confirmation'),
('4748', 'en', 'New user'), ('1312', 'en', 'Phone'), ('3664', 'en', 'Group'), ('3539', 'en', 'Open profile'),
('4749', 'en', '<p>Registered. Check your e-mail.</p>'), ('4750', 'en', 'Enter the code from SMS'), ('4708', 'en', 'Code'), ('4521', 'en', 'Send'),
('4003', 'en', 'No attempts left'), ('5642', 'en', 'The code has expired'), ('5643', 'en', 'Wrong code, attempts left'),
('cap_name', 'en', 'First name'), ('cap_company', 'en', 'Company');
CREATE TABLE lang_languages (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, lang_code VARCHAR(8) NOT NULL, active INT NOT NULL DEFAULT 1, is_default INT NOT NULL DEFAULT 0, restrict_edit INT NOT NULL DEFAULT 0);
INSERT INTO lang_languages (lang_code, active, is_default) VALUES ('en', 1, 1);
CREATE TABLE `groups` (id INT NOT NULL PRIMARY KEY, value VARCHAR(64) NOT NULL DEFAULT '', parent INT NOT NULL DEFAULT 0, `count` INT NOT NULL DEFAULT 0, for_guests INT NOT NULL DEFAULT 0, for_registrated INT NOT NULL DEFAULT 0, for_backend INT NOT NULL DEFAULT 0) DEFAULT CHARSET=utf8mb4;
INSERT INTO `groups` (id, value, parent, for_guests, for_registrated, for_backend) VALUES (1, 'Admins', 0, 0, 0, 1), (4, 'Managers', 1, 0, 0, 0), (2, 'Guests', 0, 1, 0, 0), (3, 'Customers', 0, 0, 1, 0), (21, 'Retail', 0, 0, 0, 0), (22, 'Wholesale', 0, 0, 0, 0);
CREATE TABLE users (user_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, email VARCHAR(255) NOT NULL DEFAULT '', phone VARCHAR(64) NOT NULL DEFAULT '', reg_variant INT NOT NULL DEFAULT 0, password VARCHAR(64) NOT NULL DEFAULT '', email_code VARCHAR(64) NOT NULL DEFAULT '', phone_code VARCHAR(64) NOT NULL DEFAULT '', email_code_expired INT NOT NULL DEFAULT 0, phone_code_expired INT NOT NULL DEFAULT 0, email_code_send_lock_expired INT NOT NULL DEFAULT 0, phone_code_send_lock_expired INT NOT NULL DEFAULT 0, time_registered INT NOT NULL DEFAULT 0, unlocked INT NOT NULL DEFAULT 0, email_confirmed INT NOT NULL DEFAULT 0, phone_confirmed INT NOT NULL DEFAULT 0, ip_address VARCHAR(45) NOT NULL DEFAULT '') DEFAULT CHARSET=utf8mb4;
INSERT INTO users (user_id, email, phone, time_registered, email_confirmed, phone_confirmed) VALUES (1, 'admin@example.test', '', 1767225601, 1, 0), (5, 'manager@example.test', '', 1767225605, 1, 0), (7, 'taken@example.test', '+971500000007', 1767225607, 1, 1);
CREATE TABLE users_profiles (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, data_key VARCHAR(64) NOT NULL, data_value TEXT NULL) DEFAULT CHARSET=utf8mb4;
CREATE TABLE users_groups_bind (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, group_id INT NOT NULL);
INSERT INTO users_groups_bind (user_id, group_id) VALUES (1, 1), (5, 4), (7, 3);
CREATE TABLE sessions (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL DEFAULT 0, data TEXT NULL, `2fa_code` VARCHAR(16) NULL, `2fa_attempts` INT NOT NULL DEFAULT 0, csrf_guard_key VARCHAR(64) NOT NULL DEFAULT '');
INSERT INTO sessions (session, user_id, data, `2fa_code`, `2fa_attempts`) VALUES ('sg-ok', 0, '{"expireFaCode":4102444800}', '4321', 3), ('sg-late', 0, '{"expireFaCode":1000}', '4321', 3), ('sg-none', 0, '{}', '4321', 0), ('s7', 7, NULL, NULL, 0);
CREATE TABLE reg_variants (id INT NOT NULL PRIMARY KEY, caption VARCHAR(64) NOT NULL DEFAULT '');
INSERT INTO reg_variants (id, caption) VALUES (1, 'Customer'), (2, 'Company');
CREATE TABLE reg_fields (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, name VARCHAR(64) NOT NULL, caption VARCHAR(64) NOT NULL DEFAULT '', `regexp` VARCHAR(255) NOT NULL DEFAULT '', main_flag INT NOT NULL DEFAULT 0, show_for VARCHAR(255) NULL, `order` INT NOT NULL DEFAULT 0, widget_type VARCHAR(16) NULL, field_category VARCHAR(32) NULL) DEFAULT CHARSET=utf8mb4;
INSERT INTO reg_fields (name, caption, `regexp`, main_flag, show_for, `order`, widget_type, field_category) VALUES
('email', 'E-mail', '[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\\.[a-z]{2,}', 1, '[1,2]', 1, 'text', NULL),
('phone', 'Phone', '\\+?[0-9 ]{7,16}', 1, '[1,2]', 2, 'text', NULL),
('surname', 'Surname', '', 0, '["1","2"]', 4, 'text', NULL),
('name', 'cap_name', '', 0, '[1,2]', 3, NULL, NULL),
('company_name', 'cap_company', '', 0, '[2]', 5, 'text', 'business'),
('epc_reg_note', 'Note', '', 0, '{"a":"1"}', 6, 'text', NULL),
('licence_scan', 'Licence', '', 0, '[1,2]', 7, 'file', 'documents'),
('city', 'City', '', 0, '[" 2"]', 8, 'text', 'identity'),
('broken', 'Broken', '', 0, 'not json', 9, 'text', NULL);
CREATE TABLE templates (id INT NOT NULL PRIMARY KEY, is_frontend INT NOT NULL, current INT NOT NULL, data_value TEXT NULL);
INSERT INTO templates (id, is_frontend, current, data_value) VALUES (1, 1, 1, '{"main_color":"#c00"}'), (2, 0, 1, '{"main_color":"#000"}');
CREATE TABLE epc_price_profiles (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, code VARCHAR(32) NOT NULL, group_id INT NULL);
INSERT INTO epc_price_profiles (code, group_id) VALUES ('retail', 21), ('wholesale', 22);
CREATE TABLE shop_currencies (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, iso_code VARCHAR(8) NOT NULL, iso_name VARCHAR(16) NOT NULL DEFAULT '', caption_short VARCHAR(16) NOT NULL DEFAULT '', sign VARCHAR(16) NOT NULL DEFAULT '', rate DECIMAL(14,6) NOT NULL DEFAULT 1, available TINYINT NOT NULL DEFAULT 0, `order` INT NOT NULL DEFAULT 0) DEFAULT CHARSET=utf8mb4;
INSERT INTO shop_currencies (iso_code, iso_name, caption_short, sign, rate, available, `order`) VALUES ('784', 'AED', 'AED', 'AED', 1, 1, 1);
