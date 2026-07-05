<?php
/**
 * Rental & Leasing — Industry Template
 * Auto-generated industry frontend with sample data.
 */
defined('_ASTEXE_') or die('No access');

$industryData = array(
	'name' => 'Rental & Leasing',
	'tagline' => 'Asset tracking + booking + contract management',
	'description' => 'Equipment rental, car hire, property leasing, and subscription services.',
	'icon' => 'fa-key',
	'color_primary' => '#ca8a04',
	'color_accent' => '#facc15',
	'bg_from' => '#422006',
	'bg_to' => '#854d0e',
	'hero_animation' => 'fadeInUp',
	'demo_key' => 'rental_leasing',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'SUV Rental (Day)', 'price' => 'AED 200', 'icon' => 'fa-car', 'category' => 'Vehicles'),
		array('name' => 'Excavator (Week)', 'price' => 'AED 5,000', 'icon' => 'fa-truck', 'category' => 'Equipment'),
		array('name' => 'Office Space (Mo)', 'price' => 'AED 8,000', 'icon' => 'fa-building', 'category' => 'Property'),
		array('name' => 'Event Equipment', 'price' => 'AED 3,000', 'icon' => 'fa-music', 'category' => 'Events'),
		array('name' => 'Storage Unit', 'price' => 'AED 500/mo', 'icon' => 'fa-archive', 'category' => 'Storage'),
		array('name' => 'Yacht Charter (4hr)', 'price' => 'AED 2,500', 'icon' => 'fa-ship', 'category' => 'Marine'),
	),
	'features' => array(
		array('title' => 'Online Booking', 'icon' => 'fa-calendar-check-o', 'desc' => 'Real-time availability with instant confirmation'),
		array('title' => 'Fleet Tracking', 'icon' => 'fa-map-marker', 'desc' => 'GPS tracking with geofence and mileage'),
		array('title' => 'Contract Management', 'icon' => 'fa-file-text', 'desc' => 'Auto-generate lease agreements with e-sign'),
		array('title' => 'Maintenance Schedule', 'icon' => 'fa-wrench', 'desc' => 'Preventive maintenance based on usage/time'),
		array('title' => 'Damage Assessment', 'icon' => 'fa-camera', 'desc' => 'Check-in/check-out with photo documentation'),
		array('title' => 'Revenue Optimization', 'icon' => 'fa-line-chart', 'desc' => 'Dynamic pricing based on demand and season'),
	),
	'stats' => array(
		array('value' => '12K+', 'label' => 'Assets Tracked'),
		array('value' => '82%', 'label' => 'Utilization Rate'),
		array('value' => 'AED 500M', 'label' => 'Annual Revenue'),
		array('value' => 'GCC', 'label' => 'Coverage'),
	),
	'sub_industries' => array(
		'Online booking system',
		'Fleet / asset tracking',
		'Lease contract management',
		'Car & vehicle rental',
		'Equipment & tool rental',
		'Property leasing',
		'Party & event rentals',
		'Heavy machinery hire',
		'Co-working & office rental',
		'Self-storage rental',
		'Boat & yacht charter',
		'Costume & formal wear hire',
	),
	'testimonial' => array('quote' => 'Asset utilization went from 65% to 82% with dynamic pricing and online booking.', 'author' => 'Rental Company Manager, Business Bay'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
