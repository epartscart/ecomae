<?php
/**
 * Industry Consolidation Engine
 *
 * Maps 1154 individual industries into ~28 "super groups" that share
 * one frontend template each. Clients still pick their specific industry
 * (e.g., "3D Printing Bureau") but the platform routes them to the shared
 * template group (e.g., "Manufacturing & Production") with only relevant
 * sub-areas toggled active.
 *
 * Benefits:
 *   - Fewer frontend templates to maintain (28 vs 1154)
 *   - Shared CSS/JS/layout per group reduces server load
 *   - CP adapts based on active sub-areas (toggle system)
 *   - ERP toolkit remains industry-specific but uses group-level config
 *   - New industries auto-map to existing groups without new code
 */

declare(strict_types=1);

defined('_ASTEXE_') or die('No access');

/**
 * Master list of consolidated industry super groups.
 *
 * Each group defines:
 *   - label: Display name
 *   - icon: FontAwesome icon class
 *   - template_key: Frontend template file identifier
 *   - description: What this group covers
 *   - default_sub_areas: Sub-areas that new tenants get activated by default
 *   - available_sub_areas: All possible sub-areas a tenant can toggle
 *   - erp_base: Base ERP industry pack key (from epc_erp_industry_packs)
 *   - costing_default: Default costing method for this group
 *   - hero_style: Frontend hero section style
 *   - color_scheme: Default color palette
 *
 * @return array<string, array<string, mixed>>
 */
function epc_industry_groups(): array
{
    return array(
        'automotive' => array(
            'label' => 'Automotive & Vehicles',
            'icon' => 'fa-car',
            'template_key' => 'automotive',
            'description' => 'Auto parts, repair, dealerships, detailing, and vehicle services.',
            'default_sub_areas' => array('parts_catalog', 'workshop', 'inventory'),
            'available_sub_areas' => array(
                'parts_catalog' => 'Parts catalog & cross-references',
                'workshop' => 'Workshop / garage management',
                'dealership' => 'Vehicle dealership & sales',
                'detailing' => 'Detailing & car care',
                'insurance' => 'Auto insurance processing',
                'auction' => 'Vehicle auctions',
                'rental' => 'Car rental & leasing',
                'logistics' => 'Automotive logistics',
                'manufacturing' => 'Vehicle/parts manufacturing',
                'tyres' => 'Tyre shop & fitting',
                'glass' => 'Windshield & auto glass',
                'ev_charging' => 'EV charging stations',
                'fleet' => 'Fleet management',
            ),
            'erp_base' => 'automotive_workshop',
            'costing_default' => 'weighted_avg',
            'hero_style' => 'parallax_dark',
            'color_scheme' => array('primary' => '#dc2626', 'accent' => '#f97316', 'bg_from' => '#0b1220', 'bg_to' => '#1e3a5f'),
        ),

        'healthcare_medical' => array(
            'label' => 'Healthcare & Medical',
            'icon' => 'fa-heartbeat',
            'template_key' => 'healthcare',
            'description' => 'Hospitals, clinics, pharma, dental, veterinary, wellness, and medical supplies.',
            'default_sub_areas' => array('clinic', 'pharmacy', 'equipment'),
            'available_sub_areas' => array(
                'clinic' => 'Clinic & patient management',
                'hospital' => 'Hospital operations',
                'pharmacy' => 'Pharmacy & drug dispensing',
                'dental' => 'Dental practice',
                'veterinary' => 'Veterinary services',
                'optical' => 'Optical & eyewear',
                'equipment' => 'Medical equipment supply',
                'laboratory' => 'Laboratory & diagnostics',
                'wellness' => 'Wellness & holistic health',
                'mental_health' => 'Mental health & counseling',
                'physiotherapy' => 'Physiotherapy & rehab',
                'home_care' => 'Home healthcare',
                'telemedicine' => 'Telemedicine & telehealth',
                'cosmetic_surgery' => 'Cosmetic surgery',
                'ambulance' => 'Ambulance & emergency',
                'fertility' => 'Fertility clinic',
                'addiction' => 'Addiction treatment',
            ),
            'erp_base' => 'pharma_healthcare',
            'costing_default' => 'fifo',
            'hero_style' => 'clean_modern',
            'color_scheme' => array('primary' => '#0284c7', 'accent' => '#22d3ee', 'bg_from' => '#0c4a6e', 'bg_to' => '#155e75'),
        ),

        'food_beverage' => array(
            'label' => 'Food & Beverage',
            'icon' => 'fa-cutlery',
            'template_key' => 'food_beverage',
            'description' => 'Restaurants, bakeries, cafes, catering, food manufacturing, and wholesale.',
            'default_sub_areas' => array('restaurant', 'kitchen', 'pos'),
            'available_sub_areas' => array(
                'restaurant' => 'Restaurant & dine-in',
                'cafe' => 'Cafe & coffee shop',
                'bakery' => 'Bakery & confectionery',
                'catering' => 'Catering & events',
                'food_truck' => 'Food truck & mobile',
                'cloud_kitchen' => 'Cloud kitchen / dark kitchen',
                'bar' => 'Bar & pub',
                'fast_food' => 'Fast food & QSR',
                'kitchen' => 'Kitchen management',
                'pos' => 'POS & ordering',
                'delivery' => 'Delivery & takeaway',
                'food_manufacturing' => 'Food manufacturing',
                'beverage_manufacturing' => 'Beverage production',
                'food_wholesale' => 'Food wholesale & distribution',
                'ice_cream' => 'Ice cream & desserts',
                'butcher' => 'Butcher & meat shop',
                'organic' => 'Organic & health food',
            ),
            'erp_base' => 'fnb_restaurant',
            'costing_default' => 'weighted_avg',
            'hero_style' => 'warm_inviting',
            'color_scheme' => array('primary' => '#ea580c', 'accent' => '#fb923c', 'bg_from' => '#431407', 'bg_to' => '#7c2d12'),
        ),

        'fashion_apparel' => array(
            'label' => 'Fashion & Apparel',
            'icon' => 'fa-shopping-bag',
            'template_key' => 'fashion',
            'description' => 'Clothing, footwear, accessories, textiles, and fashion retail.',
            'default_sub_areas' => array('retail', 'collections', 'ecommerce'),
            'available_sub_areas' => array(
                'retail' => 'Fashion retail store',
                'ecommerce' => 'Online fashion store',
                'collections' => 'Seasonal collections',
                'luxury' => 'Luxury & designer brands',
                'sportswear' => 'Sportswear & activewear',
                'kids' => 'Kids & baby clothing',
                'uniforms' => 'Uniforms & workwear',
                'textiles' => 'Textile manufacturing',
                'tailoring' => 'Custom tailoring',
                'footwear' => 'Shoes & footwear',
                'accessories' => 'Fashion accessories',
                'vintage' => 'Vintage & secondhand',
                'bridal' => 'Bridal & formal wear',
                'swimwear' => 'Swimwear & beachwear',
            ),
            'erp_base' => 'textile_apparel',
            'costing_default' => 'weighted_avg',
            'hero_style' => 'editorial_magazine',
            'color_scheme' => array('primary' => '#be185d', 'accent' => '#ec4899', 'bg_from' => '#1f1020', 'bg_to' => '#701a75'),
        ),

        'jewellery_luxury' => array(
            'label' => 'Jewellery & Luxury Goods',
            'icon' => 'fa-diamond',
            'template_key' => 'jewellery',
            'description' => 'Jewellery, watches, diamonds, gold, and luxury retail.',
            'default_sub_areas' => array('retail', 'gold', 'custom_design'),
            'available_sub_areas' => array(
                'retail' => 'Jewellery retail showroom',
                'gold' => 'Gold buying & selling',
                'diamond' => 'Diamond & gemstones',
                'watches' => 'Watches & timepieces',
                'custom_design' => 'Custom design & making',
                'repair' => 'Jewellery repair & polishing',
                'wholesale' => 'Jewellery wholesale / B2B',
                'manufacturing' => 'Jewellery manufacturing',
                'auction' => 'Luxury auctions',
                'antique' => 'Antique jewellery',
                'bridal' => 'Bridal jewellery',
                'certification' => 'Gem certification & hallmarking',
            ),
            'erp_base' => 'jewellery_diamond',
            'costing_default' => 'specific',
            'hero_style' => 'luxury_showcase',
            'color_scheme' => array('primary' => '#b45309', 'accent' => '#fbbf24', 'bg_from' => '#1c1917', 'bg_to' => '#78350f'),
        ),

        'electronics_technology' => array(
            'label' => 'Electronics & Technology',
            'icon' => 'fa-microchip',
            'template_key' => 'electronics',
            'description' => 'Consumer electronics, computers, gadgets, IT hardware, and tech retail.',
            'default_sub_areas' => array('retail', 'repair', 'ecommerce'),
            'available_sub_areas' => array(
                'retail' => 'Electronics retail store',
                'ecommerce' => 'Online electronics store',
                'repair' => 'Device repair & service',
                'computers' => 'Computers & laptops',
                'mobile' => 'Mobile phones & tablets',
                'gaming' => 'Gaming & consoles',
                'audio' => 'Audio & home theater',
                'smart_home' => 'Smart home & IoT',
                'components' => 'Electronic components',
                'cameras' => 'Cameras & photography',
                'appliances' => 'Home appliances',
                'wholesale' => 'Electronics wholesale',
                'refurbished' => 'Refurbished electronics',
            ),
            'erp_base' => 'general',
            'costing_default' => 'weighted_avg',
            'hero_style' => 'tech_dark',
            'color_scheme' => array('primary' => '#e10a0a', 'accent' => '#000000', 'bg_from' => '#000000', 'bg_to' => '#2d2d2d'),
        ),

        'construction_realestate' => array(
            'label' => 'Construction & Real Estate',
            'icon' => 'fa-building',
            'template_key' => 'construction',
            'description' => 'Construction, contracting, real estate, property management, and building materials.',
            'default_sub_areas' => array('contracting', 'materials', 'project_mgmt'),
            'available_sub_areas' => array(
                'contracting' => 'General contracting',
                'materials' => 'Building materials supply',
                'project_mgmt' => 'Project management',
                'real_estate' => 'Real estate sales & leasing',
                'property_mgmt' => 'Property management',
                'architecture' => 'Architecture & design',
                'interior_design' => 'Interior design & fit-out',
                'plumbing' => 'Plumbing & HVAC',
                'electrical' => 'Electrical contracting',
                'painting' => 'Painting & finishing',
                'landscaping' => 'Landscaping & garden',
                'demolition' => 'Demolition & clearing',
                'surveying' => 'Land surveying',
                'steel_fabrication' => 'Steel fabrication',
                'concrete' => 'Concrete & cement',
                'waterproofing' => 'Waterproofing & insulation',
                'solar_installation' => 'Solar installation',
            ),
            'erp_base' => 'construction_contracting',
            'costing_default' => 'specific',
            'hero_style' => 'industrial_bold',
            'color_scheme' => array('primary' => '#d97706', 'accent' => '#fbbf24', 'bg_from' => '#1c1917', 'bg_to' => '#44403c'),
        ),

        'manufacturing_industrial' => array(
            'label' => 'Manufacturing & Industrial',
            'icon' => 'fa-industry',
            'template_key' => 'manufacturing',
            'description' => 'Factories, production, assembly, chemicals, metals, and industrial operations.',
            'default_sub_areas' => array('production', 'quality', 'inventory'),
            'available_sub_areas' => array(
                'production' => 'Production & assembly',
                'quality' => 'Quality control / QC',
                'inventory' => 'Raw materials & WIP',
                'chemicals' => 'Chemical manufacturing',
                'plastics' => 'Plastics & rubber',
                'metals' => 'Metal & steel fabrication',
                'printing' => 'Printing & packaging',
                'textiles' => 'Textile manufacturing',
                'electronics_mfg' => 'Electronics manufacturing',
                'food_processing' => 'Food processing',
                'pharma_mfg' => 'Pharmaceutical manufacturing',
                'woodwork' => 'Woodwork & carpentry',
                'glass' => 'Glass manufacturing',
                '3d_printing' => '3D printing & additive',
                'ceramics' => 'Ceramics & pottery',
                'paper' => 'Paper & pulp',
            ),
            'erp_base' => 'manufacturing_process',
            'costing_default' => 'weighted_avg',
            'hero_style' => 'industrial_bold',
            'color_scheme' => array('primary' => '#475569', 'accent' => '#94a3b8', 'bg_from' => '#0f172a', 'bg_to' => '#334155'),
        ),

        'professional_services' => array(
            'label' => 'Professional & Business Services',
            'icon' => 'fa-briefcase',
            'template_key' => 'professional',
            'description' => 'Consulting, legal, accounting, HR, marketing, and advisory firms.',
            'default_sub_areas' => array('consulting', 'crm', 'billing'),
            'available_sub_areas' => array(
                'consulting' => 'Management consulting',
                'legal' => 'Law firm & legal services',
                'accounting' => 'Accounting & bookkeeping',
                'tax' => 'Tax advisory & compliance',
                'audit' => 'Audit & assurance',
                'hr' => 'HR & recruitment',
                'marketing' => 'Marketing & advertising',
                'pr' => 'Public relations',
                'it_services' => 'IT consulting & services',
                'architecture' => 'Architecture & engineering',
                'crm' => 'Client relationship management',
                'billing' => 'Time billing & invoicing',
                'translation' => 'Translation & localization',
                'research' => 'Research & analytics',
                'training' => 'Corporate training',
            ),
            'erp_base' => 'services_professional',
            'costing_default' => 'standard',
            'hero_style' => 'corporate_clean',
            'color_scheme' => array('primary' => '#7c3aed', 'accent' => '#a78bfa', 'bg_from' => '#2e1065', 'bg_to' => '#5b21b6'),
        ),

        'education_training' => array(
            'label' => 'Education & Training',
            'icon' => 'fa-graduation-cap',
            'template_key' => 'education',
            'description' => 'Schools, universities, training centers, e-learning, and tutoring.',
            'default_sub_areas' => array('enrollment', 'courses', 'lms'),
            'available_sub_areas' => array(
                'enrollment' => 'Student enrollment',
                'courses' => 'Course management',
                'lms' => 'Learning management system',
                'school' => 'K-12 school',
                'university' => 'University / college',
                'vocational' => 'Vocational training',
                'tutoring' => 'Tutoring & coaching',
                'driving_school' => 'Driving school',
                'language' => 'Language school',
                'art_school' => 'Art & music school',
                'corporate_training' => 'Corporate training',
                'elearning' => 'E-learning platform',
                'certification' => 'Certification & testing',
            ),
            'erp_base' => 'education',
            'costing_default' => 'standard',
            'hero_style' => 'clean_modern',
            'color_scheme' => array('primary' => '#2563eb', 'accent' => '#60a5fa', 'bg_from' => '#1e3a5f', 'bg_to' => '#1e40af'),
        ),

        'hospitality_travel' => array(
            'label' => 'Hospitality & Travel',
            'icon' => 'fa-hotel',
            'template_key' => 'hospitality',
            'description' => 'Hotels, resorts, travel agencies, tourism, and accommodation.',
            'default_sub_areas' => array('booking', 'rooms', 'guest_services'),
            'available_sub_areas' => array(
                'booking' => 'Reservation & booking',
                'rooms' => 'Room management',
                'guest_services' => 'Guest services & concierge',
                'resort' => 'Resort operations',
                'boutique_hotel' => 'Boutique hotel',
                'hostel' => 'Hostel & backpacker',
                'vacation_rental' => 'Vacation rental / Airbnb',
                'travel_agency' => 'Travel agency',
                'tour_operator' => 'Tour operator',
                'airline' => 'Airline & aviation',
                'cruise' => 'Cruise line',
                'adventure' => 'Adventure tourism',
                'spa' => 'Spa & wellness resort',
                'event_venue' => 'Event venue',
                'camping' => 'Camping & glamping',
            ),
            'erp_base' => 'hospitality_hotel',
            'costing_default' => 'standard',
            'hero_style' => 'panoramic',
            'color_scheme' => array('primary' => '#0d9488', 'accent' => '#14b8a6', 'bg_from' => '#042f2e', 'bg_to' => '#115e59'),
        ),

        'beauty_wellness' => array(
            'label' => 'Beauty & Personal Care',
            'icon' => 'fa-magic',
            'template_key' => 'beauty',
            'description' => 'Salons, spas, cosmetics, skincare, and personal care services.',
            'default_sub_areas' => array('salon', 'products', 'booking'),
            'available_sub_areas' => array(
                'salon' => 'Hair salon',
                'barbershop' => 'Barbershop',
                'spa' => 'Day spa & massage',
                'nail' => 'Nail salon',
                'skincare' => 'Skincare & facials',
                'cosmetics' => 'Cosmetics retail',
                'perfume' => 'Perfume & fragrances',
                'tattoo' => 'Tattoo & piercing',
                'beauty_school' => 'Beauty school',
                'products' => 'Beauty product sales',
                'booking' => 'Appointment booking',
                'organic_beauty' => 'Organic & natural beauty',
                'mens_grooming' => "Men's grooming",
            ),
            'erp_base' => 'general',
            'costing_default' => 'weighted_avg',
            'hero_style' => 'elegant_soft',
            'color_scheme' => array('primary' => '#db2777', 'accent' => '#f472b6', 'bg_from' => '#500724', 'bg_to' => '#831843'),
        ),

        'retail_ecommerce' => array(
            'label' => 'Retail & E-commerce',
            'icon' => 'fa-shopping-cart',
            'template_key' => 'retail',
            'description' => 'General retail, online stores, supermarkets, and consumer goods.',
            'default_sub_areas' => array('storefront', 'pos', 'inventory'),
            'available_sub_areas' => array(
                'storefront' => 'Online storefront',
                'pos' => 'Point of sale',
                'inventory' => 'Inventory management',
                'supermarket' => 'Supermarket / grocery',
                'convenience' => 'Convenience store',
                'department_store' => 'Department store',
                'marketplace' => 'Marketplace / multi-vendor',
                'wholesale' => 'Wholesale club',
                'discount' => 'Discount / outlet',
                'subscription' => 'Subscription box',
                'gift_shop' => 'Gift shop',
                'toy_store' => 'Toy store',
                'bookstore' => 'Bookstore & stationery',
                'pet_shop' => 'Pet shop',
                'home_decor' => 'Home decor & furnishing',
            ),
            'erp_base' => 'retail_pos',
            'costing_default' => 'weighted_avg',
            'hero_style' => 'product_showcase',
            'color_scheme' => array('primary' => '#059669', 'accent' => '#34d399', 'bg_from' => '#064e3b', 'bg_to' => '#065f46'),
        ),

        'agriculture_farming' => array(
            'label' => 'Agriculture & Farming',
            'icon' => 'fa-leaf',
            'template_key' => 'agriculture',
            'description' => 'Crop farming, livestock, aquaculture, agritech, and agricultural trading.',
            'default_sub_areas' => array('farming', 'trading', 'storage'),
            'available_sub_areas' => array(
                'farming' => 'Crop farming & cultivation',
                'livestock' => 'Livestock & poultry',
                'aquaculture' => 'Aquaculture & fisheries',
                'dairy' => 'Dairy farming',
                'organic' => 'Organic farming',
                'vertical' => 'Vertical / indoor farming',
                'trading' => 'Agricultural trading',
                'storage' => 'Cold storage & warehousing',
                'equipment' => 'Farm equipment',
                'fertilizer' => 'Fertilizer & seeds',
                'pest_control' => 'Agricultural pest control',
                'consulting' => 'Agricultural consulting',
                'cooperative' => 'Farming cooperative',
                'forestry' => 'Forestry & timber',
            ),
            'erp_base' => 'agriculture',
            'costing_default' => 'weighted_avg',
            'hero_style' => 'natural_green',
            'color_scheme' => array('primary' => '#16a34a', 'accent' => '#4ade80', 'bg_from' => '#14532d', 'bg_to' => '#166534'),
        ),

        'logistics_transport' => array(
            'label' => 'Logistics & Transport',
            'icon' => 'fa-truck',
            'template_key' => 'logistics',
            'description' => 'Freight, shipping, courier, warehousing, and supply chain.',
            'default_sub_areas' => array('freight', 'warehouse', 'tracking'),
            'available_sub_areas' => array(
                'freight' => 'Freight forwarding',
                'warehouse' => 'Warehousing & 3PL',
                'tracking' => 'Shipment tracking',
                'courier' => 'Courier & parcel',
                'trucking' => 'Trucking & road freight',
                'shipping' => 'Sea freight & container',
                'air_cargo' => 'Air cargo & express',
                'customs' => 'Customs brokerage',
                'cold_chain' => 'Cold chain logistics',
                'last_mile' => 'Last-mile delivery',
                'moving' => 'Moving & relocation',
                'fleet' => 'Fleet management',
                'rail' => 'Rail freight',
                'pipeline' => 'Pipeline transport',
            ),
            'erp_base' => 'logistics_freight',
            'costing_default' => 'specific',
            'hero_style' => 'dynamic_motion',
            'color_scheme' => array('primary' => '#0369a1', 'accent' => '#0ea5e9', 'bg_from' => '#0c4a6e', 'bg_to' => '#075985'),
        ),

        'energy_utilities' => array(
            'label' => 'Energy & Utilities',
            'icon' => 'fa-bolt',
            'template_key' => 'energy',
            'description' => 'Oil & gas, solar, wind, water, electricity, and utility services.',
            'default_sub_areas' => array('generation', 'distribution', 'billing'),
            'available_sub_areas' => array(
                'generation' => 'Power generation',
                'distribution' => 'Energy distribution',
                'billing' => 'Utility billing',
                'solar' => 'Solar energy',
                'wind' => 'Wind energy',
                'oil_gas' => 'Oil & gas operations',
                'water' => 'Water treatment & utility',
                'ev_charging' => 'EV charging infrastructure',
                'battery' => 'Battery & energy storage',
                'nuclear' => 'Nuclear energy',
                'biomass' => 'Biomass & bioenergy',
                'mining' => 'Mining & extraction',
                'waste_energy' => 'Waste-to-energy',
            ),
            'erp_base' => 'oil_gas',
            'costing_default' => 'weighted_avg',
            'hero_style' => 'industrial_bold',
            'color_scheme' => array('primary' => '#ca8a04', 'accent' => '#facc15', 'bg_from' => '#422006', 'bg_to' => '#713f12'),
        ),

        'financial_services' => array(
            'label' => 'Financial Services & Insurance',
            'icon' => 'fa-university',
            'template_key' => 'finance',
            'description' => 'Banking, insurance, fintech, wealth management, and financial advisory.',
            'default_sub_areas' => array('advisory', 'compliance', 'reporting'),
            'available_sub_areas' => array(
                'advisory' => 'Financial advisory',
                'compliance' => 'Regulatory compliance',
                'reporting' => 'Financial reporting',
                'insurance' => 'Insurance brokerage',
                'banking' => 'Banking services',
                'lending' => 'Lending & microfinance',
                'wealth' => 'Wealth management',
                'fintech' => 'Fintech solutions',
                'forex' => 'Foreign exchange',
                'investment' => 'Investment management',
                'payment' => 'Payment processing',
                'crypto' => 'Cryptocurrency exchange',
                'collection' => 'Debt collection',
            ),
            'erp_base' => 'services_professional',
            'costing_default' => 'standard',
            'hero_style' => 'corporate_clean',
            'color_scheme' => array('primary' => '#1d4ed8', 'accent' => '#3b82f6', 'bg_from' => '#1e3a5f', 'bg_to' => '#1e40af'),
        ),

        'it_software' => array(
            'label' => 'IT & Software',
            'icon' => 'fa-code',
            'template_key' => 'it_software',
            'description' => 'Software companies, SaaS, web development, cybersecurity, and IT services.',
            'default_sub_areas' => array('development', 'saas', 'support'),
            'available_sub_areas' => array(
                'development' => 'Software development',
                'saas' => 'SaaS / cloud platform',
                'support' => 'IT support & managed services',
                'web_design' => 'Web design & development',
                'mobile_apps' => 'Mobile app development',
                'cybersecurity' => 'Cybersecurity',
                'data_analytics' => 'Data analytics & BI',
                'ai_ml' => 'AI & machine learning',
                'iot' => 'IoT solutions',
                'erp_systems' => 'ERP implementation',
                'devops' => 'DevOps & cloud',
                'gaming' => 'Game development',
                'blockchain' => 'Blockchain & Web3',
            ),
            'erp_base' => 'services_professional',
            'costing_default' => 'standard',
            'hero_style' => 'tech_dark',
            'color_scheme' => array('primary' => '#7c3aed', 'accent' => '#a78bfa', 'bg_from' => '#1e1b4b', 'bg_to' => '#312e81'),
        ),

        'media_entertainment' => array(
            'label' => 'Media & Entertainment',
            'icon' => 'fa-film',
            'template_key' => 'media',
            'description' => 'Film, TV, music, publishing, gaming, events, and creative arts.',
            'default_sub_areas' => array('production', 'distribution', 'events'),
            'available_sub_areas' => array(
                'production' => 'Film & video production',
                'distribution' => 'Content distribution',
                'events' => 'Events & shows',
                'music' => 'Music production & label',
                'publishing' => 'Publishing & media',
                'photography' => 'Photography studio',
                'animation' => 'Animation & VFX',
                'gaming' => 'Gaming & esports',
                'streaming' => 'Streaming platform',
                'radio_tv' => 'Radio & TV broadcasting',
                'advertising' => 'Advertising production',
                'theater' => 'Theater & performing arts',
                'podcast' => 'Podcasting',
            ),
            'erp_base' => 'services_professional',
            'costing_default' => 'specific',
            'hero_style' => 'creative_bold',
            'color_scheme' => array('primary' => '#dc2626', 'accent' => '#f87171', 'bg_from' => '#1c1917', 'bg_to' => '#44403c'),
        ),

        'sports_fitness' => array(
            'label' => 'Sports & Fitness',
            'icon' => 'fa-futbol-o',
            'template_key' => 'sports',
            'description' => 'Gyms, sports facilities, fitness studios, sports retail, and athletics.',
            'default_sub_areas' => array('gym', 'membership', 'retail'),
            'available_sub_areas' => array(
                'gym' => 'Gym & fitness center',
                'membership' => 'Membership management',
                'retail' => 'Sports equipment retail',
                'yoga' => 'Yoga & pilates studio',
                'martial_arts' => 'Martial arts academy',
                'swimming' => 'Swimming pool / aquatics',
                'sports_academy' => 'Sports academy & coaching',
                'outdoor' => 'Outdoor & adventure sports',
                'golf' => 'Golf club & course',
                'tennis' => 'Tennis & racquet sports',
                'cycling' => 'Cycling club & shop',
                'esports' => 'Esports arena',
            ),
            'erp_base' => 'services_professional',
            'costing_default' => 'standard',
            'hero_style' => 'dynamic_motion',
            'color_scheme' => array('primary' => '#059669', 'accent' => '#10b981', 'bg_from' => '#064e3b', 'bg_to' => '#047857'),
        ),

        'home_living' => array(
            'label' => 'Home & Living',
            'icon' => 'fa-home',
            'template_key' => 'home_living',
            'description' => 'Furniture, home decor, kitchen, garden, and household supplies.',
            'default_sub_areas' => array('furniture', 'decor', 'appliances'),
            'available_sub_areas' => array(
                'furniture' => 'Furniture retail',
                'decor' => 'Home decor & accessories',
                'appliances' => 'Home appliances',
                'kitchen' => 'Kitchen & bath',
                'garden' => 'Garden & outdoor',
                'lighting' => 'Lighting & fixtures',
                'flooring' => 'Flooring & tiles',
                'mattress' => 'Mattress & bedding',
                'curtains' => 'Curtains & blinds',
                'art' => 'Art & wall decor',
                'smart_home' => 'Smart home products',
                'cleaning' => 'Cleaning products',
                'storage' => 'Storage & organization',
            ),
            'erp_base' => 'general',
            'costing_default' => 'weighted_avg',
            'hero_style' => 'warm_inviting',
            'color_scheme' => array('primary' => '#92400e', 'accent' => '#d97706', 'bg_from' => '#451a03', 'bg_to' => '#78350f'),
        ),

        'wholesale_trading' => array(
            'label' => 'Wholesale & Trading',
            'icon' => 'fa-cubes',
            'template_key' => 'wholesale',
            'description' => 'Import/export, B2B distribution, wholesale markets, and commodity trading.',
            'default_sub_areas' => array('distribution', 'import_export', 'pricing'),
            'available_sub_areas' => array(
                'distribution' => 'Wholesale distribution',
                'import_export' => 'Import / export trading',
                'pricing' => 'Tiered pricing & schemes',
                'commodity' => 'Commodity trading',
                'fmcg' => 'FMCG distribution',
                'b2b_marketplace' => 'B2B marketplace',
                'dropship' => 'Dropshipping',
                'cash_carry' => 'Cash & carry',
                'liquidation' => 'Liquidation & clearance',
                'chemicals' => 'Chemical trading',
                'metals' => 'Metals trading',
                'textiles' => 'Textile trading',
            ),
            'erp_base' => 'wholesale_distribution',
            'costing_default' => 'weighted_avg',
            'hero_style' => 'industrial_bold',
            'color_scheme' => array('primary' => '#0369a1', 'accent' => '#38bdf8', 'bg_from' => '#082f49', 'bg_to' => '#0c4a6e'),
        ),

        'rental_leasing' => array(
            'label' => 'Rental & Leasing',
            'icon' => 'fa-key',
            'template_key' => 'rental',
            'description' => 'Equipment rental, car hire, property leasing, and subscription services.',
            'default_sub_areas' => array('booking', 'fleet', 'contracts'),
            'available_sub_areas' => array(
                'booking' => 'Online booking system',
                'fleet' => 'Fleet / asset tracking',
                'contracts' => 'Lease contract management',
                'car_rental' => 'Car & vehicle rental',
                'equipment' => 'Equipment & tool rental',
                'property' => 'Property leasing',
                'party_event' => 'Party & event rentals',
                'heavy_machinery' => 'Heavy machinery hire',
                'office_space' => 'Co-working & office rental',
                'storage' => 'Self-storage rental',
                'boat_yacht' => 'Boat & yacht charter',
                'costume' => 'Costume & formal wear hire',
            ),
            'erp_base' => 'rental_leasing',
            'costing_default' => 'standard',
            'hero_style' => 'clean_modern',
            'color_scheme' => array('primary' => '#ca8a04', 'accent' => '#facc15', 'bg_from' => '#422006', 'bg_to' => '#854d0e'),
        ),

        'nonprofit_government' => array(
            'label' => 'Non-Profit & Government',
            'icon' => 'fa-institution',
            'template_key' => 'nonprofit',
            'description' => 'NGOs, charities, government services, public sector, and social enterprises.',
            'default_sub_areas' => array('donations', 'programs', 'reporting'),
            'available_sub_areas' => array(
                'donations' => 'Donation & fundraising',
                'programs' => 'Program management',
                'reporting' => 'Grant reporting',
                'charity' => 'Charitable foundation',
                'ngo' => 'NGO operations',
                'government' => 'Government services',
                'public_health' => 'Public health',
                'social_enterprise' => 'Social enterprise',
                'religious' => 'Religious organization',
                'community' => 'Community center',
                'library' => 'Public library',
                'museum' => 'Museum & gallery',
            ),
            'erp_base' => 'services_professional',
            'costing_default' => 'standard',
            'hero_style' => 'clean_modern',
            'color_scheme' => array('primary' => '#0369a1', 'accent' => '#0ea5e9', 'bg_from' => '#0c4a6e', 'bg_to' => '#075985'),
        ),

        'cleaning_maintenance' => array(
            'label' => 'Cleaning & Maintenance',
            'icon' => 'fa-recycle',
            'template_key' => 'cleaning',
            'description' => 'Commercial cleaning, pest control, waste management, and facility services.',
            'default_sub_areas' => array('commercial', 'residential', 'scheduling'),
            'available_sub_areas' => array(
                'commercial' => 'Commercial cleaning',
                'residential' => 'Residential cleaning',
                'scheduling' => 'Job scheduling',
                'pest_control' => 'Pest control',
                'waste' => 'Waste management & recycling',
                'window' => 'Window cleaning',
                'carpet' => 'Carpet & upholstery',
                'industrial' => 'Industrial cleaning',
                'disinfection' => 'Sanitization & disinfection',
                'landscape' => 'Landscape maintenance',
                'pool' => 'Pool maintenance',
                'hvac' => 'HVAC maintenance',
            ),
            'erp_base' => 'services_professional',
            'costing_default' => 'standard',
            'hero_style' => 'clean_modern',
            'color_scheme' => array('primary' => '#059669', 'accent' => '#34d399', 'bg_from' => '#064e3b', 'bg_to' => '#065f46'),
        ),

        'pet_animal' => array(
            'label' => 'Pet & Animal Services',
            'icon' => 'fa-paw',
            'template_key' => 'pet',
            'description' => 'Pet shops, grooming, veterinary, boarding, and animal supplies.',
            'default_sub_areas' => array('retail', 'grooming', 'boarding'),
            'available_sub_areas' => array(
                'retail' => 'Pet supply store',
                'grooming' => 'Pet grooming',
                'boarding' => 'Pet boarding & daycare',
                'veterinary' => 'Veterinary clinic',
                'training' => 'Pet training',
                'breeding' => 'Pet breeding',
                'aquarium' => 'Aquarium & fish',
                'equestrian' => 'Equestrian center',
                'shelter' => 'Animal shelter',
                'pet_food' => 'Pet food manufacturing',
            ),
            'erp_base' => 'general',
            'costing_default' => 'weighted_avg',
            'hero_style' => 'warm_inviting',
            'color_scheme' => array('primary' => '#d97706', 'accent' => '#fbbf24', 'bg_from' => '#451a03', 'bg_to' => '#78350f'),
        ),

        'printing_signage' => array(
            'label' => 'Printing & Signage',
            'icon' => 'fa-print',
            'template_key' => 'printing',
            'description' => 'Commercial printing, signage, packaging design, and promotional materials.',
            'default_sub_areas' => array('commercial', 'large_format', 'packaging'),
            'available_sub_areas' => array(
                'commercial' => 'Commercial printing',
                'large_format' => 'Large format & signage',
                'packaging' => 'Packaging & labels',
                'digital' => 'Digital printing',
                'screen' => 'Screen printing & embroidery',
                'banner' => 'Banner & display',
                'promotional' => 'Promotional products',
                'bookbinding' => 'Bookbinding & finishing',
                'offset' => 'Offset printing',
                'gift' => 'Gift & personalized items',
            ),
            'erp_base' => 'printing_packaging',
            'costing_default' => 'specific',
            'hero_style' => 'creative_bold',
            'color_scheme' => array('primary' => '#7c3aed', 'accent' => '#a78bfa', 'bg_from' => '#2e1065', 'bg_to' => '#4c1d95'),
        ),

        'security_safety' => array(
            'label' => 'Security & Safety',
            'icon' => 'fa-shield',
            'template_key' => 'security',
            'description' => 'Security services, fire safety, access control, CCTV, and surveillance.',
            'default_sub_areas' => array('guarding', 'systems', 'monitoring'),
            'available_sub_areas' => array(
                'guarding' => 'Security guarding',
                'systems' => 'Security systems & CCTV',
                'monitoring' => 'Remote monitoring',
                'fire' => 'Fire safety & prevention',
                'access_control' => 'Access control',
                'cyber' => 'Cybersecurity services',
                'investigation' => 'Private investigation',
                'alarm' => 'Alarm systems',
                'safes' => 'Safes & vaults',
                'training' => 'Security training',
            ),
            'erp_base' => 'services_professional',
            'costing_default' => 'standard',
            'hero_style' => 'industrial_bold',
            'color_scheme' => array('primary' => '#1e3a5f', 'accent' => '#3b82f6', 'bg_from' => '#0f172a', 'bg_to' => '#1e3a5f'),
        ),
    );
}

/**
 * Map an individual industry name (from the 1154 list) to its super group key.
 *
 * Uses keyword matching against industry names. If no match found, falls back
 * to the badge category (Specialized, Services, Commerce, etc.) which maps to
 * a default group.
 *
 * @param string $industryName The industry display name
 * @param string $badgeCategory The badge category (Commerce, Services, etc.)
 * @return string The super group key
 */
function epc_industry_resolve_group(string $industryName, string $badgeCategory = ''): string
{
    $name = strtolower(trim($industryName));
    $groups = epc_industry_groups();

    // Keyword-based matching rules (order matters — first match wins)
    $rules = array(
        'automotive' => array('auto ', 'car ', 'vehicle', 'motor', 'tyre', 'tire ', 'driving school', 'garage', 'automotive', 'fleet', 'tarpaulin'),
        'healthcare_medical' => array('medical', 'health', 'hospital', 'clinic', 'pharma', 'dental', 'doctor', 'nurse', 'veterinar', 'optical', 'ambulance', 'physiotherapy', 'mental health', 'fertility', 'addiction', 'urology', 'oncology', 'cardio', 'ortho', 'derma', 'pediatr', 'chiropr', 'acupuncture', 'ayurveda', 'homeopath'),
        'food_beverage' => array('restaurant', 'food', 'bakery', 'cafe', 'coffee', 'catering', 'kitchen', 'butcher', 'confection', 'ice cream', 'juice', 'beverage', 'brewery', 'winery', 'bar &', 'pub', 'bbq', 'smokehouse', 'pizza', 'sushi'),
        'fashion_apparel' => array('fashion', 'apparel', 'clothing', 'garment', 'textile', 'footwear', 'shoe', 'tailoring', 'bridal', 'swimwear', 'sportswear', 'uniform', 'wig', 'gown', 'handbag'),
        'jewellery_luxury' => array('jeweller', 'jewelry', 'diamond', 'gold ', 'watch ', 'watches', 'gemstone', 'luxury goods', 'precious'),
        'electronics_technology' => array('electronic', 'computer', 'laptop', 'mobile phone', 'gadget', 'gaming', 'audio', 'tv ', 'television', 'appliance repair', 'typewriter'),
        'construction_realestate' => array('construction', 'real estate', 'property', 'architect', 'interior design', 'plumb', 'electrical contract', 'painting', 'landscap', 'demolition', 'survey', 'steel fabricat', 'concrete', 'waterproof', 'insulation', 'flooring', 'roofing', 'tunnel', 'pipeline', 'road ', 'bridge'),
        'manufacturing_industrial' => array('manufactur', 'factory', 'production', 'assembly', 'chemical', 'plastic', 'rubber', 'metal', 'steel ', 'glass ', 'ceramic', 'pottery', 'paper', 'pulp', '3d print', 'woodwork', 'carpentry', 'foundry', 'extrusion', 'welding', 'fabricat', 'adhesive', 'varnish', 'aluminum', 'wire ', 'valve'),
        'professional_services' => array('consulting', 'consultancy', 'law firm', 'legal', 'accounting firm', 'audit firm', 'tax ', 'bookkeep', 'hr ', 'recruitment', 'staffing', 'marketing agency', 'advertising agency', 'pr agency', 'translation', 'research', 'advisor', 'notary', 'trademark', 'patent'),
        'education_training' => array('school', 'university', 'college', 'education', 'training', 'tutor', 'coaching', 'learning', 'academy', 'certification', 'vocational'),
        'hospitality_travel' => array('hotel', 'resort', 'hostel', 'travel', 'tourism', 'tour operator', 'airline', 'cruise', 'camping', 'glamping', 'bed & breakfast', 'vacation', 'airport'),
        'beauty_wellness' => array('beauty', 'salon', 'spa', 'barber', 'nail ', 'skincare', 'cosmetic', 'perfume', 'tattoo', 'grooming', 'wellness clinic', 'wellness retreat'),
        'retail_ecommerce' => array('retail', 'supermarket', 'grocery', 'convenience store', 'department store', 'marketplace', 'gift shop', 'toy store', 'bookstore', 'stationery', 'pet shop', 'home decor', 'antique', 'thrift', 'vending'),
        'agriculture_farming' => array('agricultur', 'farm', 'crop', 'livestock', 'poultry', 'aquaculture', 'fishery', 'dairy', 'organic farm', 'vertical farm', 'seeds', 'fertilizer', 'beehive', 'honey', 'wheat', 'vegetable farm', 'growing of'),
        'logistics_transport' => array('logistic', 'freight', 'shipping', 'courier', 'warehouse', '3pl', 'cargo', 'trucking', 'transport', 'moving', 'relocation', 'delivery', 'customs broker', 'cold chain'),
        'energy_utilities' => array('energy', 'solar', 'wind ', 'oil &', 'gas ', 'power ', 'utility', 'electric', 'nuclear', 'biomass', 'mining', 'uranium', 'battery '),
        'financial_services' => array('bank', 'insurance', 'fintech', 'wealth', 'investment', 'venture capital', 'lending', 'forex', 'payment', 'crypto', 'asset management', 'collection agency'),
        'it_software' => array('software', 'saas', 'web design', 'web develop', 'app develop', 'cybersecurity', 'data analy', 'ai &', 'machine learn', 'iot', 'blockchain', 'devops', 'cloud ', 'voip', 'it '),
        'media_entertainment' => array('film', 'video production', 'music', 'publishing', 'photography', 'animation', 'streaming', 'broadcast', 'radio', 'podcast', 'youtube', 'theater', 'event', 'amusement', 'theme park', 'entertainment'),
        'sports_fitness' => array('gym', 'fitness', 'sport', 'yoga', 'martial', 'swimming', 'golf', 'tennis', 'cycling', 'bicycle', 'trampoline', 'esport'),
        'home_living' => array('furniture', 'home & living', 'mattress', 'bedding', 'curtain', 'lighting', 'kitchen &', 'bath', 'garden', 'storage', 'art supply'),
        'wholesale_trading' => array('wholesale', 'trading', 'import', 'export', 'distribution', 'commodity', 'fmcg', 'b2b', 'liquidat'),
        'rental_leasing' => array('rental', 'leasing', 'hire', 'charter', 'co-working', 'self-storage'),
        'nonprofit_government' => array('non-profit', 'nonprofit', 'ngo', 'charity', 'government', 'public sector', 'social enterprise', 'religious', 'church', 'mosque', 'temple', 'community center', 'library', 'museum', 'volunteer', 'advocacy', 'voter'),
        'cleaning_maintenance' => array('cleaning', 'pest control', 'waste', 'recycling', 'janitorial', 'laundry', 'dry clean', 'carpet clean', 'window clean', 'pool maintenance'),
        'pet_animal' => array('pet ', 'animal', 'vet ', 'dog ', 'cat ', 'grooming', 'aquarium', 'equestrian', 'shelter'),
        'printing_signage' => array('printing', 'signage', 'sign ', 'banner', 'label', 'embroidery', 'engraving'),
        'security_safety' => array('security', 'cctv', 'surveillance', 'fire safety', 'alarm', 'guard', 'access control', 'investigation'),
    );

    foreach ($rules as $groupKey => $keywords) {
        foreach ($keywords as $keyword) {
            if (strpos($name, $keyword) !== false) {
                return $groupKey;
            }
        }
    }

    // Fallback by badge category
    $categoryMap = array(
        'commerce' => 'retail_ecommerce',
        'services' => 'professional_services',
        'industrial' => 'manufacturing_industrial',
        'supply' => 'wholesale_trading',
        'healthcare' => 'healthcare_medical',
        'technology' => 'it_software',
        'hospitality' => 'hospitality_travel',
        'specialized' => 'professional_services',
    );

    $cat = strtolower(trim($badgeCategory));
    return $categoryMap[$cat] ?? 'retail_ecommerce';
}

/**
 * Get the consolidated group info for a given industry code or name.
 *
 * @param string $industryIdentifier Industry code or display name
 * @param string $badgeCategory Optional badge category for fallback
 * @return array Group definition from epc_industry_groups()
 */
function epc_industry_get_group(string $industryIdentifier, string $badgeCategory = ''): array
{
    $groupKey = epc_industry_resolve_group($industryIdentifier, $badgeCategory);
    $groups = epc_industry_groups();
    return $groups[$groupKey] ?? $groups['retail_ecommerce'];
}

/**
 * Get the frontend template key for a given industry.
 *
 * @param string $industryIdentifier Industry code or display name
 * @param string $badgeCategory Optional badge category
 * @return string Template key (e.g., 'automotive', 'healthcare', 'fashion')
 */
function epc_industry_template_key(string $industryIdentifier, string $badgeCategory = ''): string
{
    $group = epc_industry_get_group($industryIdentifier, $badgeCategory);
    return $group['template_key'] ?? 'retail';
}

/**
 * Load the active sub-areas for a tenant (from DB settings or defaults).
 *
 * @param PDO $pdo Database connection
 * @param string $siteKey Tenant site_key
 * @param string $industryCode Tenant industry code
 * @return array<string, bool> Map of sub_area_key => active (true/false)
 */
function epc_industry_tenant_sub_areas(PDO $pdo, string $siteKey, string $industryCode): array
{
    $group = epc_industry_get_group($industryCode);
    $allAreas = $group['available_sub_areas'] ?? array();
    $defaults = $group['default_sub_areas'] ?? array();

    // Try loading saved settings
    $saved = null;
    try {
        $stmt = $pdo->prepare("SELECT sub_areas_json FROM epc_tenant_industry_config WHERE site_key = ? LIMIT 1");
        $stmt->execute(array($siteKey));
        $row = $stmt->fetch(PDO::FETCH_ASSOC);
        if ($row && !empty($row['sub_areas_json'])) {
            $saved = json_decode($row['sub_areas_json'], true);
        }
    } catch (\Throwable $e) {
        // Table might not exist yet — use defaults
    }

    $result = array();
    foreach ($allAreas as $key => $label) {
        if ($saved !== null) {
            $result[$key] = !empty($saved[$key]);
        } else {
            $result[$key] = in_array($key, $defaults, true);
        }
    }

    return $result;
}

/**
 * Save tenant sub-area toggles.
 *
 * @param PDO $pdo Database connection
 * @param string $siteKey Tenant site_key
 * @param string $industryCode Industry code
 * @param string $groupKey Resolved group key
 * @param array<string, bool> $subAreas Sub-area toggles
 * @return bool Success
 */
function epc_industry_save_tenant_sub_areas(PDO $pdo, string $siteKey, string $industryCode, string $groupKey, array $subAreas): bool
{
    epc_industry_ensure_config_schema($pdo);

    $json = json_encode($subAreas, JSON_UNESCAPED_UNICODE);
    $stmt = $pdo->prepare("
        INSERT INTO epc_tenant_industry_config (site_key, industry_code, group_key, sub_areas_json, updated_at)
        VALUES (?, ?, ?, ?, NOW())
        ON DUPLICATE KEY UPDATE
            industry_code = VALUES(industry_code),
            group_key = VALUES(group_key),
            sub_areas_json = VALUES(sub_areas_json),
            updated_at = NOW()
    ");
    return $stmt->execute(array($siteKey, $industryCode, $groupKey, $json));
}

/**
 * Ensure the tenant industry config table exists.
 */
function epc_industry_ensure_config_schema(PDO $pdo): void
{
    static $done = false;
    if ($done) {
        return;
    }
    $pdo->exec("
        CREATE TABLE IF NOT EXISTS `epc_tenant_industry_config` (
            `id`              INT UNSIGNED AUTO_INCREMENT PRIMARY KEY,
            `site_key`        VARCHAR(64) NOT NULL,
            `industry_code`   VARCHAR(64) NOT NULL DEFAULT '',
            `group_key`       VARCHAR(48) NOT NULL DEFAULT '',
            `sub_areas_json`  JSON NOT NULL,
            `updated_at`      DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            UNIQUE KEY `uk_site` (`site_key`)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
    ");
    $done = true;
}

/**
 * Get consolidation stats: how many industries map to each group.
 *
 * @param array $allIndustries List of all industry names
 * @return array<string, array{count: int, industries: string[]}>
 */
function epc_industry_consolidation_stats(array $allIndustries): array
{
    $groups = epc_industry_groups();
    $stats = array();
    foreach ($groups as $key => $group) {
        $stats[$key] = array('label' => $group['label'], 'count' => 0, 'industries' => array());
    }

    foreach ($allIndustries as $ind) {
        $name = is_array($ind) ? ($ind['name'] ?? '') : (string) $ind;
        $badge = is_array($ind) ? ($ind['badge'] ?? '') : '';
        $groupKey = epc_industry_resolve_group($name, $badge);
        if (isset($stats[$groupKey])) {
            $stats[$groupKey]['count']++;
            $stats[$groupKey]['industries'][] = $name;
        }
    }

    // Sort by count descending
    uasort($stats, function ($a, $b) {
        return $b['count'] - $a['count'];
    });

    return $stats;
}
