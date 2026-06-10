<?php
/**
 * Seed ERP sample data and return dashboard / P&L / balance sheet results.
 * GET: token=epartscart-deploy-2026
 * Optional: force=1 to insert again (new batch), reset=1 to clear sample-tagged rows first
 */
declare(strict_types=1);
header('Content-Type: application/json; charset=utf-8');

if (($_GET['token'] ?? '') !== 'epartscart-deploy-2026') {
	http_response_code(403);
	exit(json_encode(array('status' => false, 'message' => 'Forbidden')));
}

define('_ASTEXE_', 1);
require_once __DIR__ . '/config.php';
require_once __DIR__ . '/content/shop/finance/epc_erp_helpers.php';

$cfg = new DP_Config();
$pdo = new PDO(
	'mysql:host=' . $cfg->host . ';dbname=' . $cfg->db . ';charset=utf8',
	$cfg->user,
	$cfg->password,
	array(PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION)
);

$sampleTag = 'SAMPLE-DEMO-2026';
$force = !empty($_GET['force']);
$reset = !empty($_GET['reset']);

function epc_sample_already(PDO $db, string $tag): bool
{
	$q = $db->prepare('SELECT COUNT(*) FROM `epc_erp_suppliers` WHERE `name` LIKE ?');
	$q->execute(array('%' . $tag . '%'));
	return (int)$q->fetchColumn() > 0;
}

function epc_sample_reset(PDO $db, string $tag): void
{
	$like = '%' . $tag . '%';
	$jids = $db->prepare('SELECT `id` FROM `epc_erp_gl_journals` WHERE `reference` LIKE ? OR `description` LIKE ?');
	$jids->execute(array($like, $like));
	$journalIds = $jids->fetchAll(PDO::FETCH_COLUMN);
	if ($journalIds) {
		$in = implode(',', array_map('intval', $journalIds));
		$db->exec('DELETE FROM `epc_erp_gl_lines` WHERE `journal_id` IN (' . $in . ')');
		$db->exec('DELETE FROM `epc_erp_gl_journals` WHERE `id` IN (' . $in . ')');
	}
	$db->prepare('DELETE FROM `epc_erp_supplier_accounting` WHERE `reference` LIKE ? OR `note` LIKE ?')->execute(array($like, $like));
	$db->prepare('DELETE FROM `epc_erp_purchases` WHERE `invoice_number` LIKE ? OR `note` LIKE ?')->execute(array($like, $like));
	$db->prepare('DELETE FROM `epc_erp_cash_bank_entries` WHERE `reference` LIKE ? OR `note` LIKE ?')->execute(array($like, $like));
	$db->prepare('DELETE FROM `epc_erp_suppliers` WHERE `name` LIKE ?')->execute(array($like));
}

try {
	epc_erp_full_ensure_schema($pdo);

	if ($reset) {
		epc_sample_reset($pdo, $sampleTag);
	}

	$inserted = array();
	if (!epc_sample_already($pdo, $sampleTag) || $force) {
		$now = time();
		$monthStart = strtotime(date('Y-m-01 00:00:00'));
		$day1 = $monthStart + 86400 * 2;
		$day2 = $monthStart + 86400 * 5;
		$day3 = $monthStart + 86400 * 10;

		// Suppliers
		$sup1 = epc_erp_create_supplier($pdo, array(
			'name' => 'AL ARQAN Parts [' . $sampleTag . ']',
			'contact_email' => 'sample.supplier1@epartscart.local',
			'trn' => '100000000000003',
		));
		$sup2 = epc_erp_create_supplier($pdo, array(
			'name' => 'Gulf Auto Supply [' . $sampleTag . ']',
			'contact_email' => 'sample.supplier2@epartscart.local',
			'trn' => '100000000000004',
		));
		$inserted['suppliers'] = array($sup1, $sup2);

		// Purchases (auto GL)
		$p1 = epc_erp_create_purchase($pdo, array(
			'supplier_id' => $sup1,
			'invoice_number' => $sampleTag . '-INV-001',
			'purchase_date' => $day1,
			'amount_ex_vat' => 2000.00,
			'note' => $sampleTag . ' sample purchase — brake pads stock',
		));
		$p2 = epc_erp_create_purchase($pdo, array(
			'supplier_id' => $sup2,
			'invoice_number' => $sampleTag . '-INV-002',
			'purchase_date' => $day2,
			'amount_ex_vat' => 1500.00,
			'note' => $sampleTag . ' sample purchase — filters stock',
		));
		$inserted['purchases'] = array($p1, $p2);

		// Cash accounts
		$accounts = epc_erp_list_cash_accounts($pdo);
		if (empty($accounts)) {
			epc_erp_create_cash_account($pdo, array('name' => 'Main cash — AED', 'account_type' => 'cash', 'opening_balance' => 5000));
			epc_erp_create_cash_account($pdo, array('name' => 'Main bank — AED', 'account_type' => 'bank', 'bank_name' => 'Emirates NBD', 'opening_balance' => 25000));
			$accounts = epc_erp_list_cash_accounts($pdo);
		}
		$cashId = (int)$accounts[0]['id'];
		$bankId = (int)(isset($accounts[1]) ? $accounts[1]['id'] : $accounts[0]['id']);

		// Customer receipt (cash in)
		$rcpt = epc_erp_cash_entry($pdo, array(
			'account_id' => $bankId,
			'time' => $day2,
			'direction' => 1,
			'amount' => 3150.00,
			'counterparty_type' => 'customer',
			'reference' => $sampleTag . '-RCPT-001',
			'note' => $sampleTag . ' customer bank transfer received',
		));
		$inserted['cash_receipt'] = $rcpt;

		// Supplier payment
		$pay = epc_erp_supplier_payment($pdo, array(
			'supplier_id' => $sup1,
			'account_id' => $bankId,
			'amount' => 1050.00,
			'time' => $day3,
			'reference' => $sampleTag . '-PAY-001',
			'note' => $sampleTag . ' partial payment to AL ARQAN',
			'purchase_id' => $p1,
		));
		$inserted['supplier_payment'] = $pay;

		// Expense payment
		$exp = epc_erp_cash_entry($pdo, array(
			'account_id' => $cashId,
			'time' => $day3,
			'direction' => 0,
			'amount' => 250.00,
			'reference' => $sampleTag . '-EXP-001',
			'note' => $sampleTag . ' office supplies expense',
		));
		$inserted['expense'] = $exp;

		// Manual GL — sample sales (Dr AR, Cr Revenue, Cr VAT)
		$ar = epc_erp_gl_coa_by_code($pdo, '1100');
		$rev = epc_erp_gl_coa_by_code($pdo, '4000');
		$vat = epc_erp_gl_coa_by_code($pdo, '2100');
		if ($ar && $rev && $vat) {
			$saleEx = 3000.00;
			$saleVat = 150.00;
			$saleTot = 3150.00;
			$jid = epc_erp_gl_post_journal($pdo, array(
				'journal_date' => $day2,
				'reference' => $sampleTag . '-SALE-001',
				'description' => $sampleTag . ' sample parts sale to wholesale customer',
				'source_type' => 'sales',
				'source_id' => 0,
			), array(
				array('coa_id' => (int)$ar['id'], 'debit' => $saleTot, 'credit' => 0, 'line_note' => 'Sample sale AR'),
				array('coa_id' => (int)$rev['id'], 'debit' => 0, 'credit' => $saleEx, 'line_note' => 'Sample sales revenue'),
				array('coa_id' => (int)$vat['id'], 'debit' => 0, 'credit' => $saleVat, 'line_note' => 'VAT 5% output'),
			));
			$inserted['gl_sales_journal'] = $jid;
		}

		$inserted['message'] = 'Sample data inserted';
	} else {
		$inserted['message'] = 'Sample data already exists — use ?force=1 or ?reset=1';
	}

	$date_from = strtotime(date('Y-m-01 00:00:00'));
	$date_to = time();

	$dashboard = epc_erp_dashboard($pdo, $date_from, $date_to);
	$pl = epc_erp_gl_pl_report($pdo, $date_from, $date_to);
	$bs = epc_erp_gl_balance_sheet($pdo, $date_to);
	$trial = epc_erp_gl_trial_balance($pdo, $date_to);
	$coa = epc_erp_gl_list_coa($pdo);
	$suppliers = epc_erp_list_suppliers($pdo);
	$purchases = epc_erp_list_purchases($pdo, 20);
	$cashEntries = epc_erp_list_cash_entries($pdo, 0, 20);
	$journals = epc_erp_gl_list_journals($pdo, $date_from, $date_to, 20);

	$coaSummary = array();
	foreach ($coa as $a) {
		if (abs((float)$a['balance']) >= 0.01) {
			$coaSummary[] = array(
				'code' => $a['code'],
				'name' => $a['name'],
				'type' => $a['account_type'],
				'balance' => round((float)$a['balance'], 2),
			);
		}
	}

	echo json_encode(array(
		'status' => true,
		'sample_tag' => $sampleTag,
		'inserted' => $inserted,
		'cp_urls' => array(
			'erp' => '/' . $cfg->backend_dir . '/shop/finance/erp',
			'guide' => '/' . $cfg->backend_dir . '/shop/finance/erp/guide',
			'coa' => '/' . $cfg->backend_dir . '/shop/finance/erp?tab=coa',
			'gl' => '/' . $cfg->backend_dir . '/shop/finance/erp?tab=gl',
			'pl' => '/' . $cfg->backend_dir . '/shop/finance/erp?tab=pl',
			'balance_sheet' => '/' . $cfg->backend_dir . '/shop/finance/erp?tab=balance_sheet',
		),
		'results' => array(
			'dashboard' => array(
				'revenue_ex_vat' => round($dashboard['revenue_ex_vat'], 2),
				'profit_ex_vat' => round($dashboard['profit_ex_vat'], 2),
				'receivable_due_orders' => round($dashboard['receivable_due_orders'], 2),
				'customer_ledger_balance' => round($dashboard['customer_ledger_balance'], 2),
				'payable_balance' => round($dashboard['payable_balance'], 2),
				'cash_bank_total' => round($dashboard['cash_bank_total'], 2),
			),
			'profit_and_loss' => array(
				'total_revenue' => round($pl['total_revenue'], 2),
				'total_expenses' => round($pl['total_expenses'], 2),
				'net_profit' => round($pl['net_profit'], 2),
				'revenue_lines' => $pl['revenue'],
				'expense_lines' => $pl['expenses'],
			),
			'balance_sheet' => array(
				'total_assets' => round($bs['total_assets'], 2),
				'total_liabilities' => round($bs['total_liabilities'], 2),
				'total_equity' => round($bs['total_equity'], 2),
				'current_earnings' => round($bs['current_earnings'], 2),
				'balanced' => abs($bs['total_assets'] - $bs['total_liabilities_equity']) < 0.05,
				'assets' => $bs['assets'],
				'liabilities' => $bs['liabilities'],
				'equity' => $bs['equity'],
			),
			'trial_balance' => array(
				'total_debit' => round($trial['total_debit'], 2),
				'total_credit' => round($trial['total_credit'], 2),
			),
			'coa_balances' => $coaSummary,
			'suppliers' => array_map(function ($s) {
				return array('id' => (int)$s['id'], 'name' => $s['name'], 'balance' => round((float)$s['balance'], 2));
			}, array_filter($suppliers, function ($s) use ($sampleTag) {
				return strpos($s['name'], $sampleTag) !== false;
			})),
			'sample_purchases' => array_values(array_filter(array_map(function ($p) {
				return array(
					'id' => (int)$p['id'],
					'invoice' => $p['invoice_number'],
					'total' => round((float)$p['total_amount'], 2),
					'status' => $p['status'],
				);
			}, $purchases), function ($p) use ($sampleTag) {
				return strpos($p['invoice'], $sampleTag) !== false;
			})),
			'gl_journals_count' => count($journals),
			'cash_entries_count' => count($cashEntries),
		),
	), JSON_PRETTY_PRINT | JSON_UNESCAPED_UNICODE);
} catch (Throwable $e) {
	http_response_code(500);
	echo json_encode(array(
		'status' => false,
		'message' => $e->getMessage(),
	), JSON_PRETTY_PRINT);
}
