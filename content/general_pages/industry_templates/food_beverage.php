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
	'hero_photo' => 'https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=1600&q=80',
	'gallery_photos' => array(
		'https://images.unsplash.com/photo-1414235077428-338989a2e8c0?w=600&q=75',
		'https://images.unsplash.com/photo-1567620905732-2d1ec7ab7445?w=600&q=75',
		'https://images.unsplash.com/photo-1555939594-58d7cb561ad1?w=600&q=75',
		'https://images.unsplash.com/photo-1476224203421-9ac39bcb3327?w=600&q=75',
		'https://images.unsplash.com/photo-1565299624946-b28f40a0ae38?w=600&q=75',
	),
	'demo_key' => 'food_beverage',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'Wagyu Burger', 'price' => 'AED 85', 'icon' => 'fa-cutlery', 'category' => 'Main Course', 'image' => 'https://images.unsplash.com/photo-1565958011703-44f9829ba187?w=400&q=75'),
		array('name' => 'Caesar Salad', 'price' => 'AED 42', 'icon' => 'fa-leaf', 'category' => 'Starters', 'image' => 'https://images.unsplash.com/photo-1482049016688-2d3e1b311543?w=400&q=75'),
		array('name' => 'Truffle Fries', 'price' => 'AED 38', 'icon' => 'fa-fire', 'category' => 'Sides', 'image' => 'https://images.unsplash.com/photo-1432139509613-5c4255815697?w=400&q=75'),
		array('name' => 'Espresso Blend 1kg', 'price' => 'AED 120', 'icon' => 'fa-coffee', 'category' => 'Beverages', 'image' => 'https://images.unsplash.com/photo-1540189549336-e6e99c3679fe?w=400&q=75'),
		array('name' => 'Chocolate Cake', 'price' => 'AED 55', 'icon' => 'fa-birthday-cake', 'category' => 'Desserts', 'image' => 'https://images.unsplash.com/photo-1473093295043-cdd812d0e601?w=400&q=75'),
		array('name' => 'Catering (50 pax)', 'price' => 'AED 3,500', 'icon' => 'fa-users', 'category' => 'Events', 'image' => 'https://images.unsplash.com/photo-1498837167922-ddd27525d352?w=400&q=75'),
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
