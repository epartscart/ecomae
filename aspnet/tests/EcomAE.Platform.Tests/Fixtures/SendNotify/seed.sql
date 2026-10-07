CREATE TABLE lang_text_strings (str_key VARCHAR(64) NOT NULL, is_error INT NOT NULL DEFAULT 0, same VARCHAR(8) NULL);
CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NULL);
INSERT INTO lang_text_strings (str_key) VALUES ('4072'), ('4074'), ('4075'), ('4077'), ('n1_subject'), ('n1_body'), ('n1_sms');
INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
('4072', 'en', 'No input data'), ('4074', 'en', 'Notification not found'), ('4075', 'en', 'SMS sent'), ('4077', 'en', 'No SMS operator'),
('n1_subject', 'en', 'Hi %name%'), ('n1_body', 'en', '<p>Hello %name%, %missing%</p>'), ('n1_sms', 'en', 'Hello %name%');
CREATE TABLE notifications_settings (
  id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  name VARCHAR(64) NOT NULL,
  email_on INT NOT NULL DEFAULT 0,
  sms_on INT NOT NULL DEFAULT 0,
  send_for_not_confirmed INT NOT NULL DEFAULT 0,
  email_subject VARCHAR(255) NOT NULL DEFAULT '',
  email_body TEXT NULL,
  sms_body TEXT NULL,
  vars TEXT NULL
);
INSERT INTO notifications_settings (name, email_on, sms_on, send_for_not_confirmed, email_subject, email_body, sms_body, vars) VALUES
('n1', 1, 1, 0, 'n1_subject', 'n1_body', 'n1_sms', '[{"type":"text","name":"name"},{"type":"text","name":"missing"}]'),
('n2', 1, 0, 1, 'n1_subject', 'n1_body', 'n1_sms', '[{"type":"text","name":"name"}]'),
('order_status_to_customer', 1, 1, 1, 'n1_subject', 'n1_body', 'n1_sms', '[]');
CREATE TABLE users (
  user_id INT NOT NULL PRIMARY KEY,
  email VARCHAR(190) NOT NULL DEFAULT '',
  email_confirmed INT NOT NULL DEFAULT 0,
  phone VARCHAR(64) NOT NULL DEFAULT '',
  phone_confirmed INT NOT NULL DEFAULT 0
);
INSERT INTO users (user_id, email, email_confirmed, phone, phone_confirmed) VALUES
(7, 'u7@x.test', 1, '+7 (912) 000-11-22', 0),
(8, '', 0, '0501', 1);
CREATE TABLE sms_api (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, handler VARCHAR(64) NOT NULL, parameters_values TEXT NULL, active INT NOT NULL DEFAULT 0);
CREATE TABLE debug_results (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, name VARCHAR(32) NOT NULL, status VARCHAR(8) NULL, debug_result TEXT NULL, time INT NULL);
INSERT INTO debug_results (name, status, debug_result, time) VALUES ('email', '0', 'old', 1);
