<?php
/**
 * Food & Beverage — Industry Template
 * Auto-generated industry frontend with sample data.
 */
defined('_ASTEXE_') or die('No access');

$industryData = array(
	'name' => 'Food & Beverage',
	'tagline' => 'Restaurant POS + kitchen management + delivery',
	'description' => 'Restaurants, bakeries, cafes, catering, food manufacturing, and wholesale.',
	'icon' => 'fa-cutlery',
	'color_primary' => '#ea580c',
	'color_accent' => '#fb923c',
	'bg_from' => '#431407',
	'bg_to' => '#7c2d12',
	'hero_animation' => 'fadeInUp',
	'demo_key' => 'food_beverage',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'Wagyu Burger', 'price' => 'AED 85', 'icon' => 'fa-cutlery', 'category' => 'Main Course'),
		array('name' => 'Caesar Salad', 'price' => 'AED 42', 'icon' => 'fa-leaf', 'category' => 'Starters'),
		array('name' => 'Truffle Fries', 'price' => 'AED 38', 'icon' => 'fa-fire', 'category' => 'Sides'),
		array('name' => 'Espresso Blend 1kg', 'price' => 'AED 120', 'icon' => 'fa-coffee', 'category' => 'Beverages'),
		array('name' => 'Chocolate Cake', 'price' => 'AED 55', 'icon' => 'fa-birthday-cake', 'category' => 'Desserts'),
		array('name' => 'Catering (50 pax)', 'price' => 'AED 3,500', 'icon' => 'fa-users', 'category' => 'Events'),
	),
	'features' => array(
		array('title' => 'Table POS', 'icon' => 'fa-tablet', 'desc' => 'Touch-screen ordering with split bills and tips'),
		array('title' => 'Kitchen Display', 'icon' => 'fa-desktop', 'desc' => 'Real-time order routing to kitchen stations'),
		array('title' => 'Recipe Costing', 'icon' => 'fa-calculator', 'desc' => 'Ingredient-level cost tracking with margin targets'),
		array('title' => 'Delivery Integration', 'icon' => 'fa-motorcycle', 'desc' => 'Talabat, Deliveroo, Uber Eats order sync'),
		array('title' => 'Inventory Alerts', 'icon' => 'fa-exclamation-triangle', 'desc' => 'Low-stock alerts with auto-PO generation'),
		array('title' => 'Loyalty & CRM', 'icon' => 'fa-heart', 'desc' => 'Points, stamps, and targeted promotions'),
	),
	'stats' => array(
		array('value' => '15K+', 'label' => 'Restaurants'),
		array('value' => '2.1M', 'label' => 'Orders/Day'),
		array('value' => '32%', 'label' => 'Avg Cost Saving'),
		array('value' => '18', 'label' => 'Integrations'),
	),
	'sub_industries' => array(
		'Restaurant & dine-in',
		'Cafe & coffee shop',
		'Bakery & confectionery',
		'Catering & events',
		'Food truck & mobile',
		'Cloud kitchen / dark kitchen',
		'Bar & pub',
		'Fast food & QSR',
		'Kitchen management',
		'POS & ordering',
		'Delivery & takeaway',
		'Food manufacturing',
		'Beverage production',
		'Food wholesale & distribution',
		'Ice cream & desserts',
		'Butcher & meat shop',
		'Organic & health food',
	),
	'testimonial' => array('quote' => 'Kitchen efficiency improved 45% after implementing the display system.', 'author' => 'Chef Mario L., Restaurant Group, Dubai'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
