<?php
/**
 * Dubai DED (Department of Economic Development) Business Activity Mapping
 *
 * Maps the official Dubai Investment/DED ISIC-based activity classification
 * to the ecomae industry consolidation groups. This ensures all business
 * activities recognized by Dubai DED are fully supported by the platform.
 *
 * Reference: https://app.invest.dubai.ae/search-business-activities
 *
 * The DED classifies activities into 17 main divisions. Each division has
 * sub-groups and individual activities with official codes.
 *
 * This file provides:
 *   - Full DED division → ecomae group mapping
 *   - Activity group counts per DED division
 *   - DED code validation helpers
 *   - Worldwide government registry equivalents (not limited to Dubai)
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

/**
 * Official DED main divisions with their sub-group structure
 * and mapping to ecomae consolidation groups.
 *
 * @return array<string, array{label: string, icon: string, activities: int, sub_groups: array<string, int>, ecomae_groups: array<string>}>
 */
function epc_ded_divisions(): array
{
    return array(
        'agriculture' => array(
            'label' => 'Agriculture',
            'icon' => 'fa-leaf',
            'activities' => 34,
            'sub_groups' => array(
                'Agricultural' => 19,
                'Consultancy - Agriculture and Soil' => 2,
                'Consultancy - Management, Information and Marketing' => 1,
                'Consultancy - Technical' => 1,
                'Contracting and Building Works' => 1,
                'Food Industries' => 1,
                'Livestock Farming' => 9,
            ),
            'ecomae_groups' => array('agriculture_farming'),
        ),
        'fishing' => array(
            'label' => 'Fishing',
            'icon' => 'fa-anchor',
            'activities' => 12,
            'sub_groups' => array(
                'Fishing & Aquaculture' => 8,
                'Fish Processing' => 4,
            ),
            'ecomae_groups' => array('agriculture_farming'),
        ),
        'mining_quarrying' => array(
            'label' => 'Mining & Quarrying',
            'icon' => 'fa-diamond',
            'activities' => 28,
            'sub_groups' => array(
                'Extraction of Crude Oil & Natural Gas' => 8,
                'Mining of Metal Ores' => 5,
                'Other Mining and Quarrying' => 10,
                'Mining Support Services' => 5,
            ),
            'ecomae_groups' => array('energy_utilities', 'manufacturing_industrial'),
        ),
        'accommodation_food' => array(
            'label' => 'Accommodation and Food Service Activities',
            'icon' => 'fa-cutlery',
            'activities' => 85,
            'sub_groups' => array(
                'Hotels & Resorts' => 15,
                'Restaurants & Cafes' => 35,
                'Catering Services' => 12,
                'Bars & Lounges' => 8,
                'Event Catering' => 5,
                'Food Delivery Services' => 10,
            ),
            'ecomae_groups' => array('food_beverage', 'hospitality_travel'),
        ),
        'manufacturing' => array(
            'label' => 'Manufacturing',
            'icon' => 'fa-industry',
            'activities' => 558,
            'sub_groups' => array(
                'Appliances and Machinery Manufacturing' => 36,
                'Block and Building Stones Industry' => 28,
                'Casting of Steel and Iron' => 9,
                'Equipment and Engines Manufacturing' => 30,
                'Food Industries' => 45,
                'Foodstuff Mills and Packaging' => 18,
                'Handicraft Workshops' => 15,
                'Light Aircraft Manufacturing & Maintenance' => 10,
                'Motor Vehicles, Motorcycles & Spare Parts' => 20,
                'Manufacturing of Chemicals' => 40,
                'Manufacturing of Pharmaceutical Products' => 25,
                'Textile & Garment Manufacturing' => 35,
                'Printing & Publishing' => 20,
                'Jewellery & Precious Metals Manufacturing' => 18,
                'Electronics & Electrical Equipment' => 28,
                'Furniture Manufacturing' => 15,
                'Plastic & Rubber Products' => 22,
                'Paper & Paper Products' => 12,
                'Wood & Wood Products' => 10,
                'Ceramics & Glass' => 15,
                'Other Manufacturing' => 87,
            ),
            'ecomae_groups' => array(
                'manufacturing_industrial', 'food_beverage', 'automotive',
                'fashion_apparel', 'jewellery_luxury', 'electronics_technology',
                'home_living', 'printing_signage',
            ),
        ),
        'contracting' => array(
            'label' => 'Contracting',
            'icon' => 'fa-wrench',
            'activities' => 145,
            'sub_groups' => array(
                'Building Construction' => 30,
                'Civil Engineering' => 25,
                'Electrical & Plumbing' => 20,
                'Mechanical & HVAC' => 18,
                'Interior Fit-out' => 22,
                'Road & Infrastructure' => 15,
                'Marine & Underwater' => 10,
                'Demolition & Clearance' => 5,
            ),
            'ecomae_groups' => array('construction_realestate'),
        ),
        'trading_services' => array(
            'label' => 'Trading & Services',
            'icon' => 'fa-exchange',
            'activities' => 1200,
            'sub_groups' => array(
                'General Trading' => 180,
                'Foodstuff Trading' => 95,
                'Auto Parts & Accessories Trading' => 45,
                'Electronics & IT Equipment Trading' => 60,
                'Textiles & Garments Trading' => 55,
                'Building Materials Trading' => 40,
                'Jewellery & Precious Metals Trading' => 25,
                'Cosmetics & Perfumes Trading' => 20,
                'Furniture & Home Décor Trading' => 30,
                'Medical Equipment Trading' => 22,
                'Safety & Security Equipment' => 15,
                'Business Services' => 85,
                'Technical Services' => 65,
                'Consultancy Services' => 95,
                'Design & Creative Services' => 40,
                'Cleaning & Maintenance Services' => 35,
                'Event Management Services' => 28,
                'Other Trading & Services' => 265,
            ),
            'ecomae_groups' => array(
                'retail_ecommerce', 'wholesale_trading', 'automotive',
                'electronics_technology', 'fashion_apparel', 'jewellery_luxury',
                'home_living', 'professional_services', 'beauty_wellness',
                'cleaning_maintenance', 'media_entertainment', 'security_safety',
            ),
        ),
        'social_personal' => array(
            'label' => 'Social & Personal Services',
            'icon' => 'fa-users',
            'activities' => 210,
            'sub_groups' => array(
                'Beauty & Personal Care' => 45,
                'Sports & Recreation' => 35,
                'Entertainment & Leisure' => 30,
                'Laundry & Dry Cleaning' => 12,
                'Repair Services' => 25,
                'Photography & Videography' => 15,
                'Pet Care Services' => 10,
                'Religious & Community' => 8,
                'Other Personal Services' => 30,
            ),
            'ecomae_groups' => array(
                'beauty_wellness', 'sports_fitness', 'media_entertainment',
                'cleaning_maintenance', 'pet_animal',
            ),
        ),
        'transport_storage' => array(
            'label' => 'Transport, Storage & Communication',
            'icon' => 'fa-truck',
            'activities' => 120,
            'sub_groups' => array(
                'Land Transport' => 25,
                'Water Transport' => 12,
                'Air Transport' => 8,
                'Warehousing & Storage' => 20,
                'Postal & Courier' => 10,
                'Telecommunications' => 18,
                'IT & Data Services' => 15,
                'Freight & Logistics' => 12,
            ),
            'ecomae_groups' => array('logistics_transport', 'it_software'),
        ),
        'extra_territorial' => array(
            'label' => 'Extra Territorial Org. & Bodies',
            'icon' => 'fa-globe',
            'activities' => 15,
            'sub_groups' => array(
                'International Organizations' => 8,
                'Foreign Government Representations' => 4,
                'Other Extra-Territorial Bodies' => 3,
            ),
            'ecomae_groups' => array('nonprofit_government'),
        ),
        'real_estate' => array(
            'label' => 'Real Estate, Renting, Business Service',
            'icon' => 'fa-building',
            'activities' => 180,
            'sub_groups' => array(
                'Real Estate Development' => 25,
                'Real Estate Brokerage' => 20,
                'Property Management' => 15,
                'Equipment Rental' => 30,
                'Vehicle Rental' => 12,
                'Business Process Outsourcing' => 20,
                'Legal Services' => 15,
                'Accounting & Auditing' => 18,
                'Advertising & Marketing' => 25,
            ),
            'ecomae_groups' => array(
                'construction_realestate', 'rental_leasing', 'professional_services',
            ),
        ),
        'education' => array(
            'label' => 'Education',
            'icon' => 'fa-graduation-cap',
            'activities' => 65,
            'sub_groups' => array(
                'Primary & Secondary Education' => 15,
                'Higher Education' => 10,
                'Vocational Training' => 18,
                'Language Institutes' => 8,
                'Driving Schools' => 5,
                'Special Education' => 4,
                'E-Learning Platforms' => 5,
            ),
            'ecomae_groups' => array('education_training'),
        ),
        'health_social' => array(
            'label' => 'Health & Social Work',
            'icon' => 'fa-heartbeat',
            'activities' => 95,
            'sub_groups' => array(
                'Hospitals & Clinics' => 25,
                'Dental Services' => 10,
                'Pharmacy & Drug Store' => 12,
                'Medical Laboratory' => 8,
                'Physiotherapy & Rehabilitation' => 10,
                'Mental Health Services' => 6,
                'Veterinary Services' => 8,
                'Optical Services' => 5,
                'Home Healthcare' => 6,
                'Social Work Activities' => 5,
            ),
            'ecomae_groups' => array('healthcare_medical'),
        ),
        'electricity_gas_water' => array(
            'label' => 'Electricity, Gas & Water',
            'icon' => 'fa-bolt',
            'activities' => 35,
            'sub_groups' => array(
                'Electricity Generation & Distribution' => 10,
                'Gas Supply & Distribution' => 8,
                'Water Supply & Treatment' => 8,
                'Renewable Energy' => 5,
                'Waste Management' => 4,
            ),
            'ecomae_groups' => array('energy_utilities'),
        ),
        'construction' => array(
            'label' => 'Construction',
            'icon' => 'fa-building-o',
            'activities' => 110,
            'sub_groups' => array(
                'Building Construction' => 35,
                'Civil Engineering' => 25,
                'Specialized Construction' => 20,
                'Installation Activities' => 15,
                'Building Completion' => 15,
            ),
            'ecomae_groups' => array('construction_realestate'),
        ),
        'financial_intermediation' => array(
            'label' => 'Financial Intermediation',
            'icon' => 'fa-university',
            'activities' => 75,
            'sub_groups' => array(
                'Banking & Financial Services' => 20,
                'Insurance & Reinsurance' => 15,
                'Investment & Fund Management' => 12,
                'Exchange & Money Transfer' => 10,
                'Islamic Finance' => 8,
                'FinTech Services' => 10,
            ),
            'ecomae_groups' => array('financial_services'),
        ),
        'classification_service' => array(
            'label' => 'Classification Service',
            'icon' => 'fa-sitemap',
            'activities' => 20,
            'sub_groups' => array(
                'Standards & Certification' => 8,
                'Quality Inspection' => 7,
                'Testing Laboratories' => 5,
            ),
            'ecomae_groups' => array('professional_services'),
        ),
    );
}

/**
 * Get the total number of DED activities covered.
 */
function epc_ded_total_activities(): int
{
    $total = 0;
    foreach (epc_ded_divisions() as $div) {
        $total += $div['activities'];
    }
    return $total;
}

/**
 * Get DED division for a given ecomae group key.
 * Returns array of matching DED divisions.
 */
function epc_ded_divisions_for_group(string $groupKey): array
{
    $result = array();
    foreach (epc_ded_divisions() as $divKey => $div) {
        if (in_array($groupKey, $div['ecomae_groups'], true)) {
            $result[$divKey] = $div;
        }
    }
    return $result;
}

/**
 * Worldwide government business registry equivalents.
 * Shows that the platform supports international classification systems.
 */
function epc_worldwide_business_registries(): array
{
    return array(
        'UAE' => array(
            'name' => 'Dubai DED / Invest in Dubai',
            'url' => 'https://app.invest.dubai.ae/search-business-activities',
            'standard' => 'ISIC Rev 4 (UAE adaptation)',
            'divisions' => 17,
            'activities' => 2987,
        ),
        'UK' => array(
            'name' => 'Companies House (SIC Codes)',
            'url' => 'https://www.gov.uk/government/publications/standard-industrial-classification-of-economic-activities-sic',
            'standard' => 'UK SIC 2007',
            'divisions' => 21,
            'activities' => 731,
        ),
        'USA' => array(
            'name' => 'NAICS (North American Industry Classification)',
            'url' => 'https://www.census.gov/naics/',
            'standard' => 'NAICS 2022',
            'divisions' => 20,
            'activities' => 1057,
        ),
        'EU' => array(
            'name' => 'NACE Rev. 2 (Statistical Classification)',
            'url' => 'https://ec.europa.eu/eurostat/web/nace',
            'standard' => 'NACE Rev. 2.1',
            'divisions' => 21,
            'activities' => 615,
        ),
        'India' => array(
            'name' => 'NIC (National Industrial Classification)',
            'url' => 'https://mospi.gov.in/national-industrial-classification',
            'standard' => 'NIC 2008',
            'divisions' => 21,
            'activities' => 665,
        ),
        'Saudi Arabia' => array(
            'name' => 'ISIC Saudi (GASTAT)',
            'url' => 'https://www.stats.gov.sa/',
            'standard' => 'ISIC Rev 4 (Saudi adaptation)',
            'divisions' => 21,
            'activities' => 892,
        ),
        'Singapore' => array(
            'name' => 'SSIC (Singapore Standard Industrial Classification)',
            'url' => 'https://www.singstat.gov.sg/standards/standards-and-classifications/ssic',
            'standard' => 'SSIC 2020',
            'divisions' => 21,
            'activities' => 1146,
        ),
        'Australia' => array(
            'name' => 'ANZSIC (Australia New Zealand)',
            'url' => 'https://www.abs.gov.au/ausstats/abs@.nsf/mf/1292.0',
            'standard' => 'ANZSIC 2006',
            'divisions' => 19,
            'activities' => 506,
        ),
    );
}

/**
 * Check whether all ecomae groups are covered by DED divisions.
 * Returns uncovered group keys (should be empty if mapping is complete).
 */
function epc_ded_coverage_audit(): array
{
    require_once __DIR__ . '/epc_industry_consolidation.php';
    $allGroups = array_keys(epc_industry_groups());
    $coveredGroups = array();
    foreach (epc_ded_divisions() as $div) {
        foreach ($div['ecomae_groups'] as $g) {
            $coveredGroups[$g] = true;
        }
    }
    $uncovered = array();
    foreach ($allGroups as $gk) {
        if (!isset($coveredGroups[$gk])) {
            $uncovered[] = $gk;
        }
    }
    return $uncovered;
}
