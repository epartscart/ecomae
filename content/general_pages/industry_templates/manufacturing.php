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
	'sub_industry_products' => array(
		'Production & assembly' => array('photo'=>'https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?w=400&q=75','desc'=>'Specialized production & assembly solutions','products'=>array(
			array('name'=>'Production & assembly Basic','price'=>'AED 150','image'=>'https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?w=400&q=75'),
			array('name'=>'Production & assembly Professional','price'=>'AED 5,000','image'=>'https://images.unsplash.com/photo-1533630160021-65bc74fce76d?w=400&q=75'),
			array('name'=>'Production & assembly Enterprise','price'=>'AED 950','image'=>'https://images.unsplash.com/photo-1523580494863-6f3031224c94?w=400&q=75'),
		)),
		'Quality control / QC' => array('photo'=>'https://images.unsplash.com/photo-1565793298595-6a879b1d9492?w=400&q=75','desc'=>'Specialized quality control / qc solutions','products'=>array(
			array('name'=>'Quality control / QC Basic','price'=>'AED 250','image'=>'https://images.unsplash.com/photo-1597852074816-d933c7d2b988?w=400&q=75'),
			array('name'=>'Quality control / QC Professional','price'=>'AED 45','image'=>'https://images.unsplash.com/photo-1621905252507-b35492cc74b4?w=400&q=75'),
			array('name'=>'Quality control / QC Enterprise','price'=>'AED 1,800','image'=>'https://images.unsplash.com/photo-1564182842519-8a3b2af3e228?w=400&q=75'),
		)),
		'Raw materials & WIP' => array('photo'=>'https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?w=400&q=75','desc'=>'Specialized raw materials & wip solutions','products'=>array(
			array('name'=>'Raw materials & WIP Basic','price'=>'AED 500','image'=>'https://images.unsplash.com/photo-1565793298595-6a879b1d9492?w=400&q=75'),
			array('name'=>'Raw materials & WIP Professional','price'=>'AED 85','image'=>'https://images.unsplash.com/photo-1581244277943-fe4a9c777189?w=400&q=75'),
			array('name'=>'Raw materials & WIP Enterprise','price'=>'AED 75','image'=>'https://images.unsplash.com/photo-1558618666-fcd25c85f82e?w=400&q=75'),
		)),
		'Chemical manufacturing' => array('photo'=>'https://images.unsplash.com/photo-1597852074816-d933c7d2b988?w=400&q=75','desc'=>'Specialized chemical manufacturing solutions','products'=>array(
			array('name'=>'Chemical manufacturing Basic','price'=>'AED 800','image'=>'https://images.unsplash.com/photo-1533630160021-65bc74fce76d?w=400&q=75'),
			array('name'=>'Chemical manufacturing Professional','price'=>'AED 120','image'=>'https://images.unsplash.com/photo-1523580494863-6f3031224c94?w=400&q=75'),
			array('name'=>'Chemical manufacturing Enterprise','price'=>'AED 180','image'=>'https://images.unsplash.com/photo-1563520239648-a22fa3be0e2f?w=400&q=75'),
		)),
		'Plastics & rubber' => array('photo'=>'https://images.unsplash.com/photo-1565793298595-6a879b1d9492?w=400&q=75','desc'=>'Specialized plastics & rubber solutions','products'=>array(
			array('name'=>'Plastics & rubber Basic','price'=>'AED 1,200','image'=>'https://images.unsplash.com/photo-1621905252507-b35492cc74b4?w=400&q=75'),
			array('name'=>'Plastics & rubber Professional','price'=>'AED 350','image'=>'https://images.unsplash.com/photo-1564182842519-8a3b2af3e228?w=400&q=75'),
			array('name'=>'Plastics & rubber Enterprise','price'=>'AED 420','image'=>'https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?w=400&q=75'),
		)),
		'Metal & steel fabrication' => array('photo'=>'https://images.unsplash.com/photo-1533630160021-65bc74fce76d?w=400&q=75','desc'=>'Specialized metal & steel fabrication solutions','products'=>array(
			array('name'=>'Metal & steel fabrication Basic','price'=>'AED 2,500','image'=>'https://images.unsplash.com/photo-1581244277943-fe4a9c777189?w=400&q=75'),
			array('name'=>'Metal & steel fabrication Professional','price'=>'AED 650','image'=>'https://images.unsplash.com/photo-1558618666-fcd25c85f82e?w=400&q=75'),
			array('name'=>'Metal & steel fabrication Enterprise','price'=>'AED 720','image'=>'https://images.unsplash.com/photo-1565793298595-6a879b1d9492?w=400&q=75'),
		)),
		'Printing & packaging' => array('photo'=>'https://images.unsplash.com/photo-1621905252507-b35492cc74b4?w=400&q=75','desc'=>'Specialized printing & packaging solutions','products'=>array(
			array('name'=>'Printing & packaging Basic','price'=>'AED 3,500','image'=>'https://images.unsplash.com/photo-1523580494863-6f3031224c94?w=400&q=75'),
			array('name'=>'Printing & packaging Professional','price'=>'AED 950','image'=>'https://images.unsplash.com/photo-1563520239648-a22fa3be0e2f?w=400&q=75'),
			array('name'=>'Printing & packaging Enterprise','price'=>'AED 1,500','image'=>'https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?w=400&q=75'),
		)),
		'Textile manufacturing' => array('photo'=>'https://images.unsplash.com/photo-1581244277943-fe4a9c777189?w=400&q=75','desc'=>'Specialized textile manufacturing solutions','products'=>array(
			array('name'=>'Textile manufacturing Basic','price'=>'AED 5,000','image'=>'https://images.unsplash.com/photo-1564182842519-8a3b2af3e228?w=400&q=75'),
			array('name'=>'Textile manufacturing Professional','price'=>'AED 1,800','image'=>'https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?w=400&q=75'),
			array('name'=>'Textile manufacturing Enterprise','price'=>'AED 150','image'=>'https://images.unsplash.com/photo-1597852074816-d933c7d2b988?w=400&q=75'),
		)),
		'Electronics manufacturing' => array('photo'=>'https://images.unsplash.com/photo-1523580494863-6f3031224c94?w=400&q=75','desc'=>'Specialized electronics manufacturing solutions','products'=>array(
			array('name'=>'Electronics manufacturing Basic','price'=>'AED 45','image'=>'https://images.unsplash.com/photo-1558618666-fcd25c85f82e?w=400&q=75'),
			array('name'=>'Electronics manufacturing Professional','price'=>'AED 75','image'=>'https://images.unsplash.com/photo-1565793298595-6a879b1d9492?w=400&q=75'),
			array('name'=>'Electronics manufacturing Enterprise','price'=>'AED 250','image'=>'https://images.unsplash.com/photo-1565793298595-6a879b1d9492?w=400&q=75'),
		)),
		'Food processing' => array('photo'=>'https://images.unsplash.com/photo-1564182842519-8a3b2af3e228?w=400&q=75','desc'=>'Specialized food processing solutions','products'=>array(
			array('name'=>'Food processing Basic','price'=>'AED 85','image'=>'https://images.unsplash.com/photo-1563520239648-a22fa3be0e2f?w=400&q=75'),
			array('name'=>'Food processing Professional','price'=>'AED 180','image'=>'https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?w=400&q=75'),
			array('name'=>'Food processing Enterprise','price'=>'AED 500','image'=>'https://images.unsplash.com/photo-1533630160021-65bc74fce76d?w=400&q=75'),
		)),
		'Pharmaceutical manufacturing' => array('photo'=>'https://images.unsplash.com/photo-1558618666-fcd25c85f82e?w=400&q=75','desc'=>'Specialized pharmaceutical manufacturing solutions','products'=>array(
			array('name'=>'Pharmaceutical manufacturing Basic','price'=>'AED 120','image'=>'https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?w=400&q=75'),
			array('name'=>'Pharmaceutical manufacturing Professional','price'=>'AED 420','image'=>'https://images.unsplash.com/photo-1597852074816-d933c7d2b988?w=400&q=75'),
			array('name'=>'Pharmaceutical manufacturing Enterprise','price'=>'AED 800','image'=>'https://images.unsplash.com/photo-1621905252507-b35492cc74b4?w=400&q=75'),
		)),
		'Woodwork & carpentry' => array('photo'=>'https://images.unsplash.com/photo-1563520239648-a22fa3be0e2f?w=400&q=75','desc'=>'Specialized woodwork & carpentry solutions','products'=>array(
			array('name'=>'Woodwork & carpentry Basic','price'=>'AED 350','image'=>'https://images.unsplash.com/photo-1565793298595-6a879b1d9492?w=400&q=75'),
			array('name'=>'Woodwork & carpentry Professional','price'=>'AED 720','image'=>'https://images.unsplash.com/photo-1565793298595-6a879b1d9492?w=400&q=75'),
			array('name'=>'Woodwork & carpentry Enterprise','price'=>'AED 1,200','image'=>'https://images.unsplash.com/photo-1581244277943-fe4a9c777189?w=400&q=75'),
		)),
		'Glass manufacturing' => array('photo'=>'https://images.unsplash.com/photo-1581091226825-a6a2a5aee158?w=400&q=75','desc'=>'Specialized glass manufacturing solutions','products'=>array(
			array('name'=>'Glass manufacturing Basic','price'=>'AED 650','image'=>'https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?w=400&q=75'),
			array('name'=>'Glass manufacturing Professional','price'=>'AED 1,500','image'=>'https://images.unsplash.com/photo-1533630160021-65bc74fce76d?w=400&q=75'),
			array('name'=>'Glass manufacturing Enterprise','price'=>'AED 2,500','image'=>'https://images.unsplash.com/photo-1523580494863-6f3031224c94?w=400&q=75'),
		)),
		'3D printing & additive' => array('photo'=>'https://images.unsplash.com/photo-1565793298595-6a879b1d9492?w=400&q=75','desc'=>'Specialized 3d printing & additive solutions','products'=>array(
			array('name'=>'3D printing & additive Basic','price'=>'AED 950','image'=>'https://images.unsplash.com/photo-1597852074816-d933c7d2b988?w=400&q=75'),
			array('name'=>'3D printing & additive Professional','price'=>'AED 150','image'=>'https://images.unsplash.com/photo-1621905252507-b35492cc74b4?w=400&q=75'),
			array('name'=>'3D printing & additive Enterprise','price'=>'AED 3,500','image'=>'https://images.unsplash.com/photo-1564182842519-8a3b2af3e228?w=400&q=75'),
		)),
		'Ceramics & pottery' => array('photo'=>'https://images.unsplash.com/photo-1504917595217-d4dc5ebe6122?w=400&q=75','desc'=>'Specialized ceramics & pottery solutions','products'=>array(
			array('name'=>'Ceramics & pottery Basic','price'=>'AED 1,800','image'=>'https://images.unsplash.com/photo-1565793298595-6a879b1d9492?w=400&q=75'),
			array('name'=>'Ceramics & pottery Professional','price'=>'AED 250','image'=>'https://images.unsplash.com/photo-1581244277943-fe4a9c777189?w=400&q=75'),
			array('name'=>'Ceramics & pottery Enterprise','price'=>'AED 5,000','image'=>'https://images.unsplash.com/photo-1558618666-fcd25c85f82e?w=400&q=75'),
		)),
		'Paper & pulp' => array('photo'=>'https://images.unsplash.com/photo-1597852074816-d933c7d2b988?w=400&q=75','desc'=>'Specialized paper & pulp solutions','products'=>array(
			array('name'=>'Paper & pulp Basic','price'=>'AED 75','image'=>'https://images.unsplash.com/photo-1533630160021-65bc74fce76d?w=400&q=75'),
			array('name'=>'Paper & pulp Professional','price'=>'AED 500','image'=>'https://images.unsplash.com/photo-1523580494863-6f3031224c94?w=400&q=75'),
			array('name'=>'Paper & pulp Enterprise','price'=>'AED 45','image'=>'https://images.unsplash.com/photo-1563520239648-a22fa3be0e2f?w=400&q=75'),
		)),
	),
	'testimonial' => array('quote' => 'BOM accuracy went from 85% to 99.5% — game changer for our production planning.', 'author' => 'Eng. Mohammed S., Manufacturing Plant, Jebel Ali'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
