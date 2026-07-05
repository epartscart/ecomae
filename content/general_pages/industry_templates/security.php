<?php
/**
 * Security & Safety — Industry Template
 * Auto-generated industry frontend with sample data.
 */
defined('_ASTEXE_') or die('No access');

$industryData = array(
	'name' => 'Security & Safety',
	'tagline' => 'Guard management + monitoring + compliance',
	'description' => 'Security services, fire safety, access control, CCTV, and surveillance.',
	'icon' => 'fa-shield',
	'color_primary' => '#1e3a5f',
	'color_accent' => '#3b82f6',
	'bg_from' => '#0f172a',
	'bg_to' => '#1e3a5f',
	'hero_animation' => 'fadeInUp',
	'demo_key' => 'security_safety',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'Guard Service (24hr)', 'price' => 'AED 150/day', 'icon' => 'fa-user-secret', 'category' => 'Guarding'),
		array('name' => 'CCTV Installation', 'price' => 'AED 5,000', 'icon' => 'fa-video-camera', 'category' => 'Systems'),
		array('name' => 'Access Control', 'price' => 'AED 8,000', 'icon' => 'fa-lock', 'category' => 'Access'),
		array('name' => 'Fire Alarm System', 'price' => 'AED 12,000', 'icon' => 'fa-fire-extinguisher', 'category' => 'Fire'),
		array('name' => 'Security Audit', 'price' => 'AED 3,500', 'icon' => 'fa-search', 'category' => 'Consulting'),
		array('name' => 'Event Security', 'price' => 'AED 5,000/event', 'icon' => 'fa-shield', 'category' => 'Events'),
	),
	'features' => array(
		array('title' => 'Guard Patrol', 'icon' => 'fa-map-marker', 'desc' => 'NFC/QR checkpoint scanning with live GPS'),
		array('title' => 'Incident Reporting', 'icon' => 'fa-exclamation-triangle', 'desc' => 'Real-time incident reports with photo evidence'),
		array('title' => 'CCTV Monitoring', 'icon' => 'fa-video-camera', 'desc' => 'Multi-site camera feeds with AI alerting'),
		array('title' => 'Visitor Management', 'icon' => 'fa-id-card', 'desc' => 'Pre-registration, ID scan and badge printing'),
		array('title' => 'Guard Scheduling', 'icon' => 'fa-calendar', 'desc' => 'Shift planning with overtime and leave tracking'),
		array('title' => 'Compliance', 'icon' => 'fa-shield', 'desc' => 'SIRA licensing and civil defense compliance'),
	),
	'stats' => array(
		array('value' => '1,500+', 'label' => 'Sites Protected'),
		array('value' => '25K+', 'label' => 'Guards Managed'),
		array('value' => '99.9%', 'label' => 'Response Rate'),
		array('value' => 'SIRA', 'label' => 'Licensed'),
	),
	'sub_industries' => array(
		'Security guarding',
		'Security systems & CCTV',
		'Remote monitoring',
		'Fire safety & prevention',
		'Access control',
		'Cybersecurity services',
		'Private investigation',
		'Alarm systems',
		'Safes & vaults',
		'Security training',
	),
	'testimonial' => array('quote' => 'NFC patrol verification eliminated ghost guarding completely.', 'author' => 'Security Company Director, Downtown Dubai'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
