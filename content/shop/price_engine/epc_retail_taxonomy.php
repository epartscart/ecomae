<?php
/**
 * EPC General Retail Taxonomy — home & garden, industrial, office, health (3 levels).
 */
defined('_ASTEXE_') or die('No access');

/**
 * @return array<int,array{slug:string,name:string,sort?:int,children?:array}>
 */
function epc_retail_tax_seed_tree(): array
{
	return array(
		array('slug' => 'retail-home', 'name' => 'Home & kitchen', 'sort' => 10, 'children' => array(
			array('slug' => 'retail-home-appliances', 'name' => 'Small appliances', 'children' => array(
				array('slug' => 'retail-home-appliances-coffee', 'name' => 'Coffee makers'),
				array('slug' => 'retail-home-appliances-blenders', 'name' => 'Blenders & mixers'),
				array('slug' => 'retail-home-appliances-airfryer', 'name' => 'Air fryers & ovens'),
			)),
			array('slug' => 'retail-home-decor', 'name' => 'Home décor', 'children' => array(
				array('slug' => 'retail-home-decor-lighting', 'name' => 'Lamps & lighting'),
				array('slug' => 'retail-home-decor-rugs', 'name' => 'Rugs & carpets'),
				array('slug' => 'retail-home-decor-wall', 'name' => 'Wall art & mirrors'),
			)),
			array('slug' => 'retail-home-storage', 'name' => 'Storage & organisation'),
			array('slug' => 'retail-home-bedding', 'name' => 'Bedding & bath', 'children' => array(
				array('slug' => 'retail-home-bedding-sheets', 'name' => 'Sheets & duvet covers'),
				array('slug' => 'retail-home-bedding-towels', 'name' => 'Towels & bath mats'),
			)),
		)),
		array('slug' => 'retail-home-garden', 'name' => 'Home & garden', 'sort' => 15, 'children' => array(
			array('slug' => 'retail-garden-tools', 'name' => 'Garden tools', 'children' => array(
				array('slug' => 'retail-garden-tools-hand', 'name' => 'Hand tools & trowels'),
				array('slug' => 'retail-garden-tools-power', 'name' => 'Lawn mowers & trimmers'),
			)),
			array('slug' => 'retail-garden-plants', 'name' => 'Plants & seeds'),
			array('slug' => 'retail-garden-outdoor', 'name' => 'Outdoor & patio', 'children' => array(
				array('slug' => 'retail-garden-outdoor-furniture', 'name' => 'Outdoor furniture'),
				array('slug' => 'retail-garden-outdoor-bbq', 'name' => 'BBQ & grills'),
				array('slug' => 'retail-garden-outdoor-umbrella', 'name' => 'Umbrellas & shade'),
			)),
			array('slug' => 'retail-garden-watering', 'name' => 'Watering & irrigation'),
			array('slug' => 'retail-garden-pots', 'name' => 'Pots, planters & soil'),
		)),
		array('slug' => 'retail-industrial', 'name' => 'Industrial & trade', 'sort' => 18, 'children' => array(
			array('slug' => 'retail-industrial-power-tools', 'name' => 'Power tools', 'children' => array(
				array('slug' => 'retail-industrial-drills', 'name' => 'Drills & drivers'),
				array('slug' => 'retail-industrial-saws', 'name' => 'Saws & grinders'),
			)),
			array('slug' => 'retail-industrial-hand-tools', 'name' => 'Hand tools & toolkits'),
			array('slug' => 'retail-industrial-safety', 'name' => 'Safety & PPE', 'children' => array(
				array('slug' => 'retail-industrial-safety-gloves', 'name' => 'Gloves & workwear'),
				array('slug' => 'retail-industrial-safety-helmets', 'name' => 'Helmets & goggles'),
			)),
			array('slug' => 'retail-industrial-plumbing', 'name' => 'Plumbing supplies'),
			array('slug' => 'retail-industrial-electrical', 'name' => 'Electrical supplies', 'children' => array(
				array('slug' => 'retail-industrial-cables', 'name' => 'Cables & wiring'),
				array('slug' => 'retail-industrial-switches', 'name' => 'Switches & sockets'),
			)),
			array('slug' => 'retail-industrial-fasteners', 'name' => 'Fasteners & hardware'),
			array('slug' => 'retail-industrial-paint', 'name' => 'Paint & coatings'),
		)),
		array('slug' => 'retail-office', 'name' => 'Office & stationery', 'sort' => 20, 'children' => array(
			array('slug' => 'retail-office-supplies', 'name' => 'Office supplies', 'children' => array(
				array('slug' => 'retail-office-supplies-paper', 'name' => 'Paper & notebooks'),
				array('slug' => 'retail-office-supplies-pens', 'name' => 'Pens & markers'),
			)),
			array('slug' => 'retail-office-furniture', 'name' => 'Office furniture', 'children' => array(
				array('slug' => 'retail-office-furniture-desks', 'name' => 'Desks & chairs'),
				array('slug' => 'retail-office-furniture-storage', 'name' => 'Filing & storage'),
			)),
			array('slug' => 'retail-office-tech', 'name' => 'Office tech accessories'),
		)),
		array('slug' => 'retail-health', 'name' => 'Health & wellness', 'sort' => 30, 'children' => array(
			array('slug' => 'retail-health-vitamins', 'name' => 'Vitamins & supplements'),
			array('slug' => 'retail-health-personal', 'name' => 'Personal care', 'children' => array(
				array('slug' => 'retail-health-skincare', 'name' => 'Skincare'),
				array('slug' => 'retail-health-haircare', 'name' => 'Hair care'),
			)),
			array('slug' => 'retail-health-fitness', 'name' => 'Fitness & sports nutrition'),
		)),
		array('slug' => 'retail-gifts', 'name' => 'Gifts & lifestyle', 'sort' => 40, 'children' => array(
			array('slug' => 'retail-gifts-hampers', 'name' => 'Gift hampers'),
			array('slug' => 'retail-gifts-seasonal', 'name' => 'Seasonal gifts'),
			array('slug' => 'retail-gifts-toys', 'name' => 'Toys & games'),
		)),
	);
}
