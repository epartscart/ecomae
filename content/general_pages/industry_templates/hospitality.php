<?php
/**
 * Hospitality & Travel — Industry Template
 * Auto-generated industry frontend with sample data.
 */
defined('_ASTEXE_') or die('No access');

$industryData = array(
	'name' => 'Hospitality & Travel',
	'tagline' => 'Reservations + guest experience + operations',
	'description' => 'Hotels, resorts, travel agencies, tourism, and accommodation.',
	'icon' => 'fa-hotel',
	'color_primary' => '#0d9488',
	'color_accent' => '#14b8a6',
	'bg_from' => '#042f2e',
	'bg_to' => '#115e59',
	'hero_animation' => 'fadeInUp',
	'demo_key' => 'hospitality_travel',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'Deluxe Suite (Night)', 'price' => 'AED 1,200', 'icon' => 'fa-bed', 'category' => 'Rooms'),
		array('name' => 'Desert Safari Package', 'price' => 'AED 350', 'icon' => 'fa-sun-o', 'category' => 'Tours'),
		array('name' => 'Airport Transfer', 'price' => 'AED 180', 'icon' => 'fa-plane', 'category' => 'Transport'),
		array('name' => 'Spa Treatment', 'price' => 'AED 450', 'icon' => 'fa-leaf', 'category' => 'Wellness'),
		array('name' => 'Conference Room', 'price' => 'AED 2,500/day', 'icon' => 'fa-microphone', 'category' => 'Events'),
		array('name' => 'Yacht Charter', 'price' => 'AED 3,500/hr', 'icon' => 'fa-ship', 'category' => 'Luxury'),
	),
	'features' => array(
		array('title' => 'Channel Manager', 'icon' => 'fa-globe', 'desc' => 'Sync rates across Booking.com, Expedia, Airbnb'),
		array('title' => 'Revenue Management', 'icon' => 'fa-line-chart', 'desc' => 'Dynamic pricing based on occupancy and demand'),
		array('title' => 'Guest CRM', 'icon' => 'fa-heart', 'desc' => 'Preference tracking, loyalty and personalization'),
		array('title' => 'Housekeeping', 'icon' => 'fa-check-square', 'desc' => 'Room status, inspection checklists and mini-bar'),
		array('title' => 'F&B Integration', 'icon' => 'fa-cutlery', 'desc' => 'Restaurant POS linked to room charges'),
		array('title' => 'Tour Booking', 'icon' => 'fa-map-marker', 'desc' => 'Activity booking with commission tracking'),
	),
	'stats' => array(
		array('value' => '4,500+', 'label' => 'Properties'),
		array('value' => '85%', 'label' => 'Avg Occupancy'),
		array('value' => '4.7★', 'label' => 'Guest Rating'),
		array('value' => '120+', 'label' => 'OTA Channels'),
	),
	'sub_industries' => array(
		'Reservation & booking',
		'Room management',
		'Guest services & concierge',
		'Resort operations',
		'Boutique hotel',
		'Hostel & backpacker',
		'Vacation rental / Airbnb',
		'Travel agency',
		'Tour operator',
		'Airline & aviation',
		'Cruise line',
		'Adventure tourism',
		'Spa & wellness resort',
		'Event venue',
		'Camping & glamping',
	),
	'testimonial' => array('quote' => 'Dynamic pricing increased our RevPAR by 22% in the first quarter.', 'author' => 'Maria C., Hotel GM, JBR Dubai'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
