CREATE TABLE `epc_erp_inv_closing` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `period_end` date NOT NULL,
  `warehouse_id` int(11) NOT NULL DEFAULT 0,
  `item_id` int(11) NOT NULL,
  `qty_closing` decimal(14,3) NOT NULL DEFAULT 0.000,
  `avg_unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
  `value_closing` decimal(14,2) NOT NULL DEFAULT 0.00,
  `time_created` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `x_period` (`period_end`,`warehouse_id`,`item_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
CREATE TABLE `epc_erp_inv_field_defs` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `field_key` varchar(32) NOT NULL,
  `label` varchar(120) NOT NULL,
  `field_type` enum('text','number','date','select') NOT NULL DEFAULT 'text',
  `options_json` text DEFAULT NULL,
  `sort_order` int(11) NOT NULL DEFAULT 0,
  `active` tinyint(1) NOT NULL DEFAULT 1,
  `field_role` enum('inventory','non_inventory') NOT NULL DEFAULT 'inventory',
  `source_pack` varchar(64) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
  UNIQUE KEY `x_key` (`field_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
CREATE TABLE `epc_erp_inv_item_fields` (
  `item_id` int(11) NOT NULL,
  `field_key` varchar(32) NOT NULL,
  `value` varchar(512) NOT NULL DEFAULT '',
  PRIMARY KEY (`item_id`,`field_key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
CREATE TABLE `epc_erp_inv_items` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `sku` varchar(64) NOT NULL,
  `name` varchar(255) NOT NULL,
  `product_id` int(11) NOT NULL DEFAULT 0,
  `item_type` enum('standard','perishable','serialized') NOT NULL DEFAULT 'standard',
  `track_expiry` tinyint(1) NOT NULL DEFAULT 0,
  `unit` varchar(16) NOT NULL DEFAULT 'pcs',
  `active` tinyint(1) NOT NULL DEFAULT 1,
  `time_created` int(11) NOT NULL DEFAULT 0,
  `barcode` varchar(128) NOT NULL DEFAULT '',
  `search_name` varchar(255) NOT NULL DEFAULT '',
  `product_type` varchar(16) NOT NULL DEFAULT 'item',
  `item_group` varchar(64) NOT NULL DEFAULT '',
  `item_model_group` varchar(64) NOT NULL DEFAULT '',
  `costing_method` varchar(24) NOT NULL DEFAULT '',
  `storage_dim_group` varchar(64) NOT NULL DEFAULT '',
  `tracking_dim_group` varchar(64) NOT NULL DEFAULT '',
  `purchase_unit` varchar(16) NOT NULL DEFAULT '',
  `sales_unit` varchar(16) NOT NULL DEFAULT '',
  `default_warehouse_id` int(11) NOT NULL DEFAULT 0,
  `default_vendor_id` int(11) NOT NULL DEFAULT 0,
  `sales_tax_group` varchar(64) NOT NULL DEFAULT '',
  `purchase_tax_group` varchar(64) NOT NULL DEFAULT '',
  `buyer_group` varchar(64) NOT NULL DEFAULT '',
  `coverage_group` varchar(64) NOT NULL DEFAULT '',
  `abc_code` varchar(8) NOT NULL DEFAULT '',
  `net_weight` decimal(14,3) NOT NULL DEFAULT 0.000,
  `gross_weight` decimal(14,3) NOT NULL DEFAULT 0.000,
  `tare_weight` decimal(14,3) NOT NULL DEFAULT 0.000,
  `volume` decimal(14,3) NOT NULL DEFAULT 0.000,
  `gross_depth` decimal(14,3) NOT NULL DEFAULT 0.000,
  `gross_width` decimal(14,3) NOT NULL DEFAULT 0.000,
  `gross_height` decimal(14,3) NOT NULL DEFAULT 0.000,
  `standard_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
  `sales_price` decimal(14,4) NOT NULL DEFAULT 0.0000,
  `purchase_price` decimal(14,4) NOT NULL DEFAULT 0.0000,
  `notes` varchar(1000) NOT NULL DEFAULT '',
  PRIMARY KEY (`id`),
  UNIQUE KEY `x_sku` (`sku`),
  KEY `x_product` (`product_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
CREATE TABLE `epc_erp_inv_movements` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `movement_type` enum('opening','purchase_in','sale_out','transfer_in','transfer_out','adjustment','return_in','return_out') NOT NULL,
  `warehouse_id` int(11) NOT NULL,
  `item_id` int(11) NOT NULL,
  `qty` decimal(14,3) NOT NULL DEFAULT 0.000,
  `unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
  `total_cost` decimal(14,2) NOT NULL DEFAULT 0.00,
  `transfer_warehouse_id` int(11) NOT NULL DEFAULT 0,
  `purchase_id` int(11) NOT NULL DEFAULT 0,
  `order_id` int(11) NOT NULL DEFAULT 0,
  `batch_no` varchar(64) DEFAULT NULL,
  `expiry_date` date DEFAULT NULL,
  `reference` varchar(128) DEFAULT NULL,
  `note` text DEFAULT NULL,
  `movement_date` int(11) NOT NULL DEFAULT 0,
  `admin_id` int(11) NOT NULL DEFAULT 0,
  `opening_batch_id` int(11) NOT NULL DEFAULT 0,
  `active` tinyint(1) NOT NULL DEFAULT 1,
  `serial_no` varchar(128) DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `x_wh_item` (`warehouse_id`,`item_id`,`movement_date`),
  KEY `x_type` (`movement_type`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
CREATE TABLE `epc_erp_inv_serials` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `item_id` int(11) NOT NULL,
  `serial_no` varchar(128) NOT NULL,
  `warehouse_id` int(11) NOT NULL DEFAULT 0,
  `batch_no` varchar(64) DEFAULT NULL,
  `status` enum('in_stock','sold','returned','scrapped','in_transit') NOT NULL DEFAULT 'in_stock',
  `in_movement_id` int(11) NOT NULL DEFAULT 0,
  `out_movement_id` int(11) NOT NULL DEFAULT 0,
  `unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
  `note` varchar(255) DEFAULT NULL,
  `time_created` int(11) NOT NULL DEFAULT 0,
  `time_updated` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `x_item_serial` (`item_id`,`serial_no`),
  KEY `x_status` (`status`),
  KEY `x_wh` (`warehouse_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci COMMENT='Serial-number register';
CREATE TABLE `epc_erp_inv_stock` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `warehouse_id` int(11) NOT NULL,
  `item_id` int(11) NOT NULL,
  `qty_on_hand` decimal(14,3) NOT NULL DEFAULT 0.000,
  `avg_unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
  `expiry_date` date DEFAULT NULL,
  `batch_no` varchar(64) DEFAULT NULL,
  `variant_label` varchar(120) DEFAULT NULL,
  `time_updated` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `x_wh_item_batch` (`warehouse_id`,`item_id`,`batch_no`,`variant_label`),
  KEY `x_item` (`item_id`),
  KEY `x_expiry` (`expiry_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
CREATE TABLE `epc_erp_inv_warehouses` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `storage_id` int(11) NOT NULL DEFAULT 0,
  `code` varchar(32) NOT NULL DEFAULT '',
  `name` varchar(255) NOT NULL,
  `active` tinyint(1) NOT NULL DEFAULT 1,
  `time_created` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `x_code` (`code`),
  KEY `x_storage` (`storage_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
CREATE TABLE `epc_erp_purchase_inv_lines` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `purchase_id` int(11) NOT NULL,
  `warehouse_id` int(11) NOT NULL,
  `item_id` int(11) NOT NULL,
  `qty` decimal(14,3) NOT NULL DEFAULT 0.000,
  `unit_cost` decimal(14,4) NOT NULL DEFAULT 0.0000,
  `batch_no` varchar(64) DEFAULT NULL,
  `expiry_date` date DEFAULT NULL,
  `movement_id` int(11) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  KEY `x_purchase` (`purchase_id`),
  KEY `x_item` (`item_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb3 COLLATE=utf8mb3_general_ci;
CREATE TABLE shop_orders_items (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, order_id INT NOT NULL, product_id INT NOT NULL DEFAULT 0, count_need DECIMAL(10,2) NOT NULL DEFAULT 0, status INT NOT NULL DEFAULT 1);
INSERT INTO shop_orders_items (order_id, product_id, count_need) VALUES (40, 101, 2), (40, 101, 1), (40, 102, 5), (40, 0, 4), (40, 103, 1), (40, 104, 0), (41, 999, 2);
INSERT INTO epc_erp_inv_warehouses (id, code, name, active) VALUES (1, 'MAIN', 'Main', 1), (2, 'DXB', 'Dubai', 1), (3, 'OLD', 'Old', 0);
INSERT INTO epc_erp_inv_items (id, sku, name, product_id, active) VALUES (1, 'OF-1', 'Oil filter', 101, 1), (2, 'BP-1', 'Brake pad', 102, 1), (3, 'SP-1', 'Spark plug', 103, 0), (4, 'WP-1', 'Wiper', 0, 1), (5, 'BP-OLD', 'Brake pad', 0, 0), (6, 'BU-1', 'Bulb', 104, 1);
INSERT INTO epc_erp_inv_stock (warehouse_id, item_id, qty_on_hand, avg_unit_cost, batch_no, time_updated) VALUES (1, 1, 1.000, 4.0000, NULL, 1), (2, 1, 10.000, 4.5000, NULL, 1), (2, 2, 3.000, 12.3456, NULL, 1), (1, 2, 50.000, 7.0000, 'B1', 1);
