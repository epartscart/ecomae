<?php
/**
 * Logistics & Transport — Industry Template
 * Auto-generated industry frontend with sample data.
 */
defined('_ASTEXE_') or die('No access');

$industryData = array(
	'name' => 'Logistics & Transport',
	'tagline' => 'Fleet management + shipment tracking + warehousing',
	'description' => 'Freight, shipping, courier, warehousing, and supply chain.',
	'icon' => 'fa-truck',
	'color_primary' => '#0369a1',
	'color_accent' => '#0ea5e9',
	'bg_from' => '#0c4a6e',
	'bg_to' => '#075985',
	'hero_animation' => 'fadeInUp',
	'demo_key' => 'logistics_transport',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'Full Container (20ft)', 'price' => 'AED 8,500', 'icon' => 'fa-ship', 'category' => 'Sea Freight'),
		array('name' => 'Air Cargo (per kg)', 'price' => 'AED 12', 'icon' => 'fa-plane', 'category' => 'Air'),
		array('name' => 'Local Delivery', 'price' => 'AED 25', 'icon' => 'fa-motorcycle', 'category' => 'Last Mile'),
		array('name' => 'Warehouse Storage', 'price' => 'AED 15/pallet', 'icon' => 'fa-archive', 'category' => 'Storage'),
		array('name' => 'Customs Clearance', 'price' => 'AED 500', 'icon' => 'fa-file-text', 'category' => 'Customs'),
		array('name' => 'Fleet GPS Tracking', 'price' => 'AED 150/mo', 'icon' => 'fa-map-marker', 'category' => 'Tracking'),
	),
	'features' => array(
		array('title' => 'Route Optimization', 'icon' => 'fa-road', 'desc' => 'AI-powered routing for fuel and time savings'),
		array('title' => 'Live Tracking', 'icon' => 'fa-map-marker', 'desc' => 'Real-time GPS with ETA and proof of delivery'),
		array('title' => 'Warehouse Management', 'icon' => 'fa-archive', 'desc' => 'Pick/pack/ship with barcode scanning'),
		array('title' => 'Customs Documentation', 'icon' => 'fa-file-text', 'desc' => 'Auto-generate BoL, customs forms and certificates'),
		array('title' => 'Driver Management', 'icon' => 'fa-id-card', 'desc' => 'License tracking, hours and performance scoring'),
		array('title' => 'Client Portal', 'icon' => 'fa-globe', 'desc' => 'Self-service tracking and document access'),
	),
	'stats' => array(
		array('value' => '18K+', 'label' => 'Shipments/Day'),
		array('value' => '99.4%', 'label' => 'On-Time Delivery'),
		array('value' => '28%', 'label' => 'Fuel Savings'),
		array('value' => 'GCC+', 'label' => 'Coverage'),
	),
	'sub_industries' => array(
		'Freight forwarding',
		'Warehousing & 3PL',
		'Shipment tracking',
		'Courier & parcel',
		'Trucking & road freight',
		'Sea freight & container',
		'Air cargo & express',
		'Customs brokerage',
		'Cold chain logistics',
		'Last-mile delivery',
		'Moving & relocation',
		'Fleet management',
		'Rail freight',
		'Pipeline transport',
	),
	'testimonial' => array('quote' => 'Route optimization cut our fuel costs by 25% across 120 vehicles.', 'author' => 'Abdullah M., Logistics Manager, Jebel Ali'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
