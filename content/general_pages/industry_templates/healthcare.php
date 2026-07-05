<?php
/**
 * Healthcare & Medical — Industry Template
 * Auto-generated industry frontend with sample data.
 */
defined('_ASTEXE_') or die('No access');

$industryData = array(
	'name' => 'Healthcare & Medical',
	'tagline' => 'Patient management + pharma compliance',
	'description' => 'Hospitals, clinics, pharma, dental, veterinary, wellness, and medical supplies.',
	'icon' => 'fa-heartbeat',
	'color_primary' => '#0284c7',
	'color_accent' => '#22d3ee',
	'bg_from' => '#0c4a6e',
	'bg_to' => '#155e75',
	'hero_animation' => 'fadeInUp',
	'demo_key' => 'healthcare_medical',
	'product_label' => 'Products & Services',
	'sample_products' => array(
		array('name' => 'Patient Consultation', 'price' => 'AED 200', 'icon' => 'fa-stethoscope', 'category' => 'Clinic'),
		array('name' => 'Blood Test Panel', 'price' => 'AED 350', 'icon' => 'fa-flask', 'category' => 'Lab'),
		array('name' => 'MRI Scan', 'price' => 'AED 2,500', 'icon' => 'fa-medkit', 'category' => 'Imaging'),
		array('name' => 'Dental Cleaning', 'price' => 'AED 300', 'icon' => 'fa-smile-o', 'category' => 'Dental'),
		array('name' => 'Physiotherapy Session', 'price' => 'AED 250', 'icon' => 'fa-user-md', 'category' => 'Rehab'),
		array('name' => 'Prescription Refill', 'price' => 'AED 45', 'icon' => 'fa-pills', 'category' => 'Pharmacy'),
	),
	'features' => array(
		array('title' => 'EMR Integration', 'icon' => 'fa-file-text', 'desc' => 'Electronic medical records with HL7/FHIR compliance'),
		array('title' => 'Appointment Scheduling', 'icon' => 'fa-calendar-check-o', 'desc' => 'Multi-provider calendars with patient portal booking'),
		array('title' => 'Drug Inventory', 'icon' => 'fa-database', 'desc' => 'Batch tracking, expiry alerts and controlled substance log'),
		array('title' => 'Insurance Claims', 'icon' => 'fa-hospital-o', 'desc' => 'DHA/HAAD/SEHA compliant e-claims submission'),
		array('title' => 'Lab Integration', 'icon' => 'fa-flask', 'desc' => 'Automated lab order routing and result import'),
		array('title' => 'Telemedicine', 'icon' => 'fa-video-camera', 'desc' => 'Video consultation with prescription and billing'),
	),
	'stats' => array(
		array('value' => '500K+', 'label' => 'Patients Managed'),
		array('value' => '98%', 'label' => 'Claim Approval'),
		array('value' => 'HIPAA', 'label' => 'Compliant'),
		array('value' => '24/7', 'label' => 'Support'),
	),
	'sub_industries' => array(
		'Clinic & patient management',
		'Hospital operations',
		'Pharmacy & drug dispensing',
		'Dental practice',
		'Veterinary services',
		'Optical & eyewear',
		'Medical equipment supply',
		'Laboratory & diagnostics',
		'Wellness & holistic health',
		'Mental health & counseling',
		'Physiotherapy & rehab',
		'Home healthcare',
		'Telemedicine & telehealth',
		'Cosmetic surgery',
		'Ambulance & emergency',
		'Fertility clinic',
		'Addiction treatment',
	),
	'testimonial' => array('quote' => 'Streamlined our multi-clinic operations and cut claim rejections by 60%.', 'author' => 'Dr. Sarah M., Medical Director, Abu Dhabi'),
	'cta_cp_text' => 'Open Control Panel',
	'cta_erp_text' => 'Launch ERP',
);

require __DIR__ . '/_base_template.php';
