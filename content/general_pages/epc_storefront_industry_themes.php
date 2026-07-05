<?php
/**
 * Industry-specific product themes for all 28 consolidated groups.
 *
 * Each industry group gets:
 * 1. Product categories specific to that industry
 * 2. Sample products with Unsplash images
 * 3. Hero configuration (tagline, color accent, icon)
 * 4. Recommended ERP toolkit (which modules to activate)
 *
 * WORLDWIDE RULE: Prices in USD base, converted per tenant currency.
 * Category/product names are in English (translation layer handles localization).
 *
 * Usage:
 *   require_once __DIR__ . '/epc_storefront_industry_themes.php';
 *   $theme = epc_industry_theme('healthcare_medical');
 *   $categories = $theme['categories'];
 *   $products = $theme['products'];
 *   $hero = $theme['hero'];
 *   $erp_kit = $theme['erp_kit'];
 */
defined('_ASTEXE_') or die('No access');

function epc_industry_theme(string $groupCode): array
{
    $themes = epc_industry_theme_registry();
    return isset($themes[$groupCode]) ? $themes[$groupCode] : epc_industry_theme_default($groupCode);
}

function epc_industry_theme_default(string $groupCode): array
{
    return array(
        'hero' => array(
            'tagline' => 'Professional solutions for your business',
            'accent' => '#3b82f6',
            'icon' => 'fa-briefcase',
            'bg_image' => 'https://images.unsplash.com/photo-1486406146926-c627a92ad1ab?w=1200',
        ),
        'categories' => array(),
        'products' => array(),
        'erp_kit' => array('finance', 'inventory', 'procurement', 'sales'),
    );
}

function epc_industry_theme_registry(): array
{
    return array(
        'healthcare_medical' => array(
            'hero' => array(
                'tagline' => 'Advanced medical equipment & healthcare solutions',
                'accent' => '#059669',
                'icon' => 'fa-heartbeat',
                'bg_image' => 'https://images.unsplash.com/photo-1631549916768-4119b2e5f926?w=1200',
            ),
            'categories' => array(
                array('alias' => 'hc-diagnostic', 'name' => 'Diagnostic Equipment', 'url' => 'diagnostic', 'level' => 1, 'order' => 10),
                array('alias' => 'hc-imaging', 'name' => 'Medical Imaging', 'url' => 'diagnostic/imaging', 'level' => 2, 'order' => 10, 'parent_alias' => 'hc-diagnostic'),
                array('alias' => 'hc-lab', 'name' => 'Laboratory Instruments', 'url' => 'diagnostic/lab', 'level' => 2, 'order' => 20, 'parent_alias' => 'hc-diagnostic'),
                array('alias' => 'hc-surgical', 'name' => 'Surgical Instruments', 'url' => 'surgical', 'level' => 1, 'order' => 20),
                array('alias' => 'hc-ortho', 'name' => 'Orthopedic Devices', 'url' => 'surgical/orthopedic', 'level' => 2, 'order' => 10, 'parent_alias' => 'hc-surgical'),
                array('alias' => 'hc-dental', 'name' => 'Dental Equipment', 'url' => 'dental', 'level' => 1, 'order' => 30),
                array('alias' => 'hc-pharma', 'name' => 'Pharmaceutical Supplies', 'url' => 'pharma', 'level' => 1, 'order' => 40),
                array('alias' => 'hc-ppe', 'name' => 'PPE & Safety', 'url' => 'ppe-safety', 'level' => 1, 'order' => 50),
                array('alias' => 'hc-patient', 'name' => 'Patient Care', 'url' => 'patient-care', 'level' => 1, 'order' => 60),
                array('alias' => 'hc-rehab', 'name' => 'Rehabilitation', 'url' => 'patient-care/rehab', 'level' => 2, 'order' => 10, 'parent_alias' => 'hc-patient'),
            ),
            'products' => array(
                array('alias' => 'hc-ultrasound-pro', 'name' => 'Portable Ultrasound Scanner Pro', 'price' => 12500, 'category_alias' => 'hc-imaging', 'image' => 'https://images.unsplash.com/photo-1551601651-2a8555f1a136?w=400', 'specs' => 'Frequency: 2-12MHz | Display: 15.6" | Weight: 4.2kg'),
                array('alias' => 'hc-patient-monitor', 'name' => 'Multi-Parameter Patient Monitor', 'price' => 3800, 'category_alias' => 'hc-diagnostic', 'image' => 'https://images.unsplash.com/photo-1530026405186-ed1f139313f8?w=400', 'specs' => 'ECG, SpO2, NIBP, Temp | 12.1" touchscreen'),
                array('alias' => 'hc-centrifuge', 'name' => 'Clinical Centrifuge 5000 RPM', 'price' => 2200, 'category_alias' => 'hc-lab', 'image' => 'https://images.unsplash.com/photo-1579154204601-01588f351e67?w=400', 'specs' => 'Speed: 300-5000RPM | Capacity: 12x15ml'),
                array('alias' => 'hc-surgical-kit', 'name' => 'General Surgery Instrument Set', 'price' => 4500, 'category_alias' => 'hc-surgical', 'image' => 'https://images.unsplash.com/photo-1551190822-a9ce113ac100?w=400', 'specs' => '94 instruments | Stainless steel | Autoclave safe'),
                array('alias' => 'hc-dental-chair', 'name' => 'Dental Treatment Unit Complete', 'price' => 8900, 'category_alias' => 'hc-dental', 'image' => 'https://images.unsplash.com/photo-1588776814546-1ffcf47267a5?w=400', 'specs' => 'LED light | Electric chair | Suction unit'),
                array('alias' => 'hc-wheelchair', 'name' => 'Electric Wheelchair Lightweight', 'price' => 1800, 'category_alias' => 'hc-rehab', 'image' => 'https://images.unsplash.com/photo-1576091160550-2173dba999ef?w=400', 'specs' => 'Battery: 20km range | Weight: 23kg | Foldable'),
            ),
            'erp_kit' => array('finance', 'inventory', 'procurement', 'sales', 'compliance', 'quality_control', 'batch_tracking'),
        ),
        'automotive' => array(
            'hero' => array(
                'tagline' => 'Quality auto parts & accessories for every vehicle',
                'accent' => '#dc2626',
                'icon' => 'fa-car',
                'bg_image' => 'https://images.unsplash.com/photo-1492144534655-ae79c964c9d7?w=1200',
            ),
            'categories' => array(
                array('alias' => 'auto-engine', 'name' => 'Engine & Drivetrain', 'url' => 'engine', 'level' => 1, 'order' => 10),
                array('alias' => 'auto-filters', 'name' => 'Filters & Fluids', 'url' => 'engine/filters', 'level' => 2, 'order' => 10, 'parent_alias' => 'auto-engine'),
                array('alias' => 'auto-belts', 'name' => 'Belts & Timing', 'url' => 'engine/belts', 'level' => 2, 'order' => 20, 'parent_alias' => 'auto-engine'),
                array('alias' => 'auto-brakes', 'name' => 'Brakes & Suspension', 'url' => 'brakes', 'level' => 1, 'order' => 20),
                array('alias' => 'auto-pads', 'name' => 'Brake Pads & Discs', 'url' => 'brakes/pads', 'level' => 2, 'order' => 10, 'parent_alias' => 'auto-brakes'),
                array('alias' => 'auto-body', 'name' => 'Body & Exterior', 'url' => 'body', 'level' => 1, 'order' => 30),
                array('alias' => 'auto-lighting', 'name' => 'Lighting', 'url' => 'body/lighting', 'level' => 2, 'order' => 10, 'parent_alias' => 'auto-body'),
                array('alias' => 'auto-tires', 'name' => 'Tires & Wheels', 'url' => 'tires', 'level' => 1, 'order' => 40),
                array('alias' => 'auto-electrical', 'name' => 'Electrical & Battery', 'url' => 'electrical', 'level' => 1, 'order' => 50),
                array('alias' => 'auto-accessories', 'name' => 'Accessories & Interior', 'url' => 'accessories', 'level' => 1, 'order' => 60),
            ),
            'products' => array(
                array('alias' => 'auto-brake-kit', 'name' => 'Brembo Front Brake Kit', 'price' => 450, 'category_alias' => 'auto-pads', 'image' => 'https://images.unsplash.com/photo-1558618666-fcd25c85f82e?w=400', 'specs' => 'Ceramic pads | Ventilated discs | Toyota/Nissan fit'),
                array('alias' => 'auto-led-bar', 'name' => 'LED Light Bar 42" Curved', 'price' => 280, 'category_alias' => 'auto-lighting', 'image' => 'https://images.unsplash.com/photo-1544636331-e26879cd4d9b?w=400', 'specs' => '240W | 6000K | IP68 waterproof | Off-road'),
                array('alias' => 'auto-tire-mich', 'name' => 'Michelin Pilot Sport 4S 245/40R18', 'price' => 320, 'category_alias' => 'auto-tires', 'image' => 'https://images.unsplash.com/photo-1580273916550-e323be2ae537?w=400', 'specs' => 'Summer tire | Y-rated | EU label: A/A/70dB'),
                array('alias' => 'auto-battery', 'name' => 'AGM Battery 80Ah 800A', 'price' => 220, 'category_alias' => 'auto-electrical', 'image' => 'https://images.unsplash.com/photo-1620714223084-8fcacc6dfd8d?w=400', 'specs' => 'Start-stop compatible | 4-year warranty'),
                array('alias' => 'auto-oil-filter', 'name' => 'Full Synthetic Oil Change Kit', 'price' => 85, 'category_alias' => 'auto-filters', 'image' => 'https://images.unsplash.com/photo-1487754180451-c456f719a1fc?w=400', 'specs' => '5W-30 fully synthetic 5L + OE filter + drain plug'),
                array('alias' => 'auto-dashcam', 'name' => 'Dual Dash Camera 4K', 'price' => 190, 'category_alias' => 'auto-accessories', 'image' => 'https://images.unsplash.com/photo-1502877338535-766e1452684a?w=400', 'specs' => 'Front+rear | GPS | Night vision | 128GB'),
            ),
            'erp_kit' => array('finance', 'inventory', 'procurement', 'sales', 'warranty', 'vehicle_compat', 'barcode'),
        ),
        'food_beverage' => array(
            'hero' => array(
                'tagline' => 'Fresh ingredients & gourmet supplies for food professionals',
                'accent' => '#ea580c',
                'icon' => 'fa-cutlery',
                'bg_image' => 'https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=1200',
            ),
            'categories' => array(
                array('alias' => 'fb-fresh', 'name' => 'Fresh Produce', 'url' => 'fresh', 'level' => 1, 'order' => 10),
                array('alias' => 'fb-dairy', 'name' => 'Dairy & Cheese', 'url' => 'fresh/dairy', 'level' => 2, 'order' => 10, 'parent_alias' => 'fb-fresh'),
                array('alias' => 'fb-meat', 'name' => 'Meat & Poultry', 'url' => 'fresh/meat', 'level' => 2, 'order' => 20, 'parent_alias' => 'fb-fresh'),
                array('alias' => 'fb-bakery', 'name' => 'Bakery & Pastry', 'url' => 'bakery', 'level' => 1, 'order' => 20),
                array('alias' => 'fb-beverages', 'name' => 'Beverages', 'url' => 'beverages', 'level' => 1, 'order' => 30),
                array('alias' => 'fb-coffee', 'name' => 'Coffee & Tea', 'url' => 'beverages/coffee', 'level' => 2, 'order' => 10, 'parent_alias' => 'fb-beverages'),
                array('alias' => 'fb-equipment', 'name' => 'Kitchen Equipment', 'url' => 'equipment', 'level' => 1, 'order' => 40),
                array('alias' => 'fb-packaging', 'name' => 'Packaging & Disposables', 'url' => 'packaging', 'level' => 1, 'order' => 50),
                array('alias' => 'fb-spices', 'name' => 'Spices & Condiments', 'url' => 'spices', 'level' => 1, 'order' => 60),
                array('alias' => 'fb-frozen', 'name' => 'Frozen Foods', 'url' => 'frozen', 'level' => 1, 'order' => 70),
            ),
            'products' => array(
                array('alias' => 'fb-espresso-machine', 'name' => 'Commercial Espresso Machine 2-Group', 'price' => 4200, 'category_alias' => 'fb-equipment', 'image' => 'https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?w=400', 'specs' => '2 group heads | Steam wand | 11L boiler'),
                array('alias' => 'fb-olive-oil', 'name' => 'Extra Virgin Olive Oil 5L (Italian)', 'price' => 45, 'category_alias' => 'fb-spices', 'image' => 'https://images.unsplash.com/photo-1474979266404-7f28f05b8173?w=400', 'specs' => 'Cold pressed | Acidity <0.5% | DOP certified'),
                array('alias' => 'fb-chef-knife', 'name' => 'Japanese Chef Knife 210mm', 'price' => 180, 'category_alias' => 'fb-equipment', 'image' => 'https://images.unsplash.com/photo-1593618998160-e34014e67546?w=400', 'specs' => 'VG-10 Damascus steel | 67 layers | Octagonal handle'),
                array('alias' => 'fb-flour-25', 'name' => 'Bread Flour Type 550 (25kg)', 'price' => 22, 'category_alias' => 'fb-bakery', 'image' => 'https://images.unsplash.com/photo-1586444248902-2f64eddc13df?w=400', 'specs' => 'Protein 12.5% | French milling | Bread & pizza'),
                array('alias' => 'fb-coffee-beans', 'name' => 'Single Origin Ethiopia Yirgacheffe 1kg', 'price' => 38, 'category_alias' => 'fb-coffee', 'image' => 'https://images.unsplash.com/photo-1447933601403-0c6688de566e?w=400', 'specs' => 'Light roast | Altitude 1800m | Notes: blueberry, jasmine'),
                array('alias' => 'fb-packaging-box', 'name' => 'Eco Food Containers (250 pack)', 'price' => 55, 'category_alias' => 'fb-packaging', 'image' => 'https://images.unsplash.com/photo-1610832958506-aa56368176cf?w=400', 'specs' => 'Biodegradable | 750ml | Microwave safe'),
            ),
            'erp_kit' => array('finance', 'inventory', 'procurement', 'sales', 'batch_tracking', 'expiry_management', 'haccp_compliance'),
        ),
        'construction_realestate' => array(
            'hero' => array(
                'tagline' => 'Building materials & construction supplies',
                'accent' => '#d97706',
                'icon' => 'fa-building',
                'bg_image' => 'https://images.unsplash.com/photo-1504307651254-35680f356dfd?w=1200',
            ),
            'categories' => array(
                array('alias' => 'con-cement', 'name' => 'Cement & Concrete', 'url' => 'cement', 'level' => 1, 'order' => 10),
                array('alias' => 'con-steel', 'name' => 'Steel & Rebar', 'url' => 'steel', 'level' => 1, 'order' => 20),
                array('alias' => 'con-timber', 'name' => 'Timber & Formwork', 'url' => 'timber', 'level' => 1, 'order' => 30),
                array('alias' => 'con-electrical', 'name' => 'Electrical & Wiring', 'url' => 'electrical', 'level' => 1, 'order' => 40),
                array('alias' => 'con-plumbing', 'name' => 'Plumbing & HVAC', 'url' => 'plumbing', 'level' => 1, 'order' => 50),
                array('alias' => 'con-paint', 'name' => 'Paint & Finishes', 'url' => 'paint', 'level' => 1, 'order' => 60),
                array('alias' => 'con-tiles', 'name' => 'Tiles & Flooring', 'url' => 'tiles', 'level' => 1, 'order' => 70),
                array('alias' => 'con-safety', 'name' => 'Safety Equipment', 'url' => 'safety', 'level' => 1, 'order' => 80),
                array('alias' => 'con-tools', 'name' => 'Power Tools', 'url' => 'tools', 'level' => 1, 'order' => 90),
                array('alias' => 'con-hardware', 'name' => 'Hardware & Fasteners', 'url' => 'hardware', 'level' => 1, 'order' => 100),
            ),
            'products' => array(
                array('alias' => 'con-cement-opc', 'name' => 'OPC Cement 42.5N (50kg bag)', 'price' => 8, 'category_alias' => 'con-cement', 'image' => 'https://images.unsplash.com/photo-1518709268805-4e9042af9f23?w=400', 'specs' => 'Grade 42.5 | Setting time 2-4hrs | BS EN 197-1'),
                array('alias' => 'con-rebar-16', 'name' => 'Rebar Y16 (Grade 500B) per ton', 'price' => 650, 'category_alias' => 'con-steel', 'image' => 'https://images.unsplash.com/photo-1558618666-fcd25c85f82e?w=400', 'specs' => '16mm diameter | 12m length | BS 4449 Grade 500B'),
                array('alias' => 'con-drill-hilti', 'name' => 'Rotary Hammer Drill SDS-Plus', 'price' => 480, 'category_alias' => 'con-tools', 'image' => 'https://images.unsplash.com/photo-1504148455328-c376907d081c?w=400', 'specs' => '800W | 3.2J impact | Variable speed | Anti-vibration'),
                array('alias' => 'con-paint-ext', 'name' => 'Exterior Weather Shield Paint 20L', 'price' => 120, 'category_alias' => 'con-paint', 'image' => 'https://images.unsplash.com/photo-1562259949-e8e7689d7828?w=400', 'specs' => 'UV resistant | 15-year warranty | Self-cleaning'),
                array('alias' => 'con-porcelain', 'name' => 'Porcelain Floor Tiles 60x60cm (m2)', 'price' => 25, 'category_alias' => 'con-tiles', 'image' => 'https://images.unsplash.com/photo-1615971677499-5467cbab01c0?w=400', 'specs' => 'Polished finish | PEI 4 | Frost resistant | 10mm thick'),
                array('alias' => 'con-safety-helmet', 'name' => 'Safety Helmet with Visor (10 pack)', 'price' => 95, 'category_alias' => 'con-safety', 'image' => 'https://images.unsplash.com/photo-1504328345606-18bbc8c9d7d1?w=400', 'specs' => 'EN 397 | Ventilated | Adjustable | High-vis yellow'),
            ),
            'erp_kit' => array('finance', 'inventory', 'procurement', 'sales', 'project_costing', 'landed_cost', 'document_vault'),
        ),
        'hospitality_travel' => array(
            'hero' => array(
                'tagline' => 'Hospitality supplies & hotel amenities',
                'accent' => '#7c3aed',
                'icon' => 'fa-hotel',
                'bg_image' => 'https://images.unsplash.com/photo-1566073771259-6a8506099945?w=1200',
            ),
            'categories' => array(
                array('alias' => 'hos-linen', 'name' => 'Bed & Bath Linen', 'url' => 'linen', 'level' => 1, 'order' => 10),
                array('alias' => 'hos-amenities', 'name' => 'Guest Amenities', 'url' => 'amenities', 'level' => 1, 'order' => 20),
                array('alias' => 'hos-furniture', 'name' => 'Hotel Furniture', 'url' => 'furniture', 'level' => 1, 'order' => 30),
                array('alias' => 'hos-kitchen', 'name' => 'Commercial Kitchen', 'url' => 'kitchen', 'level' => 1, 'order' => 40),
                array('alias' => 'hos-tableware', 'name' => 'Tableware & Cutlery', 'url' => 'tableware', 'level' => 1, 'order' => 50),
                array('alias' => 'hos-cleaning', 'name' => 'Cleaning & Housekeeping', 'url' => 'cleaning', 'level' => 1, 'order' => 60),
                array('alias' => 'hos-pos', 'name' => 'POS & Technology', 'url' => 'technology', 'level' => 1, 'order' => 70),
                array('alias' => 'hos-outdoor', 'name' => 'Pool & Outdoor', 'url' => 'outdoor', 'level' => 1, 'order' => 80),
            ),
            'products' => array(
                array('alias' => 'hos-towel-set', 'name' => 'Premium Egyptian Cotton Towel Set', 'price' => 45, 'category_alias' => 'hos-linen', 'image' => 'https://images.unsplash.com/photo-1563453392212-326f5e854473?w=400', 'specs' => '700GSM | Bath + Hand + Face | White | Hotel grade'),
                array('alias' => 'hos-amenity-box', 'name' => 'Luxury Guest Welcome Box (50 sets)', 'price' => 350, 'category_alias' => 'hos-amenities', 'image' => 'https://images.unsplash.com/photo-1582719478250-c89cae4dc85b?w=400', 'specs' => 'Shampoo, conditioner, body wash, lotion | Recyclable'),
                array('alias' => 'hos-bed-king', 'name' => 'Hotel King Bed Frame & Mattress', 'price' => 1200, 'category_alias' => 'hos-furniture', 'image' => 'https://images.unsplash.com/photo-1505693416388-ac5ce068fe85?w=400', 'specs' => 'Pocket spring | Fire retardant | 180x200cm'),
                array('alias' => 'hos-pos-system', 'name' => 'Restaurant POS System Complete', 'price' => 2800, 'category_alias' => 'hos-pos', 'image' => 'https://images.unsplash.com/photo-1556742049-0cfed4f6a45d?w=400', 'specs' => 'Touchscreen | Kitchen printer | Card terminal | Cloud'),
                array('alias' => 'hos-pool-chair', 'name' => 'Outdoor Sun Lounger (Set of 4)', 'price' => 680, 'category_alias' => 'hos-outdoor', 'image' => 'https://images.unsplash.com/photo-1540541338287-41700207dee6?w=400', 'specs' => 'Aluminum frame | UV-resistant fabric | Adjustable'),
            ),
            'erp_kit' => array('finance', 'inventory', 'procurement', 'sales', 'reservations', 'housekeeping', 'pos_integration'),
        ),
        'beauty_wellness' => array(
            'hero' => array(
                'tagline' => 'Professional beauty & wellness products',
                'accent' => '#ec4899',
                'icon' => 'fa-magic',
                'bg_image' => 'https://images.unsplash.com/photo-1522335789203-aabd1fc54bc9?w=1200',
            ),
            'categories' => array(
                array('alias' => 'bw-skincare', 'name' => 'Skincare', 'url' => 'skincare', 'level' => 1, 'order' => 10),
                array('alias' => 'bw-makeup', 'name' => 'Makeup & Cosmetics', 'url' => 'makeup', 'level' => 1, 'order' => 20),
                array('alias' => 'bw-hair', 'name' => 'Hair Care & Styling', 'url' => 'hair', 'level' => 1, 'order' => 30),
                array('alias' => 'bw-perfume', 'name' => 'Perfumes & Fragrances', 'url' => 'perfumes', 'level' => 1, 'order' => 40),
                array('alias' => 'bw-salon', 'name' => 'Salon Equipment', 'url' => 'salon', 'level' => 1, 'order' => 50),
                array('alias' => 'bw-spa', 'name' => 'Spa & Wellness', 'url' => 'spa', 'level' => 1, 'order' => 60),
                array('alias' => 'bw-nails', 'name' => 'Nail Art & Manicure', 'url' => 'nails', 'level' => 1, 'order' => 70),
                array('alias' => 'bw-mens', 'name' => "Men's Grooming", 'url' => 'mens', 'level' => 1, 'order' => 80),
            ),
            'products' => array(
                array('alias' => 'bw-serum-vit-c', 'name' => 'Vitamin C Brightening Serum 30ml', 'price' => 65, 'category_alias' => 'bw-skincare', 'image' => 'https://images.unsplash.com/photo-1556228578-8c89e6adf883?w=400', 'specs' => '20% L-Ascorbic Acid | Hyaluronic acid | Ferulic acid'),
                array('alias' => 'bw-palette-pro', 'name' => 'Professional Eyeshadow Palette 35 Shades', 'price' => 48, 'category_alias' => 'bw-makeup', 'image' => 'https://images.unsplash.com/photo-1512496015851-a90fb38ba796?w=400', 'specs' => 'Matte + shimmer + metallic | Highly pigmented | Vegan'),
                array('alias' => 'bw-dryer-pro', 'name' => 'Professional Hair Dryer 2400W', 'price' => 180, 'category_alias' => 'bw-hair', 'image' => 'https://images.unsplash.com/photo-1522338140262-f46f5913618a?w=400', 'specs' => 'Ionic technology | 3 heat settings | Concentrator nozzle'),
                array('alias' => 'bw-perfume-oud', 'name' => 'Oud Royal Eau de Parfum 100ml', 'price' => 220, 'category_alias' => 'bw-perfume', 'image' => 'https://images.unsplash.com/photo-1541643600914-78b084683601?w=400', 'specs' => 'Top: saffron, bergamot | Heart: oud, rose | Base: musk, amber'),
                array('alias' => 'bw-chair-salon', 'name' => 'Hydraulic Salon Chair (Black)', 'price' => 450, 'category_alias' => 'bw-salon', 'image' => 'https://images.unsplash.com/photo-1521590832167-7bcbfaa6381f?w=400', 'specs' => '360-degree swivel | Adjustable height | PU leather'),
            ),
            'erp_kit' => array('finance', 'inventory', 'procurement', 'sales', 'expiry_management', 'batch_tracking', 'appointments'),
        ),
        'education_training' => array(
            'hero' => array(
                'tagline' => 'Educational resources & training materials',
                'accent' => '#2563eb',
                'icon' => 'fa-graduation-cap',
                'bg_image' => 'https://images.unsplash.com/photo-1523050854058-8df90110c9f1?w=1200',
            ),
            'categories' => array(
                array('alias' => 'edu-courses', 'name' => 'Online Courses', 'url' => 'courses', 'level' => 1, 'order' => 10),
                array('alias' => 'edu-books', 'name' => 'Textbooks & Materials', 'url' => 'books', 'level' => 1, 'order' => 20),
                array('alias' => 'edu-tech', 'name' => 'EdTech & Software', 'url' => 'technology', 'level' => 1, 'order' => 30),
                array('alias' => 'edu-lab', 'name' => 'Lab Equipment', 'url' => 'lab', 'level' => 1, 'order' => 40),
                array('alias' => 'edu-furniture', 'name' => 'Classroom Furniture', 'url' => 'furniture', 'level' => 1, 'order' => 50),
                array('alias' => 'edu-sports', 'name' => 'Sports & PE Equipment', 'url' => 'sports', 'level' => 1, 'order' => 60),
            ),
            'products' => array(
                array('alias' => 'edu-whiteboard', 'name' => 'Interactive Smart Board 75"', 'price' => 3500, 'category_alias' => 'edu-tech', 'image' => 'https://images.unsplash.com/photo-1509062522246-3755977927d7?w=400', 'specs' => '4K display | Multi-touch | Built-in Android | Wi-Fi'),
                array('alias' => 'edu-microscope', 'name' => 'Binocular Microscope 1000x', 'price' => 680, 'category_alias' => 'edu-lab', 'image' => 'https://images.unsplash.com/photo-1516979187457-637abb4f9353?w=400', 'specs' => '40x-1000x | LED illumination | Mechanical stage'),
                array('alias' => 'edu-desk-set', 'name' => 'Student Desk & Chair Set (10 units)', 'price' => 1200, 'category_alias' => 'edu-furniture', 'image' => 'https://images.unsplash.com/photo-1580582932707-520aed937b7b?w=400', 'specs' => 'Ergonomic | Stackable | Anti-scratch laminate'),
                array('alias' => 'edu-lms', 'name' => 'LMS Platform Annual License', 'price' => 2400, 'category_alias' => 'edu-courses', 'image' => 'https://images.unsplash.com/photo-1501504905252-473c47e087f8?w=400', 'specs' => 'Unlimited students | Video hosting | Analytics | Certificates'),
            ),
            'erp_kit' => array('finance', 'inventory', 'sales', 'student_management', 'scheduling', 'subscription_billing'),
        ),
        'energy_utilities' => array(
            'hero' => array(
                'tagline' => 'Energy solutions & industrial utilities equipment',
                'accent' => '#16a34a',
                'icon' => 'fa-bolt',
                'bg_image' => 'https://images.unsplash.com/photo-1509391366360-2e959784a276?w=1200',
            ),
            'categories' => array(
                array('alias' => 'eng-solar', 'name' => 'Solar & Renewable', 'url' => 'solar', 'level' => 1, 'order' => 10),
                array('alias' => 'eng-generators', 'name' => 'Generators & UPS', 'url' => 'generators', 'level' => 1, 'order' => 20),
                array('alias' => 'eng-cables', 'name' => 'Cables & Wiring', 'url' => 'cables', 'level' => 1, 'order' => 30),
                array('alias' => 'eng-transformers', 'name' => 'Transformers & Switchgear', 'url' => 'transformers', 'level' => 1, 'order' => 40),
                array('alias' => 'eng-meters', 'name' => 'Meters & Instruments', 'url' => 'meters', 'level' => 1, 'order' => 50),
                array('alias' => 'eng-hvac', 'name' => 'HVAC & Cooling', 'url' => 'hvac', 'level' => 1, 'order' => 60),
            ),
            'products' => array(
                array('alias' => 'eng-solar-panel', 'name' => 'Monocrystalline Solar Panel 550W', 'price' => 280, 'category_alias' => 'eng-solar', 'image' => 'https://images.unsplash.com/photo-1509391366360-2e959784a276?w=400', 'specs' => '550W | 21.3% efficiency | 25-year warranty | Tier-1'),
                array('alias' => 'eng-generator', 'name' => 'Diesel Generator 100kVA', 'price' => 12000, 'category_alias' => 'eng-generators', 'image' => 'https://images.unsplash.com/photo-1569012871812-f38ee64cd54c?w=400', 'specs' => '100kVA | Cummins engine | Auto transfer switch'),
                array('alias' => 'eng-inverter', 'name' => 'Hybrid Solar Inverter 10kW', 'price' => 2800, 'category_alias' => 'eng-solar', 'image' => 'https://images.unsplash.com/photo-1558449028-b53a39d100fc?w=400', 'specs' => '10kW | MPPT | Battery compatible | Wi-Fi monitoring'),
                array('alias' => 'eng-multimeter', 'name' => 'True RMS Digital Multimeter', 'price' => 95, 'category_alias' => 'eng-meters', 'image' => 'https://images.unsplash.com/photo-1581092160562-40aa08e78837?w=400', 'specs' => 'CAT III 1000V | Auto-range | Temperature probe | Backlit'),
            ),
            'erp_kit' => array('finance', 'inventory', 'procurement', 'sales', 'project_costing', 'maintenance', 'asset_tracking'),
        ),
        'manufacturing_industrial' => array(
            'hero' => array(
                'tagline' => 'Industrial machinery & manufacturing components',
                'accent' => '#475569',
                'icon' => 'fa-industry',
                'bg_image' => 'https://images.unsplash.com/photo-1565043666747-69f6646db940?w=1200',
            ),
            'categories' => array(
                array('alias' => 'mfg-machines', 'name' => 'CNC & Machine Tools', 'url' => 'machines', 'level' => 1, 'order' => 10),
                array('alias' => 'mfg-raw', 'name' => 'Raw Materials', 'url' => 'raw-materials', 'level' => 1, 'order' => 20),
                array('alias' => 'mfg-automation', 'name' => 'Automation & Robotics', 'url' => 'automation', 'level' => 1, 'order' => 30),
                array('alias' => 'mfg-safety', 'name' => 'Industrial Safety', 'url' => 'safety', 'level' => 1, 'order' => 40),
                array('alias' => 'mfg-tools', 'name' => 'Cutting Tools & Tooling', 'url' => 'tools', 'level' => 1, 'order' => 50),
                array('alias' => 'mfg-conveyor', 'name' => 'Conveyors & Material Handling', 'url' => 'material-handling', 'level' => 1, 'order' => 60),
                array('alias' => 'mfg-welding', 'name' => 'Welding & Fabrication', 'url' => 'welding', 'level' => 1, 'order' => 70),
                array('alias' => 'mfg-packaging', 'name' => 'Packaging Machinery', 'url' => 'packaging', 'level' => 1, 'order' => 80),
            ),
            'products' => array(
                array('alias' => 'mfg-cnc-lathe', 'name' => 'CNC Turning Lathe 250mm', 'price' => 45000, 'category_alias' => 'mfg-machines', 'image' => 'https://images.unsplash.com/photo-1565043666747-69f6646db940?w=400', 'specs' => 'Swing: 250mm | Spindle: 4000RPM | Fanuc control'),
                array('alias' => 'mfg-welding-mig', 'name' => 'MIG Welder 350A Industrial', 'price' => 3200, 'category_alias' => 'mfg-welding', 'image' => 'https://images.unsplash.com/photo-1504328345606-18bbc8c9d7d1?w=400', 'specs' => '350A | Wire feed | Pulse mode | Water-cooled torch'),
                array('alias' => 'mfg-robot-arm', 'name' => '6-Axis Industrial Robot Arm', 'price' => 28000, 'category_alias' => 'mfg-automation', 'image' => 'https://images.unsplash.com/photo-1485827404703-89b55fcc595e?w=400', 'specs' => 'Payload: 20kg | Reach: 1.8m | Repeatability: 0.02mm'),
                array('alias' => 'mfg-endmill', 'name' => 'Carbide End Mill Set (10pc)', 'price' => 180, 'category_alias' => 'mfg-tools', 'image' => 'https://images.unsplash.com/photo-1504148455328-c376907d081c?w=400', 'specs' => '2-12mm | TiAlN coated | 4 flutes | For steel/stainless'),
            ),
            'erp_kit' => array('finance', 'inventory', 'procurement', 'sales', 'production_planning', 'quality_control', 'maintenance', 'landed_cost'),
        ),
        'agriculture_farming' => array(
            'hero' => array(
                'tagline' => 'Agricultural supplies & farming equipment',
                'accent' => '#15803d',
                'icon' => 'fa-leaf',
                'bg_image' => 'https://images.unsplash.com/photo-1500937386664-56d1dfef3854?w=1200',
            ),
            'categories' => array(
                array('alias' => 'agr-seeds', 'name' => 'Seeds & Seedlings', 'url' => 'seeds', 'level' => 1, 'order' => 10),
                array('alias' => 'agr-fertilizer', 'name' => 'Fertilizers & Nutrients', 'url' => 'fertilizers', 'level' => 1, 'order' => 20),
                array('alias' => 'agr-machinery', 'name' => 'Farm Machinery', 'url' => 'machinery', 'level' => 1, 'order' => 30),
                array('alias' => 'agr-irrigation', 'name' => 'Irrigation Systems', 'url' => 'irrigation', 'level' => 1, 'order' => 40),
                array('alias' => 'agr-greenhouse', 'name' => 'Greenhouse & Hydroponics', 'url' => 'greenhouse', 'level' => 1, 'order' => 50),
                array('alias' => 'agr-livestock', 'name' => 'Livestock & Feed', 'url' => 'livestock', 'level' => 1, 'order' => 60),
            ),
            'products' => array(
                array('alias' => 'agr-drip-kit', 'name' => 'Drip Irrigation Kit (1 acre)', 'price' => 1200, 'category_alias' => 'agr-irrigation', 'image' => 'https://images.unsplash.com/photo-1500937386664-56d1dfef3854?w=400', 'specs' => 'Timer controller | 500 drippers | Filter | Pump ready'),
                array('alias' => 'agr-tractor', 'name' => 'Compact Tractor 45HP', 'price' => 22000, 'category_alias' => 'agr-machinery', 'image' => 'https://images.unsplash.com/photo-1580537659466-0a9bfa916a54?w=400', 'specs' => '45HP diesel | 4WD | PTO | 3-point hitch'),
                array('alias' => 'agr-npk', 'name' => 'NPK Compound Fertilizer 20-20-20 (50kg)', 'price' => 35, 'category_alias' => 'agr-fertilizer', 'image' => 'https://images.unsplash.com/photo-1416879595882-3373a0480b5b?w=400', 'specs' => 'Balanced nutrition | Granular | Slow-release | All crops'),
                array('alias' => 'agr-hydro-system', 'name' => 'NFT Hydroponic System (100 plants)', 'price' => 800, 'category_alias' => 'agr-greenhouse', 'image' => 'https://images.unsplash.com/photo-1530836369250-ef72a3f5cda8?w=400', 'specs' => 'NFT channels | pH controller | Nutrient tank | Timer'),
            ),
            'erp_kit' => array('finance', 'inventory', 'procurement', 'sales', 'crop_management', 'batch_tracking', 'weather_integration'),
        ),
    );
}

/**
 * Get ERP industry kit configuration.
 * Returns which ERP modules should be activated and how they should be configured
 * for a specific industry group.
 */
function epc_erp_industry_kit(string $groupCode): array
{
    $kits = array(
        'healthcare_medical' => array(
            'label' => 'Healthcare ERP Kit',
            'modules' => array('finance', 'inventory', 'procurement', 'sales', 'compliance', 'quality_control', 'batch_tracking'),
            'costing_method' => 'weighted_average',
            'inventory_features' => array('batch_tracking', 'expiry_dates', 'serial_numbers', 'cold_chain'),
            'compliance' => array('fda_usfda', 'ce_marking', 'iso_13485', 'gmp'),
            'reports' => array('inventory_by_expiry', 'batch_recall_report', 'regulatory_compliance', 'supplier_quality'),
            'integrations' => array('hospital_mis', 'pharmacy_system', 'medical_device_registry'),
        ),
        'automotive' => array(
            'label' => 'Automotive ERP Kit',
            'modules' => array('finance', 'inventory', 'procurement', 'sales', 'warranty', 'vehicle_compat', 'barcode'),
            'costing_method' => 'weighted_average',
            'inventory_features' => array('vehicle_compatibility', 'oem_cross_reference', 'barcode_scanning', 'multi_warehouse'),
            'compliance' => array('ece_un', 'sae_standards', 'iatf_16949'),
            'reports' => array('slow_moving_inventory', 'vehicle_fitment', 'warranty_claims', 'oem_xref'),
            'integrations' => array('laximo_oem', 'tecdoc', 'vehicle_database'),
        ),
        'food_beverage' => array(
            'label' => 'Food & Beverage ERP Kit',
            'modules' => array('finance', 'inventory', 'procurement', 'sales', 'batch_tracking', 'expiry_management', 'haccp_compliance'),
            'costing_method' => 'fifo',
            'inventory_features' => array('batch_tracking', 'expiry_dates', 'temperature_monitoring', 'allergen_tracking'),
            'compliance' => array('haccp', 'iso_22000', 'halal_cert', 'organic_cert'),
            'reports' => array('expiry_alert', 'batch_traceability', 'allergen_report', 'haccp_log'),
            'integrations' => array('cold_chain_iot', 'supplier_portal', 'delivery_fleet'),
        ),
        'jewellery_luxury' => array(
            'label' => 'Jewellery & Luxury ERP Kit',
            'modules' => array('finance', 'inventory', 'procurement', 'sales', 'gold_management', 'tag_system', 'hallmarking'),
            'costing_method' => 'specific_identification',
            'inventory_features' => array('tag_per_item', 'karat_tracking', 'stone_grading', 'weight_management', 'photo_per_item'),
            'compliance' => array('hallmarking_standards', 'kimberley_process', 'aml_kyc', 'tourist_refund'),
            'reports' => array('gold_stock_by_karat', 'stone_inventory', 'fix_unfix_report', 'daily_rate_pnl', 'tag_movement'),
            'integrations' => array('gold_rate_api', 'hallmark_office', 'insurance_api'),
        ),
        'construction_realestate' => array(
            'label' => 'Construction & Real Estate ERP Kit',
            'modules' => array('finance', 'inventory', 'procurement', 'sales', 'project_costing', 'landed_cost', 'document_vault'),
            'costing_method' => 'weighted_average',
            'inventory_features' => array('multi_warehouse', 'site_allocation', 'project_reservation', 'bulk_materials'),
            'compliance' => array('building_codes', 'safety_standards', 'environmental'),
            'reports' => array('project_cost_vs_budget', 'material_consumption', 'site_stock', 'subcontractor_claims'),
            'integrations' => array('project_management', 'bim_software', 'fleet_gps'),
        ),
        'hospitality_travel' => array(
            'label' => 'Hospitality ERP Kit',
            'modules' => array('finance', 'inventory', 'procurement', 'sales', 'reservations', 'housekeeping', 'pos_integration'),
            'costing_method' => 'weighted_average',
            'inventory_features' => array('par_levels', 'consumption_tracking', 'linen_management', 'minibar'),
            'compliance' => array('tourism_license', 'food_safety', 'fire_safety', 'dtcm'),
            'reports' => array('occupancy_rate', 'revenue_per_room', 'f_and_b_cost', 'housekeeping_status'),
            'integrations' => array('pms_system', 'channel_manager', 'pos_micros'),
        ),
        'electronics_technology' => array(
            'label' => 'Electronics & Technology ERP Kit',
            'modules' => array('finance', 'inventory', 'procurement', 'sales', 'warranty', 'serial_tracking', 'returns'),
            'costing_method' => 'weighted_average',
            'inventory_features' => array('serial_numbers', 'imei_tracking', 'warranty_registration', 'refurbished_tracking'),
            'compliance' => array('weee_directive', 'rohs', 'fcc_ce_marking'),
            'reports' => array('serial_movement', 'warranty_expiry', 'return_rate', 'brand_performance'),
            'integrations' => array('marketplace_sync', 'shipping_api', 'warranty_portal'),
        ),
        'fashion_apparel' => array(
            'label' => 'Fashion & Apparel ERP Kit',
            'modules' => array('finance', 'inventory', 'procurement', 'sales', 'size_matrix', 'season_management', 'returns'),
            'costing_method' => 'weighted_average',
            'inventory_features' => array('size_color_matrix', 'season_tracking', 'style_variants', 'markdown_automation'),
            'compliance' => array('textile_labeling', 'sustainability_cert', 'import_duties'),
            'reports' => array('sell_through_rate', 'size_curve_analysis', 'markdown_report', 'dead_stock'),
            'integrations' => array('marketplace_sync', 'influencer_platform', 'returns_portal'),
        ),
        'beauty_wellness' => array(
            'label' => 'Beauty & Wellness ERP Kit',
            'modules' => array('finance', 'inventory', 'procurement', 'sales', 'expiry_management', 'batch_tracking', 'appointments'),
            'costing_method' => 'fifo',
            'inventory_features' => array('batch_tracking', 'expiry_dates', 'shade_variants', 'tester_tracking'),
            'compliance' => array('cosmetics_regulation', 'ingredient_disclosure', 'animal_testing_free'),
            'reports' => array('expiry_forecast', 'bestseller_analysis', 'appointment_revenue', 'client_retention'),
            'integrations' => array('booking_system', 'loyalty_program', 'social_commerce'),
        ),
        'education_training' => array(
            'label' => 'Education ERP Kit',
            'modules' => array('finance', 'inventory', 'sales', 'student_management', 'scheduling', 'subscription_billing'),
            'costing_method' => 'standard',
            'inventory_features' => array('asset_tracking', 'library_management', 'consumables'),
            'compliance' => array('accreditation', 'data_protection', 'safeguarding'),
            'reports' => array('enrollment_trends', 'course_profitability', 'student_retention', 'faculty_utilization'),
            'integrations' => array('lms_platform', 'payment_gateway', 'student_portal'),
        ),
        'energy_utilities' => array(
            'label' => 'Energy & Utilities ERP Kit',
            'modules' => array('finance', 'inventory', 'procurement', 'sales', 'project_costing', 'maintenance', 'asset_tracking'),
            'costing_method' => 'weighted_average',
            'inventory_features' => array('asset_lifecycle', 'preventive_maintenance', 'spare_parts', 'field_service'),
            'compliance' => array('energy_regulations', 'environmental', 'safety_standards', 'grid_codes'),
            'reports' => array('asset_depreciation', 'maintenance_schedule', 'energy_output', 'project_margin'),
            'integrations' => array('scada_system', 'iot_sensors', 'grid_operator'),
        ),
        'manufacturing_industrial' => array(
            'label' => 'Manufacturing ERP Kit',
            'modules' => array('finance', 'inventory', 'procurement', 'sales', 'production_planning', 'quality_control', 'maintenance', 'landed_cost'),
            'costing_method' => 'standard',
            'inventory_features' => array('bom_management', 'work_orders', 'wip_tracking', 'quality_inspection'),
            'compliance' => array('iso_9001', 'iso_14001', 'osha', 'reach'),
            'reports' => array('production_efficiency', 'scrap_rate', 'oee_report', 'material_variance'),
            'integrations' => array('mes_system', 'plc_interface', 'cad_cam'),
        ),
        'agriculture_farming' => array(
            'label' => 'Agriculture ERP Kit',
            'modules' => array('finance', 'inventory', 'procurement', 'sales', 'crop_management', 'batch_tracking', 'weather_integration'),
            'costing_method' => 'fifo',
            'inventory_features' => array('batch_tracking', 'harvest_tracking', 'storage_conditions', 'organic_segregation'),
            'compliance' => array('organic_cert', 'gap_standards', 'phytosanitary', 'water_usage'),
            'reports' => array('crop_yield', 'input_cost_per_hectare', 'harvest_forecast', 'weather_impact'),
            'integrations' => array('weather_api', 'iot_soil_sensors', 'market_prices'),
        ),
    );

    return isset($kits[$groupCode]) ? $kits[$groupCode] : array(
        'label' => ucwords(str_replace('_', ' ', $groupCode)) . ' ERP Kit',
        'modules' => array('finance', 'inventory', 'procurement', 'sales'),
        'costing_method' => 'weighted_average',
        'inventory_features' => array('multi_warehouse', 'barcode_scanning'),
        'compliance' => array(),
        'reports' => array('stock_report', 'sales_analysis', 'aged_payables'),
        'integrations' => array(),
    );
}

/**
 * CP settings that align the frontend with ERP for a given industry.
 * Returns configuration that the CP settings panel should show/enforce.
 */
function epc_cp_industry_alignment(string $groupCode): array
{
    $alignments = array(
        'healthcare_medical' => array(
            'storefront_features' => array('product_specs_mandatory', 'certification_badges', 'bulk_pricing', 'rfq_button'),
            'product_fields' => array('specs', 'certifications', 'regulatory_class', 'shelf_life', 'storage_conditions'),
            'checkout_fields' => array('medical_license_number', 'institution_name', 'delivery_special_instructions'),
            'catalog_display' => 'grid_with_specs',
            'price_display' => 'request_quote_option',
        ),
        'automotive' => array(
            'storefront_features' => array('vehicle_selector', 'oem_cross_ref', 'compatibility_check', 'installation_guide'),
            'product_fields' => array('specs', 'vehicle_compatibility', 'oem_number', 'brand', 'condition'),
            'checkout_fields' => array('vehicle_make', 'vehicle_model', 'vehicle_year'),
            'catalog_display' => 'grid_with_vehicle_filter',
            'price_display' => 'standard',
        ),
        'food_beverage' => array(
            'storefront_features' => array('allergen_filter', 'dietary_badges', 'freshness_date', 'bulk_order'),
            'product_fields' => array('ingredients', 'allergens', 'nutritional_info', 'origin', 'best_before'),
            'checkout_fields' => array('delivery_time_preference', 'cold_chain_required'),
            'catalog_display' => 'cards_with_dietary_icons',
            'price_display' => 'per_unit_and_bulk',
        ),
        'jewellery_luxury' => array(
            'storefront_features' => array('360_view', 'zoom_detail', 'certification_viewer', 'customization_builder'),
            'product_fields' => array('metal_type', 'karat', 'weight_grams', 'stone_details', 'certificate_number'),
            'checkout_fields' => array('ring_size', 'engraving_text', 'gift_wrap'),
            'catalog_display' => 'luxury_gallery',
            'price_display' => 'on_request_option',
        ),
        'construction_realestate' => array(
            'storefront_features' => array('bulk_calculator', 'project_lists', 'delivery_scheduling', 'credit_account'),
            'product_fields' => array('specs', 'unit_of_measure', 'min_order_qty', 'lead_time', 'brand'),
            'checkout_fields' => array('site_address', 'project_reference', 'crane_access'),
            'catalog_display' => 'table_with_specs',
            'price_display' => 'tiered_pricing',
        ),
        'hospitality_travel' => array(
            'storefront_features' => array('room_configurator', 'bulk_amenity_order', 'repeat_order', 'seasonal_catalog'),
            'product_fields' => array('specs', 'color_options', 'min_order', 'lead_time', 'fire_rating'),
            'checkout_fields' => array('property_name', 'purchase_order', 'delivery_dock'),
            'catalog_display' => 'grid_with_room_sections',
            'price_display' => 'volume_discount',
        ),
    );

    return isset($alignments[$groupCode]) ? $alignments[$groupCode] : array(
        'storefront_features' => array('product_specs', 'search', 'filters'),
        'product_fields' => array('specs', 'brand', 'sku'),
        'checkout_fields' => array(),
        'catalog_display' => 'standard_grid',
        'price_display' => 'standard',
    );
}
