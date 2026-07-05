<?php
/**
 * Agriculture & Farming — Industry Template
 * Auto-generated industry frontend with sample data.
 */
defined('_ASTEXE_') or die('No access');

$industryData = array(
	'name' => 'Agriculture & Farming',
	'tagline' => 'Crop management + livestock + trading',
	'description' => 'Crop farming, livestock, aquaculture, agritech, and agricultural trading.',
	'icon' => 'fa-leaf',
	'color_primary' => '#16a34a',
	'color_accent' => '#4ade80',
	'bg_from' => '#14532d',
	'bg_to' => '#166534',
	'hero_animation' => 'fadeInUp',
	'demo_key' => 'agriculture_farming',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'Organic Tomatoes (Ton)', 'price' => 'AED 4,500', 'icon' => 'fa-leaf', 'category' => 'Crops'),
		array('name' => 'Date Palm Harvest', 'price' => 'AED 8,000/season', 'icon' => 'fa-tree', 'category' => 'Fruits'),
		array('name' => 'Poultry Feed (50kg)', 'price' => 'AED 85', 'icon' => 'fa-cube', 'category' => 'Feed'),
		array('name' => 'Farm Equipment Hire', 'price' => 'AED 500/day', 'icon' => 'fa-truck', 'category' => 'Equipment'),
		array('name' => 'Seed Packet (1kg)', 'price' => 'AED 120', 'icon' => 'fa-archive', 'category' => 'Seeds'),
		array('name' => 'Irrigation System', 'price' => 'AED 15,000', 'icon' => 'fa-tint', 'category' => 'Systems'),
	),
	'features' => array(
		array('title' => 'Crop Planning', 'icon' => 'fa-calendar', 'desc' => 'Seasonal planting schedules with yield forecasting'),
		array('title' => 'Livestock Register', 'icon' => 'fa-paw', 'desc' => 'Animal tagging, health records and breeding'),
		array('title' => 'Weather Integration', 'icon' => 'fa-cloud', 'desc' => 'Local weather data for irrigation decisions'),
		array('title' => 'Harvest Tracking', 'icon' => 'fa-line-chart', 'desc' => 'Yield per hectare with quality grading'),
		array('title' => 'Cold Storage', 'icon' => 'fa-snowflake-o', 'desc' => 'Temperature monitoring and shelf life tracking'),
		array('title' => 'Market Pricing', 'icon' => 'fa-money', 'desc' => 'Live commodity prices from local mandis/auctions'),
	),
	'stats' => array(
		array('value' => '5,000+', 'label' => 'Farms'),
		array('value' => '2M+', 'label' => 'Hectares Managed'),
		array('value' => '35%', 'label' => 'Yield Increase'),
		array('value' => '22', 'label' => 'Crop Types'),
	),
	'sub_industries' => array(
		'Crop farming & cultivation',
		'Livestock & poultry',
		'Aquaculture & fisheries',
		'Dairy farming',
		'Organic farming',
		'Vertical / indoor farming',
		'Agricultural trading',
		'Cold storage & warehousing',
		'Farm equipment',
		'Fertilizer & seeds',
		'Agricultural pest control',
		'Agricultural consulting',
		'Farming cooperative',
		'Forestry & timber',
	),
	'testimonial' => array('quote' => 'Weather-based irrigation scheduling saved 30% water and improved crop quality.', 'author' => 'Farhan A., Date Farm Owner, Al Ain'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
