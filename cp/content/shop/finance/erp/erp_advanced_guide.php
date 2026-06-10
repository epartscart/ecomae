<?php
/**
 * Advanced ERP — full in-app user guide (CP documentation).
 *
 * Plain-HTML, translation-friendly content (works with the existing Google
 * Translate layer). Renders an end-to-end workflow plus per-module guidance,
 * and a live "current configuration" panel read from the database.
 */
defined('_ASTEXE_') or die('No access');

$doc = $_SERVER['DOCUMENT_ROOT'];
require_once $doc . '/content/shop/finance/epc_erp_advanced.php';
require_once $doc . '/content/shop/finance/epc_erp_industry.php';

if (!isset($db_link) || !($db_link instanceof PDO)) {
    try {
        $db_link = new PDO(
            'mysql:host=' . $GLOBALS['DP_Config']->host . ';dbname=' . $GLOBALS['DP_Config']->db,
            $GLOBALS['DP_Config']->user,
            $GLOBALS['DP_Config']->password,
            array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
        );
        $db_link->query('SET NAMES utf8;');
    } catch (Exception $e) {
        echo '<div class="alert alert-danger">Database connection failed.</div>';
        return;
    }
}

$backend = '/' . htmlspecialchars((string) $GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8');

// Live configuration snapshot (degrades gracefully).
$current = array('key' => '', 'label' => '', 'item_type' => '', 'default_unit' => '');
$industryCount = 0;
$taxKits = 0;
$taxCatalog = 0;
try {
    $current = epc_erp_industry_current($db_link);
    $industryCount = count(epc_erp_industry_catalog());
} catch (Exception $e) {
}
try {
    $taxKits = (int) $db_link->query('SELECT COUNT(*) FROM `epc_tax_toolkit_installs`')->fetchColumn();
} catch (Exception $e) {
    $taxKits = 0;
}
try {
    $taxCatalog = (int) $db_link->query('SELECT COUNT(*) FROM `epc_tax_toolkits`')->fetchColumn();
} catch (Exception $e) {
    $taxCatalog = 0;
}

$h = 'epc_erp_adv_h';

// Workflow steps (end-to-end).
$workflow = array(
    array('Set your industry', 'Open the ERP setup and choose your industry (auto parts, electronics, fashion, jewellery, food, pharma, services, and more). This instantly configures the right product fields, units and item type for your business.'),
    array('Configure your tax profile', 'Pick your country in the Tax Toolkit. The worldwide tax engine applies the correct VAT / GST / sales-tax automatically — for the company and per customer country.'),
    array('Add warehouses & products', 'Create one or more warehouses, then add products. Industry-specific fields (e.g. OEM number, size/colour, batch/expiry, IMEI) appear automatically based on your industry.'),
    array('Record purchases (procurement)', 'Add suppliers and record purchase invoices. Stock, weighted-average cost and input tax are updated, and the general ledger is posted automatically.'),
    array('Sell & invoice', 'Create sales orders / invoices. Output tax is calculated by the tax engine, stock is reduced, and revenue is posted to the ledger. E-invoicing fields are included where required.'),
    array('Manage customers in CRM', 'Capture leads, score them, move opportunities through the pipeline, send tax-correct quotes, and convert won deals into orders — all linked to the same customers and ledger.'),
    array('Run payroll & track assets', 'Process staff payroll and manage fixed assets with depreciation. Both post to the ledger so your books stay complete.'),
    array('Review reports', 'See the live dashboard: profit & loss, balance sheet, tax due, inventory valuation and CRM forecast — in your language and currency.'),
);

// Per-module reference.
$modules = array(
    array(
        'title' => 'Industry foundation',
        'icon' => 'fa-industry',
        'body' => 'Choose from ' . (int) $industryCount . ' ready industry blueprints. Each one seeds the right inventory custom fields, default unit of measure and item type (standard, perishable with expiry, or serialized). You can switch or combine industries at any time — applying an industry only adds fields, it never deletes your data.',
        'tips' => array(
            'Auto parts: OEM/article, fits make/model/year, position.',
            'Food / pharma: batch number and expiry (FEFO) tracking.',
            'Electronics / jewellery: per-unit serial / IMEI / certificate.',
            'Fashion: size, colour, material, gender variants.',
            'Services: hourly / fixed / retainer billing basis.',
        ),
    ),
    array(
        'title' => 'Products & inventory',
        'icon' => 'fa-cubes',
        'body' => 'Multi-warehouse stock with weighted-average costing, batch / serial tracking, expiry dates and unlimited custom fields. Every stock movement is journalled so inventory valuation always ties back to the ledger.',
        'tips' => array(
            'Use warehouses for shops, vans or bonded stores.',
            'Custom fields are searchable and show on documents.',
            'Opening balances can be imported when you go live.',
        ),
    ),
    array(
        'title' => 'Worldwide tax engine',
        'icon' => 'fa-globe',
        'body' => 'Built on the worldwide tax toolkit: per-tenant and per-customer tax profiles resolve the correct treatment by country — UAE/GCC VAT, EU VAT, UK VAT, India GST, US sales tax, and zero-rated / exempt handling. Corporate-tax estimates are included. ' . (int) $taxKits . ' tax kit(s) installed of ' . (int) $taxCatalog . ' available country kits.',
        'tips' => array(
            'Set the company country once; customers can override by their country.',
            'Quotes, invoices and purchases all use the same engine for consistency.',
            'Install more country kits any time from the Tax Toolkit.',
        ),
    ),
    array(
        'title' => 'Procurement & suppliers',
        'icon' => 'fa-truck',
        'body' => 'Record suppliers, purchase invoices and supplier payments. Input tax is captured, stock and average cost update automatically, and supplier balances are tracked for accurate payables.',
        'tips' => array(
            'Link purchases to warehouses to update the right stock.',
            'Supplier statements show outstanding balances.',
        ),
    ),
    array(
        'title' => 'Sales & invoicing',
        'icon' => 'fa-file-text',
        'body' => 'Create tax-correct sales invoices and credit notes in any currency. Output tax, stock reduction and ledger posting happen together. E-invoicing fields are supported where the country requires them.',
        'tips' => array(
            'Convert an accepted CRM quote straight into an order.',
            'Multi-currency invoices show the customer-country tax label.',
        ),
    ),
    array(
        'title' => 'Advanced purchasing (RFQ)',
        'icon' => 'fa-handshake-o',
        'body' => 'Run a request-for-quotation: send line items to multiple suppliers, capture their prices and lead times, compare them side by side, and award the best supplier straight into a purchase order — no re-keying.',
        'tips' => array(
            'The cheapest supplier per line is highlighted automatically.',
            'Suppliers are ranked by total quoted value.',
            'Awarding an RFQ creates a draft PO using the winning prices.',
        ),
    ),
    array(
        'title' => 'Demand forecasting & planning',
        'icon' => 'fa-line-chart',
        'body' => 'The planner reads your real sales history to compute average daily demand, a trend (up / flat / down) and a moving-average forecast. It calculates a reorder point per item (lead time + safety stock) and suggests exactly how much to order to cover the next cycle.',
        'tips' => array(
            'Set lead time, safety stock and minimum order qty per item.',
            'Suggestions respect your minimum order quantity.',
            'Items below their reorder point are listed first.',
        ),
    ),
    array(
        'title' => 'Landed cost',
        'icon' => 'fa-ship',
        'body' => 'Spread freight, customs duty, insurance and other import charges across received items — by value, quantity or weight — so your stock cost reflects the true landed cost. Applying a voucher rolls the per-unit add-on into the weighted-average cost.',
        'tips' => array(
            'Choose value / qty / weight as the allocation basis.',
            'Allocation always sums exactly to the charges entered.',
            'Apply is idempotent — it cannot double-count.',
        ),
    ),
    array(
        'title' => 'Shipping & logistics',
        'icon' => 'fa-plane',
        'body' => 'Track inbound and outbound shipments with carriers, tracking numbers and ETAs. The logistics dashboard shows in-transit and overdue shipments, and receiving an inbound shipment books the goods straight into the right warehouse.',
        'tips' => array(
            'Carrier tracking links are built automatically from the tracking number.',
            'Overdue shipments (ETA passed, not delivered) are flagged.',
            'Receiving updates stock and weighted-average cost.',
        ),
    ),
    array(
        'title' => 'Advanced CRM',
        'icon' => 'fa-users',
        'body' => 'Leads are automatically scored (hot / warm / cold) from status, contact details, value and activity. The pipeline gives a weighted forecast and win-rate, the Customer-360 view merges CRM with ERP sales, and quotes are priced with the same worldwide tax engine.',
        'tips' => array(
            'Work the "hot" leads first — they are sorted to the top.',
            'Use the next-best-action list to clear overdue follow-ups.',
            'Won opportunities convert to orders without re-keying.',
        ),
    ),
    array(
        'title' => 'Accounting, payroll & assets',
        'icon' => 'fa-calculator',
        'body' => 'A full double-entry general ledger underpins everything, with profit & loss and balance sheet. Payroll processes salaries and posts to the ledger; fixed assets track depreciation over time.',
        'tips' => array(
            'Chart of accounts is pre-seeded and extensible.',
            'Every module posts to the ledger — no manual re-entry.',
        ),
    ),
);
?>
<style>
.epc-erp-guide-intro { background: linear-gradient(135deg, #0f172a 0%, #1e4d3a 100%); color: #fff; border-radius: 8px; padding: 22px 24px; margin-bottom: 18px; }
.epc-erp-guide-intro h3 { margin: 0 0 8px; color: #fff; }
.epc-erp-guide-intro p { margin: 0; opacity: .9; }
.epc-erp-status { display: flex; flex-wrap: wrap; gap: 10px; margin: 14px 0 4px; }
.epc-erp-status .chip { background: rgba(255,255,255,.12); border: 1px solid rgba(255,255,255,.2); color: #fff; border-radius: 20px; padding: 5px 14px; font-size: 12px; }
.epc-erp-guide-step { border-left: 4px solid #27ae60; padding: 12px 16px; margin: 12px 0; background: #f8fafc; border-radius: 0 6px 6px 0; }
.epc-erp-guide-step h5 { margin: 0 0 6px; font-weight: 700; color: #0f172a; }
.epc-erp-guide-step .num { display: inline-block; width: 24px; height: 24px; line-height: 24px; text-align: center; border-radius: 50%; background: #27ae60; color: #fff; font-size: 12px; margin-right: 8px; }
.epc-erp-mod { border: 1px solid #e6eaf0; border-radius: 8px; padding: 16px 18px; margin-bottom: 14px; background: #fff; }
.epc-erp-mod h4 { margin: 0 0 8px; color: #0f172a; font-size: 16px; }
.epc-erp-mod h4 i { color: #27ae60; margin-right: 8px; }
.epc-erp-mod ul { margin: 8px 0 0; padding-left: 18px; }
.epc-erp-mod li { font-size: 13px; line-height: 1.7; color: #475569; }
.epc-erp-flow { font-size: 13px; line-height: 1.7; color: #334155; }
</style>

<div class="col-lg-12">
    <div class="epc-erp-guide-intro">
        <h3><i class="fa fa-book"></i> Advanced ERP — Complete User Guide</h3>
        <p>An industry-agnostic ERP for any business in the world: configurable products &amp; inventory, worldwide taxation, advanced CRM, accounting, payroll and assets — with multilingual support.</p>
        <div class="epc-erp-status">
            <span class="chip"><i class="fa fa-industry"></i> Industry: <?php echo $h($current['label'] !== '' ? $current['label'] : 'not set'); ?></span>
            <span class="chip"><i class="fa fa-cube"></i> Default item: <?php echo $h($current['item_type'] !== '' ? $current['item_type'] : 'standard'); ?></span>
            <span class="chip"><i class="fa fa-globe"></i> Tax kits: <?php echo (int) $taxKits; ?> installed / <?php echo (int) $taxCatalog; ?> available</span>
            <span class="chip"><i class="fa fa-list"></i> Industries available: <?php echo (int) $industryCount; ?></span>
        </div>
    </div>

    <div class="hpanel">
        <div class="panel-heading hbuilt">
            End-to-end workflow
            <span class="pull-right">
                <a class="btn btn-primary btn-xs" href="<?php echo $backend; ?>/shop/finance/erp"><i class="fa fa-calculator"></i> Open ERP</a>
                <a class="btn btn-default btn-xs" href="<?php echo $backend; ?>/shop/finance/crm"><i class="fa fa-users"></i> Open CRM</a>
            </span>
        </div>
        <div class="panel-body epc-erp-flow">
            <p>Follow these steps to run your business end-to-end. Each step links to the module that does the work.</p>
            <?php foreach ($workflow as $i => $step): ?>
            <div class="epc-erp-guide-step">
                <h5><span class="num"><?php echo (int) ($i + 1); ?></span><?php echo $h($step[0]); ?></h5>
                <div><?php echo $h($step[1]); ?></div>
            </div>
            <?php endforeach; ?>
        </div>
    </div>

    <div class="hpanel">
        <div class="panel-heading hbuilt">Module reference</div>
        <div class="panel-body">
            <?php foreach ($modules as $m): ?>
            <div class="epc-erp-mod">
                <h4><i class="fa <?php echo $h($m['icon']); ?>"></i><?php echo $h($m['title']); ?></h4>
                <div class="epc-erp-flow"><?php echo $h($m['body']); ?></div>
                <?php if (!empty($m['tips'])): ?>
                <ul>
                    <?php foreach ($m['tips'] as $tip): ?>
                    <li><?php echo $h($tip); ?></li>
                    <?php endforeach; ?>
                </ul>
                <?php endif; ?>
            </div>
            <?php endforeach; ?>
        </div>
    </div>

    <div class="hpanel">
        <div class="panel-heading hbuilt">Languages</div>
        <div class="panel-body epc-erp-flow">
            <p>This guide and the ERP screens work with the platform's built-in Google Translate layer. Use the language selector to switch languages; numbers, currency and right-to-left (RTL) layouts adapt automatically.</p>
        </div>
    </div>
</div>
<?php
// End of guide.
?>
