-- Docpart: customer quote requests (run once in phpMyAdmin or mysql client)
CREATE TABLE IF NOT EXISTS `shop_quote_requests` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `user_id` int(11) NOT NULL DEFAULT 0,
  `session_id` int(11) NOT NULL DEFAULT 0,
  `status` varchar(32) NOT NULL DEFAULT 'draft',
  `time_created` int(11) DEFAULT NULL,
  `time_updated` int(11) DEFAULT NULL,
  `time_submitted` int(11) DEFAULT NULL,
  `admin_note` text,
  `customer_note` text,
  `accepted_order_id` int(11) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `user_id` (`user_id`),
  KEY `status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

CREATE TABLE IF NOT EXISTS `shop_quote_items` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `quote_id` int(11) NOT NULL,
  `product_type` int(11) NOT NULL DEFAULT 2,
  `product_object_json` longtext NOT NULL,
  `count_need` int(11) NOT NULL DEFAULT 1,
  `quoted_price` decimal(12,2) DEFAULT NULL,
  `quoted_time_to_exe` int(11) DEFAULT NULL,
  `line_admin_note` varchar(512) DEFAULT NULL,
  `offer_alternative` tinyint(1) NOT NULL DEFAULT 0,
  `alt_manufacturer` varchar(128) DEFAULT NULL,
  `alt_article` varchar(128) DEFAULT NULL,
  `alt_article_show` varchar(128) DEFAULT NULL,
  `alt_name` varchar(512) DEFAULT NULL,
  `alt_count_need` int(11) DEFAULT NULL,
  `alt_quoted_price` decimal(12,2) DEFAULT NULL,
  `alt_storage_id` int(11) DEFAULT NULL COMMENT 'Supplier warehouse for order process',
  PRIMARY KEY (`id`),
  KEY `quote_id` (`quote_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
