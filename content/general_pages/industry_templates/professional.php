<?php
/**
 * Professional & Business Services — Industry Template
 * Auto-generated industry frontend with sample data.
 */
defined('_ASTEXE_') or die('No access');

$industryData = array(
	'name' => 'Professional & Business Services',
	'tagline' => 'Time billing + project delivery + client CRM',
	'description' => 'Consulting, legal, accounting, HR, marketing, and advisory firms.',
	'icon' => 'fa-briefcase',
	'color_primary' => '#7c3aed',
	'color_accent' => '#a78bfa',
	'bg_from' => '#2e1065',
	'bg_to' => '#5b21b6',
	'hero_animation' => 'fadeInUp',
	'demo_key' => 'professional_services',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'Business Consulting', 'price' => 'AED 500/hr', 'icon' => 'fa-briefcase', 'category' => 'Consulting'),
		array('name' => 'Legal Advisory', 'price' => 'AED 800/hr', 'icon' => 'fa-gavel', 'category' => 'Legal'),
		array('name' => 'Tax Filing Service', 'price' => 'AED 2,500', 'icon' => 'fa-file-text', 'category' => 'Tax'),
		array('name' => 'HR Recruitment', 'price' => '15% Salary', 'icon' => 'fa-user-plus', 'category' => 'HR'),
		array('name' => 'Marketing Campaign', 'price' => 'AED 15,000', 'icon' => 'fa-bullhorn', 'category' => 'Marketing'),
		array('name' => 'Audit & Assurance', 'price' => 'AED 8,000', 'icon' => 'fa-search', 'category' => 'Audit'),
	),
	'features' => array(
		array('title' => 'Time Tracking', 'icon' => 'fa-clock-o', 'desc' => 'Billable hours with timesheet approval workflow'),
		array('title' => 'Project Management', 'icon' => 'fa-tasks', 'desc' => 'Milestones, deliverables and resource allocation'),
		array('title' => 'Client Portal', 'icon' => 'fa-user-circle', 'desc' => 'Secure document sharing and status updates'),
		array('title' => 'Invoice Automation', 'icon' => 'fa-file-pdf-o', 'desc' => 'Time-based and fixed-fee invoice generation'),
		array('title' => 'Knowledge Base', 'icon' => 'fa-book', 'desc' => 'Internal wikis, precedents and templates library'),
		array('title' => 'Pipeline CRM', 'icon' => 'fa-funnel', 'desc' => 'Opportunity tracking with proposal automation'),
	),
	'stats' => array(
		array('value' => '3,500+', 'label' => 'Firms'),
		array('value' => 'AED 1.2B', 'label' => 'Billed Annually'),
		array('value' => '94%', 'label' => 'Retention Rate'),
		array('value' => '22', 'label' => 'Practice Areas'),
	),
	'sub_industries' => array(
		'Management consulting',
		'Law firm & legal services',
		'Accounting & bookkeeping',
		'Tax advisory & compliance',
		'Audit & assurance',
		'HR & recruitment',
		'Marketing & advertising',
		'Public relations',
		'IT consulting & services',
		'Architecture & engineering',
		'Client relationship management',
		'Time billing & invoicing',
		'Translation & localization',
		'Research & analytics',
		'Corporate training',
	),
	'testimonial' => array('quote' => 'Automated our monthly billing from 3 days to 2 hours. Clients love the portal.', 'author' => 'James W., Managing Partner, DIFC'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
