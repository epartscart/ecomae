CREATE TABLE lang_text_strings (str_key VARCHAR(64) NOT NULL, is_error INT NOT NULL DEFAULT 0, same VARCHAR(8) NULL);
CREATE TABLE lang_text_strings_translation (str_key VARCHAR(64) NOT NULL, lang_code VARCHAR(8) NOT NULL, value TEXT NULL);
INSERT INTO lang_text_strings (str_key) VALUES ('1312'), ('4699'), ('4700'), ('4701'), ('5641');
INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
('1312', 'en', 'Phone'), ('4699', 'en', 'Incorrect'), ('4700', 'en', 'This'), ('4701', 'en', 'is already used'),
('5641', 'en', 'Адрес разработчика / "запрещён"');
CREATE TABLE reg_fields (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, name VARCHAR(64) NOT NULL, `regexp` TEXT NULL);
INSERT INTO reg_fields (name, `regexp`) VALUES ('email', '[a-zA-Z0-9._&-]+@[a-z0-9.-]+\\.[a-z]+'), ('phone', '\\+?[0-9]{7,15}');
CREATE TABLE users (user_id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, email VARCHAR(255) NOT NULL DEFAULT '', phone VARCHAR(64) NOT NULL DEFAULT '');
INSERT INTO users (user_id, email, phone) VALUES (7, 'ann@x.test', '+97150111222'), (8, 'o&amp;k@x.test', '');
CREATE TABLE sessions (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, session VARCHAR(64) NOT NULL, user_id INT NOT NULL, type INT NOT NULL DEFAULT 0, csrf_guard_key VARCHAR(64) NOT NULL DEFAULT '');
INSERT INTO sessions (session, user_id, type, csrf_guard_key) VALUES ('s_user', 7, 0, 'ku'), ('s_admin', 1, 1, 'ka');
