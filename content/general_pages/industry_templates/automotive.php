<?php
/**
 * Automotive & Vehicles — Industry Template
 * Auto-generated industry frontend with sample data.
 */
defined('_ASTEXE_') or die('No access');

$industryData = array(
	'name' => 'Automotive & Vehicles',
	'tagline' => 'Complete workshop + dealership management',
	'description' => 'Auto parts, repair, dealerships, detailing, and vehicle services.',
	'icon' => 'fa-car',
	'color_primary' => '#dc2626',
	'color_accent' => '#f97316',
	'bg_from' => '#0b1220',
	'bg_to' => '#1e3a5f',
	'hero_animation' => 'fadeInUp',
	'demo_key' => 'automotive',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'Brake Pad Set (OEM)', 'price' => 'AED 245', 'icon' => 'fa-wrench', 'category' => 'Parts'),
		array('name' => 'Engine Oil 5W-30', 'price' => 'AED 89', 'icon' => 'fa-tint', 'category' => 'Fluids'),
		array('name' => 'Full Service Package', 'price' => 'AED 599', 'icon' => 'fa-car', 'category' => 'Service'),
		array('name' => 'Tyre Set 225/45R17', 'price' => 'AED 1,200', 'icon' => 'fa-circle-o', 'category' => 'Tyres'),
		array('name' => 'Battery 12V 80Ah', 'price' => 'AED 350', 'icon' => 'fa-bolt', 'category' => 'Electrical'),
		array('name' => 'AC Compressor', 'price' => 'AED 780', 'icon' => 'fa-snowflake-o', 'category' => 'Climate'),
	),
	'features' => array(
		array('title' => 'Parts Cross-Reference', 'icon' => 'fa-exchange', 'desc' => 'OE, aftermarket and TecDoc cross-references with live stock'),
		array('title' => 'Workshop Calendar', 'icon' => 'fa-calendar', 'desc' => 'Bay scheduling, tech allocation and job card automation'),
		array('title' => 'Fleet Contracts', 'icon' => 'fa-truck', 'desc' => 'Corporate fleet maintenance contracts with SLA tracking'),
		array('title' => 'VIN Decoder', 'icon' => 'fa-barcode', 'desc' => 'Decode any VIN to year/make/model and parts compatibility'),
		array('title' => 'Insurance Claims', 'icon' => 'fa-shield', 'desc' => 'Direct insurer integration for claim-based repairs'),
		array('title' => 'Warranty Tracking', 'icon' => 'fa-certificate', 'desc' => 'Part-level warranty periods with auto-reminder alerts'),
	),
	'stats' => array(
		array('value' => '2.4M+', 'label' => 'Parts Listed'),
		array('value' => '350+', 'label' => 'Makes Covered'),
		array('value' => '99.5%', 'label' => 'Uptime SLA'),
		array('value' => '12', 'label' => 'Countries'),
	),
	'sub_industries' => array(
		'Parts catalog & cross-references',
		'Workshop / garage management',
		'Vehicle dealership & sales',
		'Detailing & car care',
		'Auto insurance processing',
		'Vehicle auctions',
		'Car rental & leasing',
		'Automotive logistics',
		'Vehicle/parts manufacturing',
		'Tyre shop & fitting',
		'Windshield & auto glass',
		'EV charging stations',
		'Fleet management',
	),
	'testimonial' => array('quote' => 'Reduced our parts lookup time by 80% and increased workshop efficiency dramatically.', 'author' => 'Ahmed K., Workshop Manager, Dubai'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
