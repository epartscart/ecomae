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
	'hero_photo' => 'https://images.unsplash.com/photo-1565043666747-69f6646db940?w=1600&q=80',
	'gallery_photos' => array(
		'https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?w=600&q=75',
		'https://images.unsplash.com/photo-1567789884554-0b308d79bc56?w=600&q=75',
		'https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?w=600&q=75',
		'https://images.unsplash.com/photo-1561625247-85c26e6a1e82?w=600&q=75',
		'https://images.unsplash.com/photo-1513828583688-c52646db42da?w=600&q=75',
	),
	'demo_key' => 'manufacturing_industrial',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'CNC Machined Part', 'price' => 'AED 85/unit', 'icon' => 'fa-cog', 'category' => 'Production', 'image' => 'https://images.unsplash.com/photo-1565043666747-69f6646db940?w=400&q=75'),
		array('name' => 'Quality Inspection', 'price' => 'AED 150/batch', 'icon' => 'fa-check-circle', 'category' => 'QC', 'image' => 'https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?w=400&q=75'),
		array('name' => 'Raw Aluminum Sheet', 'price' => 'AED 45/kg', 'icon' => 'fa-square', 'category' => 'Materials', 'image' => 'https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?w=400&q=75'),
		array('name' => 'Custom Mold Design', 'price' => 'AED 25,000', 'icon' => 'fa-pencil-square', 'category' => 'Tooling', 'image' => 'https://images.unsplash.com/photo-1581091007718-0c50d599bfd0?w=400&q=75'),
		array('name' => 'Packaging Line', 'price' => 'AED 8/unit', 'icon' => 'fa-archive', 'category' => 'Packaging', 'image' => 'https://images.unsplash.com/photo-1558346490-a72e53ae2d4f?w=400&q=75'),
		array('name' => 'Maintenance Service', 'price' => 'AED 2,500/mo', 'icon' => 'fa-wrench', 'category' => 'Plant', 'image' => 'https://images.unsplash.com/photo-1611117775350-ac3950990985?w=400&q=75'),
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
