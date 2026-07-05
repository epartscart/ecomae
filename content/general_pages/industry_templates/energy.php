<?php
/**
 * Energy & Utilities — Industry Template
 * Auto-generated industry frontend with sample data.
 */
defined('_ASTEXE_') or die('No access');

$industryData = array(
	'name' => 'Energy & Utilities',
	'tagline' => 'Asset management + billing + field operations',
	'description' => 'Oil & gas, solar, wind, water, electricity, and utility services.',
	'icon' => 'fa-bolt',
	'color_primary' => '#ca8a04',
	'color_accent' => '#facc15',
	'bg_from' => '#422006',
	'bg_to' => '#713f12',
	'hero_animation' => 'fadeInUp',
	'demo_key' => 'energy_utilities',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'Solar Panel Install', 'price' => 'AED 25,000', 'icon' => 'fa-sun-o', 'category' => 'Solar'),
		array('name' => 'Meter Reading', 'price' => 'AED 50/unit', 'icon' => 'fa-tachometer', 'category' => 'Utility'),
		array('name' => 'Generator Service', 'price' => 'AED 800', 'icon' => 'fa-bolt', 'category' => 'Power'),
		array('name' => 'EV Charger Setup', 'price' => 'AED 5,000', 'icon' => 'fa-plug', 'category' => 'EV'),
		array('name' => 'Energy Audit', 'price' => 'AED 3,500', 'icon' => 'fa-search', 'category' => 'Consulting'),
		array('name' => 'Battery Storage', 'price' => 'AED 18,000', 'icon' => 'fa-battery-full', 'category' => 'Storage'),
	),
	'features' => array(
		array('title' => 'Asset Registry', 'icon' => 'fa-sitemap', 'desc' => 'Complete plant/equipment hierarchy with maintenance'),
		array('title' => 'Utility Billing', 'icon' => 'fa-file-text-o', 'desc' => 'Metered billing with tariff management'),
		array('title' => 'Field Ops', 'icon' => 'fa-map-marker', 'desc' => 'Mobile work orders with GPS and photo capture'),
		array('title' => 'SCADA Integration', 'icon' => 'fa-tachometer', 'desc' => 'Real-time monitoring of generation assets'),
		array('title' => 'Compliance', 'icon' => 'fa-shield', 'desc' => 'Environmental reporting and permit tracking'),
		array('title' => 'Demand Forecasting', 'icon' => 'fa-line-chart', 'desc' => 'Load prediction for capacity planning'),
	),
	'stats' => array(
		array('value' => '50GW+', 'label' => 'Capacity Managed'),
		array('value' => '99.97%', 'label' => 'Grid Uptime'),
		array('value' => '40%', 'label' => 'Carbon Reduction'),
		array('value' => '15', 'label' => 'Countries'),
	),
	'sub_industries' => array(
		'Power generation',
		'Energy distribution',
		'Utility billing',
		'Solar energy',
		'Wind energy',
		'Oil & gas operations',
		'Water treatment & utility',
		'EV charging infrastructure',
		'Battery & energy storage',
		'Nuclear energy',
		'Biomass & bioenergy',
		'Mining & extraction',
		'Waste-to-energy',
	),
	'testimonial' => array('quote' => 'Predictive maintenance reduced our unplanned downtime by 60%.', 'author' => 'Eng. Nadia K., Power Plant Director, DEWA'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
