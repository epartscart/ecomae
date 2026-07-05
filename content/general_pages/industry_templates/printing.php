<?php
/**
 * Printing & Signage — Industry Template
 * Auto-generated industry frontend with sample data.
 */
defined('_ASTEXE_') or die('No access');

$industryData = array(
	'name' => 'Printing & Signage',
	'tagline' => 'Job estimation + production tracking + delivery',
	'description' => 'Commercial printing, signage, packaging design, and promotional materials.',
	'icon' => 'fa-print',
	'color_primary' => '#7c3aed',
	'color_accent' => '#a78bfa',
	'bg_from' => '#2e1065',
	'bg_to' => '#4c1d95',
	'hero_animation' => 'fadeInUp',
	'demo_key' => 'printing_signage',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'Business Cards (500)', 'price' => 'AED 120', 'icon' => 'fa-id-card', 'category' => 'Print'),
		array('name' => 'Banner (3x6m)', 'price' => 'AED 350', 'icon' => 'fa-flag', 'category' => 'Large Format'),
		array('name' => 'Brochure (1000)', 'price' => 'AED 800', 'icon' => 'fa-book', 'category' => 'Marketing'),
		array('name' => 'Vehicle Wrap', 'price' => 'AED 3,500', 'icon' => 'fa-car', 'category' => 'Signage'),
		array('name' => 'T-Shirt Print (50)', 'price' => 'AED 750', 'icon' => 'fa-male', 'category' => 'Apparel'),
		array('name' => 'Packaging Design', 'price' => 'AED 2,000', 'icon' => 'fa-archive', 'category' => 'Design'),
	),
	'features' => array(
		array('title' => 'Job Estimation', 'icon' => 'fa-calculator', 'desc' => 'Auto-calculate based on material, size and quantity'),
		array('title' => 'Production Queue', 'icon' => 'fa-tasks', 'desc' => 'Job tracking through design → print → finishing'),
		array('title' => 'Proof Approval', 'icon' => 'fa-check-circle', 'desc' => 'Online proofing with client markup and approval'),
		array('title' => 'Material Planning', 'icon' => 'fa-archive', 'desc' => 'Paper/ink/media consumption tracking'),
		array('title' => 'Machine Scheduling', 'icon' => 'fa-cog', 'desc' => 'Equipment allocation with maintenance windows'),
		array('title' => 'Delivery Management', 'icon' => 'fa-truck', 'desc' => 'Cut-off times, dispatch and delivery confirmation'),
	),
	'stats' => array(
		array('value' => '800+', 'label' => 'Print Shops'),
		array('value' => '50K+', 'label' => 'Jobs/Month'),
		array('value' => '24hr', 'label' => 'Express Turnaround'),
		array('value' => '99%', 'label' => 'Accuracy'),
	),
	'sub_industries' => array(
		'Commercial printing',
		'Large format & signage',
		'Packaging & labels',
		'Digital printing',
		'Screen printing & embroidery',
		'Banner & display',
		'Promotional products',
		'Bookbinding & finishing',
		'Offset printing',
		'Gift & personalized items',
	),
	'testimonial' => array('quote' => 'Online proofing eliminated 90% of reprints due to client miscommunication.', 'author' => 'Print Shop Owner, Al Quoz Industrial'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
