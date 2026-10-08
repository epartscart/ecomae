CREATE TABLE lang_text_strings (str_key VARCHAR(64) NOT NULL, is_error INT NOT NULL DEFAULT 0, same VARCHAR(8) NULL);
CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NULL);
INSERT INTO lang_text_strings (str_key) VALUES ('2387'), ('2388');
INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
('2387', 'en', 'No pages to check'), ('2388', 'en', 'Access denied'), ('2388', 'ar', 'تم رفض الوصول'), ('2388', 'ru', 'Доступ запрещён');
CREATE TABLE lang_languages (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, lang_code VARCHAR(8) NOT NULL, active INT NOT NULL DEFAULT 1, is_default INT NOT NULL DEFAULT 0, restrict_edit INT NOT NULL DEFAULT 0);
INSERT INTO lang_languages (lang_code, active, is_default) VALUES ('en', 1, 1), ('ar', 1, 0), ('ru', 0, 0);
CREATE TABLE `groups` (id INT NOT NULL PRIMARY KEY, parent INT NOT NULL DEFAULT 0, `count` INT NOT NULL DEFAULT 0, for_guests INT NOT NULL DEFAULT 0, for_registrated INT NOT NULL DEFAULT 0);
INSERT INTO `groups` (id, parent, `count`, for_guests, for_registrated) VALUES
(2, 0, 1, 0, 0), (5, 2, 1, 0, 0), (13, 5, 0, 0, 0),
(6, 0, 0, 0, 0), (7, 6, 0, 0, 0),
(10, 0, 0, 1, 0), (11, 0, 0, 0, 1), (12, 0, 0, 0, 0), (14, 0, 0, 0, 1);
CREATE TABLE users_groups_bind (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, group_id INT NOT NULL);
INSERT INTO users_groups_bind (user_id, group_id) VALUES (1, 2), (3, 5), (4, 13), (5, 7), (8, 12);
CREATE TABLE sessions (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL DEFAULT 0, csrf_guard_key VARCHAR(64) NOT NULL DEFAULT '');
INSERT INTO sessions (session, user_id, type) VALUES ('sa1', 1, 1), ('sa2', 2, 1), ('sa3', 3, 1), ('sa4', 4, 1), ('sa5', 5, 1), ('su7', 7, 0), ('su8', 8, 0), ('su9', 9, 0);
CREATE TABLE users (user_id INT NOT NULL PRIMARY KEY, email VARCHAR(255) NOT NULL DEFAULT '', email_confirmed INT NOT NULL DEFAULT 0, email_code_send_lock_expired INT NOT NULL DEFAULT 0, phone VARCHAR(64) NOT NULL DEFAULT '', phone_confirmed INT NOT NULL DEFAULT 0, phone_code_send_lock_expired INT NOT NULL DEFAULT 0, reg_variant INT NOT NULL DEFAULT 0);
INSERT INTO users (user_id) VALUES (1), (2), (3), (4), (5), (7), (8), (9);
CREATE TABLE users_profiles (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, user_id INT NOT NULL, data_key VARCHAR(64) NOT NULL, data_value TEXT NULL);
INSERT INTO users_groups_bind (user_id, group_id) VALUES (7, 10);
CREATE TABLE content (id INT NOT NULL PRIMARY KEY, url VARCHAR(255) NOT NULL, is_frontend INT NOT NULL);
INSERT INTO content (id, url, is_frontend) VALUES (1, 'lang/editor', 0), (2, 'shop/prices', 0), (3, 'cp/empty', 0), (4, 'catalogue', 1), (5, 'cabinet', 1), (6, 'open', 1), (7, 'admins_only', 1);
CREATE TABLE content_access (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, content_id INT NOT NULL, group_id INT NOT NULL);
INSERT INTO content_access (content_id, group_id) VALUES (1, 2), (2, 6), (4, 10), (5, 11), (7, 2);
