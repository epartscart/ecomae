<?php
/**
 * Manufacturing & Industrial — Industry Template
 * Auto-generated industry frontend with sample data.
 */
defined('_ASTEXE_') or die('No access');

$industryData = array(
	'name' => 'Manufacturing & Industrial',
	'tagline' => 'Production planning + quality control + BOM',
	'description' => 'Factories, production, assembly, chemicals, metals, and industrial operations.',
	'icon' => 'fa-industry',
	'color_primary' => '#475569',
	'color_accent' => '#94a3b8',
	'bg_from' => '#0f172a',
	'bg_to' => '#334155',
	'hero_animation' => 'fadeInUp',
	'demo_key' => 'manufacturing_industrial',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'CNC Machined Part', 'price' => 'AED 85/unit', 'icon' => 'fa-cog', 'category' => 'Production'),
		array('name' => 'Quality Inspection', 'price' => 'AED 150/batch', 'icon' => 'fa-check-circle', 'category' => 'QC'),
		array('name' => 'Raw Aluminum Sheet', 'price' => 'AED 45/kg', 'icon' => 'fa-square', 'category' => 'Materials'),
		array('name' => 'Custom Mold Design', 'price' => 'AED 25,000', 'icon' => 'fa-pencil-square', 'category' => 'Tooling'),
		array('name' => 'Packaging Line', 'price' => 'AED 8/unit', 'icon' => 'fa-archive', 'category' => 'Packaging'),
		array('name' => 'Maintenance Service', 'price' => 'AED 2,500/mo', 'icon' => 'fa-wrench', 'category' => 'Plant'),
	),
	'features' => array(
		array('title' => 'BOM Management', 'icon' => 'fa-sitemap', 'desc' => 'Multi-level bill of materials with revision control'),
		array('title' => 'Production Planning', 'icon' => 'fa-calendar', 'desc' => 'MRP scheduling with capacity and constraint planning'),
		array('title' => 'Quality Control', 'icon' => 'fa-check-square', 'desc' => 'Inspection plans, NCR handling and CAPA tracking'),
		array('title' => 'Machine Monitoring', 'icon' => 'fa-tachometer', 'desc' => 'OEE tracking, downtime analysis and maintenance'),
		array('title' => 'Batch Traceability', 'icon' => 'fa-qrcode', 'desc' => 'Lot tracking from raw material to finished goods'),
		array('title' => 'Cost Allocation', 'icon' => 'fa-pie-chart', 'desc' => 'Direct + overhead cost allocation per production order'),
	),
	'stats' => array(
		array('value' => '1,200+', 'label' => 'Factories'),
		array('value' => '99.2%', 'label' => 'OTD Rate'),
		array('value' => 'ISO 9001', 'label' => 'Compliant'),
		array('value' => '45%', 'label' => 'Less Waste'),
	),
	'sub_industries' => array(
		'Production & assembly',
		'Quality control / QC',
		'Raw materials & WIP',
		'Chemical manufacturing',
		'Plastics & rubber',
		'Metal & steel fabrication',
		'Printing & packaging',
		'Textile manufacturing',
		'Electronics manufacturing',
		'Food processing',
		'Pharmaceutical manufacturing',
		'Woodwork & carpentry',
		'Glass manufacturing',
		'3D printing & additive',
		'Ceramics & pottery',
		'Paper & pulp',
	),
	'testimonial' => array('quote' => 'BOM accuracy went from 85% to 99.5% — game changer for our production planning.', 'author' => 'Eng. Mohammed S., Manufacturing Plant, Jebel Ali'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
