<?php
/**
 * Advanced ERP — full step-by-step in-app guide (CP body).
 *
 * Renders a step-by-step section for EVERY module (entitlement-aware: a
 * payroll-only tenant sees only its modules) plus the per-industry document
 * chains (LPO/PO -> GRN -> SO -> DO -> Invoice, etc.). Plain HTML so the
 * existing Google Translate layer can localise it. Degrades gracefully if the
 * entitlement table is absent (shows the full guide).
 */
defined('_ASTEXE_') or die('No access');

$doc = $_SERVER['DOCUMENT_ROOT'];
require_once $doc . '/content/shop/finance/epc_erp_guide_content.php';
require_once $doc . '/content/shop/finance/epc_erp_process_flows.php';
@require_once $doc . '/content/shop/finance/epc_erp_modules.php';
@require_once $doc . '/content/shop/finance/epc_erp_theme.php';

$backend = '/' . htmlspecialchars((string) $GLOBALS['DP_Config']->backend_dir, ENT_QUOTES, 'UTF-8');
$themeBase = $backend . '/content/shop/finance/erp/theme';

if (!isset($db_link) || !($db_link instanceof PDO)) {
    try {
        $db_link = new PDO(
            'mysql:host=' . $GLOBALS['DP_Config']->host . ';dbname=' . $GLOBALS['DP_Config']->db,
            $GLOBALS['DP_Config']->user,
            $GLOBALS['DP_Config']->password,
            array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
        );
    } catch (Exception $e) {
        $db_link = null;
    }
}

$enabled = array();
if ($db_link instanceof PDO && function_exists('epc_mod_enabled_list')) {
    try {
        $enabled = epc_mod_enabled_list($db_link, true);
    } catch (Exception $e) {
        $enabled = array();
    }
}

$sections = epc_guide_for_entitlements($enabled);

$esc = function ($v) {
    return htmlspecialchars((string) $v, ENT_QUOTES, 'UTF-8');
};

// Industry document chains for the workflow section.
$flows = array();
try {
    $flows = epc_flow_registry();
} catch (Exception $e) {
    $flows = array();
}
?>
<link rel="stylesheet" href="<?php echo $themeBase; ?>/erp_theme.css" />
<?php if (function_exists('epc_theme_style_tag')) { echo epc_theme_style_tag_for_surface('erp'); } ?>
<div class="erp-theme" style="background:transparent;min-height:auto">
<div class="erp-dash" style="padding-top:6px">
    <div class="erp-topbar">
        <div class="erp-brand">
            <div class="mark">?</div>
            <div class="name">ERP User Guide<small>Step-by-step &middot; every module &middot; per-industry workflows</small></div>
        </div>
        <div><span class="erp-chip">Modules shown: <b><?php echo count($sections); ?></b></span></div>
    </div>

    <div class="erp-panel" style="animation-delay:.05s">
        <h3>How to use this guide</h3>
        <p style="color:var(--erp-muted);line-height:1.8;margin:6px 0 0">
            Each module below has four parts: <b>What it does</b>, <b>Set up (in order)</b>, the <b>Daily workflow</b> click-path,
            and the <b>Accounting impact</b>. You only see modules your plan includes. Below the modules, the
            <b>industry document chains</b> show exactly which document is prepared at which stage (LPO/PO &rarr; GRN &rarr; SO &rarr; DO &rarr; Invoice).
        </p>
        <div style="margin-top:12px">
            <a href="#g-firstrun" class="erp-chip" style="display:inline-block;margin:3px 4px;text-decoration:none"><b>First-time setup &amp; configuration</b></a>
            <?php foreach ($sections as $key => $e): ?>
                <a href="#g-<?php echo $esc($key); ?>" class="erp-chip" style="display:inline-block;margin:3px 4px;text-decoration:none"><?php echo $esc($e['title']); ?></a>
            <?php endforeach; ?>
        </div>
    </div>

    <div class="erp-panel" id="g-firstrun" style="animation-delay:.06s">
        <h3>First-time setup &amp; configuration <span>&mdash; do these in order</span></h3>
        <p style="color:var(--erp-muted);line-height:1.7;margin:6px 0 12px">
            New tenant? Work top to bottom once — each step unlocks the next. After this the modules below are ready for daily use.
        </p>
        <ol style="color:var(--erp-text);line-height:1.9;padding-left:20px;margin:0">
            <li><b>Company profile</b> — legal/trade name, logo, address, TRN/VAT, trade licence and bank pay-to details. These print on every document.</li>
            <li><b>Country</b> — set the company country once; currency, language (incl. RTL), tax regime (VAT/GST rate + e-invoice scheme), fiscal-year start and the labour-law pack all localise together.</li>
            <li><b>Financial year &amp; periods</b> — set the year start and open the accounting periods (Fixed assets / Year-end closing → Setup).</li>
            <li><b>Chart of accounts</b> — load or adjust the COA every module posts to (General ledger → Setup).</li>
            <li><b>Number sequences</b> — confirm the voucher/document sequences (invoices, POs, requisitions, journals).</li>
            <li><b>Tax setup</b> — confirm the tax/VAT codes and rates for your country; add withholding codes if you withhold at source (Tax → Setup).</li>
            <li><b>Business units / legal entities</b> — define them so masters (bank accounts, vendors, customers, assets, employees) can be attached to the right unit.</li>
            <li><b>Bank accounts</b> — create your cash and bank accounts with full details (IBAN, SWIFT/BIC, GL account, business unit).</li>
            <li><b>Master data</b> — add vendors (Accounts payable), customers (Accounts receivable), items (Product information) and fixed assets to full depth.</li>
            <li><b>Opening balances</b> — enter go-live balances (trial balance, customer/vendor open items, stock on hand) as at your migration date.</li>
            <li><b>Users &amp; roles</b> — create users and assign roles/permissions; set approval thresholds and workflow (System administration).</li>
            <li><b>Go live</b> — start daily transactions; use each module’s <b>Reports &amp; inquiries</b> tab to monitor, and Year-end closing when the period ends.</li>
        </ol>
    </div>

    <?php foreach ($sections as $key => $e): ?>
    <div class="erp-panel" id="g-<?php echo $esc($key); ?>" style="animation-delay:.08s">
        <h3><?php echo $esc($e['title']); ?> <span>&mdash; <?php echo $esc($e['module']); ?></span></h3>
        <p style="color:var(--erp-text);line-height:1.7;margin:4px 0 10px"><?php echo $esc($e['what']); ?></p>

        <div style="display:grid;grid-template-columns:1fr 1fr;gap:18px">
            <div>
                <div style="color:var(--erp-acc);font-weight:600;margin-bottom:6px">Set up (in order)</div>
                <ol style="color:var(--erp-muted);line-height:1.7;padding-left:18px;margin:0">
                    <?php foreach ($e['setup'] as $s): ?><li><?php echo $esc($s); ?></li><?php endforeach; ?>
                </ol>
            </div>
            <div>
                <div style="color:var(--erp-acc2);font-weight:600;margin-bottom:6px">Daily workflow</div>
                <ol style="color:var(--erp-muted);line-height:1.7;padding-left:18px;margin:0">
                    <?php foreach ($e['daily'] as $s): ?><li><?php echo $esc($s); ?></li><?php endforeach; ?>
                </ol>
            </div>
        </div>

        <div style="margin-top:12px;padding:10px 12px;border-radius:10px;background:rgba(43,184,255,0.06);border:1px solid var(--erp-card-brd)">
            <b style="color:var(--erp-text)">Accounting impact:</b>
            <span style="color:var(--erp-muted)"><?php echo $esc($e['accounting']); ?></span>
        </div>
        <?php if (!empty($e['tips'])): ?>
        <ul style="color:var(--erp-muted);line-height:1.7;margin:10px 0 0;padding-left:18px">
            <?php foreach ($e['tips'] as $t): ?><li><i><?php echo $esc($t); ?></i></li><?php endforeach; ?>
        </ul>
        <?php endif; ?>
    </div>
    <?php endforeach; ?>

    <?php if (!empty($flows)): ?>
    <div class="erp-panel" id="g-workflows" style="animation-delay:.1s">
        <h3>Industry document workflows <span>&mdash; which document at which stage</span></h3>
        <p style="color:var(--erp-muted);line-height:1.7;margin:4px 0 14px">
            End-to-end document chain per industry, with who prepares it, the stage, and the posting impact.
        </p>
        <?php foreach ($flows as $ind => $flow): ?>
            <?php $steps = epc_flow_describe($ind); ?>
            <div style="margin-bottom:18px">
                <div style="color:var(--erp-acc);font-weight:600;margin-bottom:8px"><?php echo $esc($flow['label']); ?></div>
                <div style="overflow-x:auto">
                <table style="width:100%;border-collapse:collapse;font-size:12.5px">
                    <thead><tr style="text-align:left;color:var(--erp-muted)">
                        <th style="padding:6px 8px;border-bottom:1px solid var(--erp-card-brd)">#</th>
                        <th style="padding:6px 8px;border-bottom:1px solid var(--erp-card-brd)">Document</th>
                        <th style="padding:6px 8px;border-bottom:1px solid var(--erp-card-brd)">Prepared by</th>
                        <th style="padding:6px 8px;border-bottom:1px solid var(--erp-card-brd)">Stage</th>
                        <th style="padding:6px 8px;border-bottom:1px solid var(--erp-card-brd)">Posting impact</th>
                    </tr></thead>
                    <tbody>
                    <?php foreach ($steps as $s): ?>
                        <tr>
                            <td style="padding:6px 8px;border-bottom:1px solid rgba(120,160,220,0.06);color:var(--erp-muted)"><?php echo $esc($s['no']); ?></td>
                            <td style="padding:6px 8px;border-bottom:1px solid rgba(120,160,220,0.06)"><b><?php echo $esc($s['doc_code']); ?></b> &middot; <?php echo $esc($s['doc_name']); ?></td>
                            <td style="padding:6px 8px;border-bottom:1px solid rgba(120,160,220,0.06)"><?php echo $esc($s['role']); ?></td>
                            <td style="padding:6px 8px;border-bottom:1px solid rgba(120,160,220,0.06)"><?php echo $esc($s['stage']); ?></td>
                            <td style="padding:6px 8px;border-bottom:1px solid rgba(120,160,220,0.06);color:var(--erp-muted)"><?php echo $esc($s['posting']); ?></td>
                        </tr>
                    <?php endforeach; ?>
                    </tbody>
                </table>
                </div>
            </div>
        <?php endforeach; ?>
    </div>
    <?php endif; ?>
</div>
</div>
<?php
// End full guide.
