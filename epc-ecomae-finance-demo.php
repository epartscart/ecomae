<?php
/**
 * Seed a coherent set of GL journals into the live ecomae tenant DB so the
 * accounting modules (GL, P&L, Balance Sheet, Trial Balance, VAT) show real,
 * balanced numbers instead of empty/old screens.
 *
 * GET token=epartscart-deploy-2026 [&reset=1]
 * Idempotent: tagged 'FINDEMO-2026'; reset=1 clears prior tagged journals first.
 */
declare(strict_types=1);
require_once __DIR__ . '/epc_deploy_auth.php';
epc_deploy_require_token();
define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/general_pages/epc_portal.php';
@require_once __DIR__ . '/content/general_pages/epc_portal_db.php';
@require_once __DIR__ . '/content/general_pages/epc_portal_tenant.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_gl.php';
header('Content-Type: text/plain; charset=utf-8');
ini_set('display_errors', '1');
error_reporting(E_ALL);

$cfg = new DP_Config();
if (function_exists('epc_portal_apply_config')) {
    epc_portal_apply_config($cfg);
}
try {
    $db = new PDO(
        'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
        $cfg->user,
        $cfg->password,
        array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
    );
} catch (PDOException $e) {
    exit('DB connect failed: ' . $e->getMessage() . "\n");
}
echo "DB: {$cfg->db} @ {$cfg->host}\n";

epc_erp_gl_ensure_schema($db);

$TAG = 'FINDEMO-2026';

// Full wipe (demo tenant only): clear ALL GL journals/lines + zero COA opening
// balances so the seeded set produces a clean, balanced book.
if (!empty($_GET['wipe'])) {
    $db->exec('DELETE FROM `epc_erp_gl_lines`');
    $db->exec('DELETE FROM `epc_erp_gl_journals`');
    $db->exec('UPDATE `epc_erp_coa_accounts` SET `opening_balance` = 0');
    echo "Wipe: cleared all GL journals/lines and zeroed COA opening balances\n";
}

// Reset prior tagged journals (safe, no nested transactions).
if (!empty($_GET['reset'])) {
    $ids = $db->prepare('SELECT `id` FROM `epc_erp_gl_journals` WHERE `reference` LIKE ? OR `description` LIKE ?');
    $ids->execute(array('%' . $TAG . '%', '%' . $TAG . '%'));
    $jids = $ids->fetchAll(PDO::FETCH_COLUMN);
    if ($jids) {
        $in = implode(',', array_map('intval', $jids));
        $db->exec('DELETE FROM `epc_erp_gl_lines` WHERE `journal_id` IN (' . $in . ')');
        $db->exec('DELETE FROM `epc_erp_gl_journals` WHERE `id` IN (' . $in . ')');
        echo 'Reset: removed ' . count($jids) . " tagged journals\n";
    }
}

// Already seeded?
$exists = $db->prepare('SELECT COUNT(*) FROM `epc_erp_gl_journals` WHERE `reference` LIKE ? OR `description` LIKE ?');
$exists->execute(array('%' . $TAG . '%', '%' . $TAG . '%'));
if ((int) $exists->fetchColumn() > 0) {
    exit("Already seeded (use ?reset=1 to rebuild).\n");
}

$coa = function (string $code) use ($db): int {
    $a = epc_erp_gl_coa_by_code($db, $code);
    return (int) ($a['id'] ?? 0);
};
$cAR = $coa('1100'); $cBank = $coa('1010'); $cVatOut = $coa('2100');
$cVatIn = $coa('1150'); $cAP = $coa('2000'); $cEquity = $coa('3000');
$cRev = $coa('4000'); $cCogs = $coa('5000'); $cExp = $coa('6100');

$mStart = strtotime(date('Y-m-01 00:00:00'));
$day = function (int $n) use ($mStart): int { return $mStart + 86400 * $n; };
// Warm all schemas OUTSIDE any transaction so no DDL runs mid-journal
// (DDL causes an implicit commit in MySQL and breaks post_journal's txn).
require_once __DIR__ . '/content/shop/finance/epc_erp_vouchers.php';
epc_erp_vouchers_ensure_schema($db);
$jseq = 0;
$post = function (array $h, array $lines) use ($db, &$jseq): int {
    // Provide an explicit journal_no to bypass epc_erp_gl_next_journal_no(),
    // which runs schema DDL inside the transaction.
    $jseq++;
    if (empty($h['journal_no'])) {
        $h['journal_no'] = 'GV-FD' . date('ym') . '-' . str_pad((string) $jseq, 4, '0', STR_PAD_LEFT);
    }
    try {
        return (int) epc_erp_gl_post_journal($db, $h, $lines);
    } catch (Throwable $e) {
        echo '  POST FAIL [' . ($h['reference'] ?? '?') . ']: ' . $e->getMessage() . "\n";
        if ($db->inTransaction()) { $db->rollBack(); }
        return 0;
    }
};
echo "COA ids: AR=$cAR Bank=$cBank VatOut=$cVatOut VatIn=$cVatIn AP=$cAP Eq=$cEquity Rev=$cRev COGS=$cCogs Exp=$cExp\n";
$n = 0;

// 1) Capital injection
$n += (bool) $post(
    array('journal_date' => $day(1), 'reference' => 'GV-CAP ' . $TAG, 'description' => 'Owner capital injection ' . $TAG),
    array(
        array('coa_id' => $cBank, 'debit' => 500000, 'credit' => 0, 'line_note' => 'Capital to bank'),
        array('coa_id' => $cEquity, 'debit' => 0, 'credit' => 500000, 'line_note' => 'Owner equity'),
    )
);

// 2) Sales (ex-VAT amounts) -> AR / Revenue / VAT output 5%
$sales = array(12000, 18500, 9500, 22000, 15000, 31000, 8000, 27500);
$si = 0;
foreach ($sales as $ex) {
    $vat = round($ex * 0.05, 2); $tot = $ex + $vat; $si++;
    $n += (bool) $post(
        array('journal_date' => $day(2 + $si), 'reference' => 'SI-' . str_pad((string) $si, 3, '0', STR_PAD_LEFT) . ' ' . $TAG, 'description' => 'Sample sale ' . $TAG),
        array(
            array('coa_id' => $cAR, 'debit' => $tot, 'credit' => 0, 'line_note' => 'AR'),
            array('coa_id' => $cRev, 'debit' => 0, 'credit' => $ex, 'line_note' => 'Revenue'),
            array('coa_id' => $cVatOut, 'debit' => 0, 'credit' => $vat, 'line_note' => 'VAT 5% output'),
        )
    );
}
$salesTotalEx = array_sum($sales);

// 3) Purchases of stock (COGS ~58% of sales) -> COGS + VAT input / AP
$purch = array(9000, 14000, 7000, 16000, 11000, 21000);
$pi = 0;
foreach ($purch as $ex) {
    $vat = round($ex * 0.05, 2); $tot = $ex + $vat; $pi++;
    $n += (bool) $post(
        array('journal_date' => $day(2 + $pi), 'reference' => 'PI-' . str_pad((string) $pi, 3, '0', STR_PAD_LEFT) . ' ' . $TAG, 'description' => 'Sample purchase ' . $TAG),
        array(
            array('coa_id' => $cCogs, 'debit' => $ex, 'credit' => 0, 'line_note' => 'COGS'),
            array('coa_id' => $cVatIn, 'debit' => $vat, 'credit' => 0, 'line_note' => 'VAT 5% input'),
            array('coa_id' => $cAP, 'debit' => 0, 'credit' => $tot, 'line_note' => 'AP'),
        )
    );
}

// 4) Operating expenses -> Expense / Bank
$exps = array(array('Office rent', 12000), array('Salaries', 25000), array('Utilities', 3500), array('Marketing', 4500));
$ei = 0;
foreach ($exps as $e) {
    $ei++;
    $n += (bool) $post(
        array('journal_date' => $day(8 + $ei), 'reference' => 'PV-EXP' . $ei . ' ' . $TAG, 'description' => $e[0] . ' ' . $TAG),
        array(
            array('coa_id' => $cExp, 'debit' => $e[1], 'credit' => 0, 'line_note' => $e[0]),
            array('coa_id' => $cBank, 'debit' => 0, 'credit' => $e[1], 'line_note' => 'Paid from bank'),
        )
    );
}

// 5) Customer receipts (collect ~65% of AR) -> Bank / AR
$collect = round(($salesTotalEx * 1.05) * 0.65, 2);
$n += (bool) $post(
    array('journal_date' => $day(12), 'reference' => 'RV-COL ' . $TAG, 'description' => 'Customer collections ' . $TAG),
    array(
        array('coa_id' => $cBank, 'debit' => $collect, 'credit' => 0, 'line_note' => 'Collected from customers'),
        array('coa_id' => $cAR, 'debit' => 0, 'credit' => $collect, 'line_note' => 'AR settled'),
    )
);

// 6) Supplier payments (pay ~60% of AP) -> AP / Bank
$payAp = round((array_sum($purch) * 1.05) * 0.60, 2);
$n += (bool) $post(
    array('journal_date' => $day(13), 'reference' => 'PV-SUP ' . $TAG, 'description' => 'Supplier payments ' . $TAG),
    array(
        array('coa_id' => $cAP, 'debit' => $payAp, 'credit' => 0, 'line_note' => 'AP paid'),
        array('coa_id' => $cBank, 'debit' => 0, 'credit' => $payAp, 'line_note' => 'Paid from bank'),
    )
);

echo "Journals posted: {$n}\n";

// Report P&L + BS snapshot
$from = $mStart; $to = strtotime(date('Y-m-t 23:59:59'));
$pl = epc_erp_gl_pl_report($db, $from, $to);
$bs = epc_erp_gl_balance_sheet($db, $to);
echo 'P&L revenue: ' . number_format((float) ($pl['total_revenue'] ?? 0), 2) . "\n";
echo 'P&L expenses: ' . number_format((float) ($pl['total_expenses'] ?? 0), 2) . "\n";
echo 'P&L net profit: ' . number_format((float) ($pl['net_profit'] ?? 0), 2) . "\n";
echo 'BS assets: ' . number_format((float) ($bs['total_assets'] ?? 0), 2) . "\n";
echo 'BS liabilities: ' . number_format((float) ($bs['total_liabilities'] ?? 0), 2) . "\n";
echo 'BS equity: ' . number_format((float) ($bs['total_equity'] ?? 0), 2) . "\n";
echo 'BS balanced: ' . (!empty($bs['balanced']) ? 'YES' : 'NO') . "\n";
echo "Done.\n";
