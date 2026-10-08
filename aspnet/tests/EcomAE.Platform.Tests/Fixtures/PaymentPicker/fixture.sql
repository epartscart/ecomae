CREATE TABLE shop_payment_systems (
  id INT NOT NULL PRIMARY KEY,
  name VARCHAR(255) NOT NULL,
  handler VARCHAR(64) NOT NULL,
  description VARCHAR(255) NOT NULL DEFAULT '',
  active INT NOT NULL DEFAULT 0,
  parameters_values TEXT NULL,
  anable INT NOT NULL DEFAULT 0
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
INSERT INTO shop_payment_systems (id, name, handler, active, anable) VALUES
  (1, 'epc_pay_robokassa', 'robokassa', 0, 1),
  (2, 'epc_pay_stripe', 'stripe', 1, 1),
  (3, 'epc_pay_nowpayments', 'nowpayments', 0, 1),
  (4, 'epc_pay_tabby', 'tabby', 0, 0),
  (5, 'epc_pay_blank', '', 0, 1),
  (6, 'epc_pay_jazzcash', 'jazzcash', 0, 1),
  (7, 'epc_pay_paypal', 'paypal', 0, 1),
  (8, 'epc_pay_tamara', 'tamara', 0, 1);
INSERT INTO lang_text_strings (str_key, is_error, same) VALUES
  ('epc_pay_robokassa', 0, NULL),
  ('epc_pay_stripe', 0, ''),
  ('epc_pay_nowpayments', 0, NULL),
  ('epc_pay_jazzcash', 0, NULL),
  ('epc_pay_tamara', 0, 'ar');
INSERT INTO lang_text_strings_translation (str_key, lang_code, value) VALUES
  ('epc_pay_robokassa', 'en', 'Robokassa'),
  ('epc_pay_stripe', 'en', 'Stripe & "Card" <Pay> \'Now\''),
  ('epc_pay_nowpayments', 'en', 'NOWPayments/Crypto'),
  ('epc_pay_jazzcash', 'en', 'جاز كاش JazzCash'),
  ('epc_pay_tamara', 'en', 'Tamara EN'),
  ('epc_pay_tamara', 'ar', 'تمارا');
