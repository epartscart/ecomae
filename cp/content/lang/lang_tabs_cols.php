<?php
//Здесь содержится массив с описанием таблиц и колонок с мультиязычным контентом. Этот массив может использоваться для поиска использования строк.

/*
Структура.
На первом уровне ассоциативный массив. Ключ равен имени таблицы. Значение - вложенный ассоциативный массив. Ключ равен имени колонки, значение - тип полонки (text - обычная строка, т.е. содержит прямое указание не str_key; json - JSON-структура, в которой могут встречаться str_key)

'' => array(
		'' => 'text',
	),

*/


$lang_tabs_cols = array(
	
	'config_groups' => array(
		'caption' => 'text'
	),
	
	'config_items' => array(
		'caption' => 'text',
		'hint' => 'text'
	),
	
	
	'content' => array(
		'value' => 'text',
		'description' => 'text',
		'title_tag' => 'text',
		'description_tag' => 'text',
		'keywords_tag' => 'text',
		'author_tag' => 'text'
	),
	
	'control_groups' => array(
		'caption' => 'text'
	),
	
	'control_items' => array(
		'caption' => 'text'
	),
	
	'groups' => array(
		'value' => 'text',
		'description' => 'text'
	),
	
	
	'menu' => array(
		'caption' => 'text',
		'structure' => 'json'
	),
	
	'metadata_handler_rules' => array(
		'title_rule' => 'json'
	),
	
	
	'modules' => array(
		'prototype_name' => 'text',
		'caption' => 'text',
		'data' => 'json'
	),
	
	'notifications_settings' => array(
		'caption' => 'text',
		'description' => 'text',
		'event' => 'text',
		'email_subject' => 'text',
		'email_body' => 'text',
		'sms_body' => 'text',
		'default_email_subject' => 'text',
		'default_email_body' => 'text',
		'default_sms_body' => 'text',
		'vars' => 'json'
	),
	
	'plugins' => array(
		'caption' => 'text',
		'description' => 'text',
		'data_structure' => 'json',
		'data_value' => 'json'
	),
	
	'reg_fields' => array(
		'caption' => 'text'
	),
	
	'reg_variants' => array(
		'caption' => 'text'
	),
	
	'shop_accounting_codes' => array(
		'name' => 'text'
	),
	
	'shop_catalogue_categories' => array(
		'value' => 'text',
		'title_tag' => 'text',
		'description_tag' => 'text',
		'keywords_tag' => 'text'
	),
	
	'shop_catalogue_products' => array(
		'caption' => 'text',
		'title_tag' => 'text',
		'description_tag' => 'text',
		'keywords_tag' => 'text'
	),
	
	'shop_categories_properties_map' => array(
		'value' => 'text'
	),
	
	'shop_currencies' => array(
		'caption_short' => 'text'
	),
	
	'shop_docpart_cars' => array(
		'caption' => 'text'
	),
	
	'shop_docpart_cars_catalogues' => array(
		'caption' => 'text'
	),
	
	'shop_docpart_prices_cols_types' => array(
		'caption' => 'text'
	),
	
	'shop_docpart_prices_load_modes' => array(
		'name' => 'text'
	),
	
	'shop_docpart_search_tabs' => array(
		'caption' => 'text'
	),
	
	'shop_geo' => array(
		'value' => 'text'
	),
	
	'shop_kkt_devices' => array(
		'name' => 'text'
	),
	
	'shop_kkt_interfaces_types' => array(
		'description' => 'text'
	),
	
	'shop_line_lists' => array(
		'caption' => 'text'
	),
	
	'shop_line_lists_items' => array(
		'value' => 'text'
	),
	
	'shop_main_page_groups' => array(
		'caption' => 'text'
	),
	
	'shop_obtaining_modes' => array(
		'caption' => 'text'
	),
	
	'shop_offices' => array(
		'caption' => 'text',
		'country' => 'text',
		'region' => 'text',
		'city' => 'text',
		'address' => 'text',
		'description' => 'text',
		'timetable' => 'text'
	),
	
	'shop_orders_items_statuses_ref' => array(
		'name' => 'text'
	),
	
	'shop_orders_statuses_ref' => array(
		'name' => 'text'
	),
	
	'shop_payment_systems' => array(
		'name' => 'text',
		'description' => 'text'
	),
	
	'shop_print_docs' => array(
		'caption' => 'text',
		'description' => 'text'
	),
	
	'shop_products_stickers' => array(
		'value' => 'text',
		'description' => 'text'
	),
	
	'shop_products_text' => array(
		'content' => 'text'
	),
	
	'shop_properties_types' => array(
		'caption' => 'text',
		'info' => 'text'
	),
	
	'shop_properties_values_text' => array(
		'value' => 'text'
	),
	
	'shop_sao_actions' => array(
		'name' => 'text'
	),
	
	'shop_sao_states' => array(
		'name' => 'text'
	),
	
	'shop_tree_lists' => array(
		'caption' => 'text'
	),
	
	'shop_tree_lists_items' => array(
		'value' => 'text'
	)
	
);
?>