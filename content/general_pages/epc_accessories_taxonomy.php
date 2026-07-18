<?php
/**
 * ePartsCart Accessories & Spare Parts taxonomy.
 * Structure inspired by marketplace accessories hubs (category → subcategory),
 * mapped onto UAE warehouse stock via keyword rules — not a copy of third-party listings.
 */

if (!function_exists('epc_acc_taxonomy')) {
	function epc_acc_taxonomy(): array
	{
		return array(
			'car-care' => array(
				'label' => 'Car Care',
				'icon' => 'fa-shower',
				'subs' => array(
					'car-covers' => array('label' => 'Car Covers', 'keywords' => array('cover', 'car cover', 'body cover')),
					'air-fresheners' => array('label' => 'Air Fresheners', 'keywords' => array('freshener', 'perfume', 'aroma')),
					'microfiber' => array('label' => 'Microfiber Cloth', 'keywords' => array('microfiber', 'cloth')),
					'wax-polish' => array('label' => 'Wax & Polish', 'keywords' => array('wax', 'polish', 'compound')),
					'shampoo' => array('label' => 'Car Shampoo', 'keywords' => array('shampoo', 'wash', 'soap')),
					'coolant' => array('label' => 'Coolants', 'keywords' => array('coolant', 'antifreeze')),
					'cleaners' => array('label' => 'Cleaners', 'keywords' => array('cleaner', 'cleaning', 'detail')),
				),
			),
			'interior' => array(
				'label' => 'Interior',
				'icon' => 'fa-car',
				'subs' => array(
					'floor-mats' => array('label' => 'Floor Mats', 'keywords' => array('floor mat', 'mat', 'carpet')),
					'sun-shades' => array('label' => 'Sun Shades', 'keywords' => array('sun shade', 'shade', 'visor')),
					'dash-covers' => array('label' => 'Dash Covers', 'keywords' => array('dash', 'dashboard')),
					'steering' => array('label' => 'Steering Covers', 'keywords' => array('steering')),
					'seat' => array('label' => 'Seat Accessories', 'keywords' => array('seat', 'cushion', 'pillow')),
					'organizers' => array('label' => 'Organizers', 'keywords' => array('organizer', 'holder', 'console')),
				),
			),
			'exterior' => array(
				'label' => 'Exterior',
				'icon' => 'fa-road',
				'subs' => array(
					'wipers' => array('label' => 'Wipers', 'keywords' => array('wiper', 'blade')),
					'mirrors' => array('label' => 'Side Mirrors', 'keywords' => array('mirror')),
					'mud-flaps' => array('label' => 'Mud Flaps', 'keywords' => array('mud', 'flap', 'guard')),
					'stickers' => array('label' => 'Stickers & Logos', 'keywords' => array('sticker', 'emblem', 'logo')),
					'bumpers' => array('label' => 'Bumpers', 'keywords' => array('bumper')),
					'grills' => array('label' => 'Grills', 'keywords' => array('grill', 'grille')),
				),
			),
			'lights-electrical' => array(
				'label' => 'Lights & Electrical',
				'icon' => 'fa-lightbulb-o',
				'subs' => array(
					'headlights' => array('label' => 'Headlights', 'keywords' => array('headlight', 'head lamp')),
					'bulbs' => array('label' => 'Bulbs & LED', 'keywords' => array('bulb', 'led', 'lamp')),
					'fog' => array('label' => 'Fog Lights', 'keywords' => array('fog')),
					'spark-glow' => array('label' => 'Spark & Glow Plugs', 'keywords' => array('spark plug', 'glow plug', 'ignition')),
					'sensors' => array('label' => 'Sensors', 'keywords' => array('sensor', 'switch')),
					'batteries' => array('label' => 'Batteries', 'keywords' => array('battery', 'accumulator')),
				),
			),
			'brakes' => array(
				'label' => 'Brakes',
				'icon' => 'fa-stop-circle',
				'subs' => array(
					'pads' => array('label' => 'Brake Pads', 'keywords' => array('brake pad', 'pad')),
					'discs' => array('label' => 'Brake Discs', 'keywords' => array('brake disc', 'rotor', 'disc')),
					'shoes' => array('label' => 'Brake Shoes', 'keywords' => array('brake shoe', 'shoe')),
					'fluid' => array('label' => 'Brake Fluid', 'keywords' => array('brake fluid')),
				),
			),
			'engine-mechanical' => array(
				'label' => 'Engine & Mechanical',
				'icon' => 'fa-cogs',
				'subs' => array(
					'filters' => array('label' => 'Filters', 'keywords' => array('filter', 'oil filter', 'air filter', 'cabin', 'fuel filter')),
					'belts' => array('label' => 'Belts', 'keywords' => array('belt', 'timing')),
					'gaskets' => array('label' => 'Gaskets', 'keywords' => array('gasket', 'seal')),
					'pistons' => array('label' => 'Pistons', 'keywords' => array('piston')),
					'bearings' => array('label' => 'Bearings', 'keywords' => array('bearing')),
					'pumps' => array('label' => 'Pumps', 'keywords' => array('pump', 'fuel pump', 'water pump')),
					'joints' => array('label' => 'Joints', 'keywords' => array('joint', 'ball joint', 'propshaft')),
					'engine' => array('label' => 'Engine Parts', 'keywords' => array('engine', 'valve', 'cylinder', 'radiator', 'thermostat')),
				),
			),
			'oils-lubricants' => array(
				'label' => 'Oils & Lubricants',
				'icon' => 'fa-tint',
				'subs' => array(
					'engine-oil' => array('label' => 'Engine Oil', 'keywords' => array('engine oil', 'motor oil', '0w', '5w', '10w')),
					'gear-oil' => array('label' => 'Gear Oil', 'keywords' => array('gear oil', 'transmission oil', 'atf')),
					'grease' => array('label' => 'Grease', 'keywords' => array('grease', 'lubricant')),
				),
			),
			'tools-gadgets' => array(
				'label' => 'Tools & Gadgets',
				'icon' => 'fa-wrench',
				'subs' => array(
					'chargers' => array('label' => 'Chargers', 'keywords' => array('charger', 'jump')),
					'scanners' => array('label' => 'Scanners', 'keywords' => array('scanner', 'diagnostic', 'obd')),
					'tools' => array('label' => 'Hand Tools', 'keywords' => array('tool', 'socket', 'wrench', 'jack')),
				),
			),
			'tyres-wheels' => array(
				'label' => 'Tyres & Wheels',
				'icon' => 'fa-circle-o',
				'subs' => array(
					'tyres' => array('label' => 'Tyres', 'keywords' => array('tyre', 'tire')),
					'rims' => array('label' => 'Rims & Wheels', 'keywords' => array('rim', 'wheel', 'alloy')),
				),
			),
			'ev-hybrid' => array(
				'label' => 'EV & Hybrid',
				'icon' => 'fa-bolt',
				'subs' => array(
					'ev-chargers' => array('label' => 'EV Chargers', 'keywords' => array('ev charger', 'wallbox', 'type 2')),
					'hybrid' => array('label' => 'Hybrid Parts', 'keywords' => array('hybrid', 'inverter')),
				),
			),
			'other' => array(
				'label' => 'Other Parts',
				'icon' => 'fa-cubes',
				'subs' => array(
					'general' => array('label' => 'General', 'keywords' => array()),
				),
			),
		);
	}
}

if (!function_exists('epc_acc_classify')) {
	/**
	 * @return array{category:string, subcategory:string, category_label:string, subcategory_label:string}
	 */
	function epc_acc_classify(string $name, string $brand = ''): array
	{
		$hay = mb_strtolower(trim($name . ' ' . $brand), 'UTF-8');
		$tax = epc_acc_taxonomy();
		$best = null;
		$bestScore = 0;
		foreach ($tax as $catSlug => $cat) {
			if ($catSlug === 'other') {
				continue;
			}
			foreach ($cat['subs'] as $subSlug => $sub) {
				$keywords = isset($sub['keywords']) && is_array($sub['keywords']) ? $sub['keywords'] : array();
				foreach ($keywords as $kw) {
					$kw = trim((string) $kw);
					if ($kw === '' || mb_strpos($hay, $kw) === false) {
						continue;
					}
					$score = mb_strlen($kw, 'UTF-8');
					if ($score > $bestScore) {
						$bestScore = $score;
						$best = array(
							'category' => $catSlug,
							'subcategory' => $subSlug,
							'category_label' => (string) $cat['label'],
							'subcategory_label' => (string) $sub['label'],
						);
					}
				}
			}
		}
		if ($best !== null) {
			return $best;
		}
		return array(
			'category' => 'other',
			'subcategory' => 'general',
			'category_label' => 'Other Parts',
			'subcategory_label' => 'General',
		);
	}
}

if (!function_exists('epc_acc_warehouse_regions')) {
	/**
	 * Map warehouse codes to browseable "region" facets (UAE-focused equivalent of city filters).
	 *
	 * @return array<string, string>
	 */
	function epc_acc_warehouse_regions(): array
	{
		return array(
			'S-UAE' => 'Dubai / Sharjah stock',
			'R-UAE' => 'Ras Al Khaimah stock',
			'RK-UAE' => 'Ras Al Khaimah stock',
			'FJ-UAE' => 'Fujairah stock',
			'L-UAE' => 'UAE local stock',
			'APAI' => 'Catalogue stock',
		);
	}
}
