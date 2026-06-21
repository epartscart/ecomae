<?php
/**
 * Module: Account Receivable setup.
 * Sub-modules: Method of payment, Terms of payment, Customer group,
 * + links to customer invoice / customer payment journals.
 */
defined('_ASTEXE_') or die('No access');

require_once __DIR__ . '/erp_pm_render.php';
epc_erp_pm_inline_assets();

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_credit.php';
$csrfLocal = isset($csrf) ? $csrf : '';

$view = isset($_GET['pm_view']) ? (string) $_GET['pm_view'] : 'customer';
$subs = array(
	'customer' => 'Customer master',
	'methods' => 'Method of payment',
	'terms' => 'Terms of payment',
	'groups' => 'Customer group',
	'journals' => 'Journals',
);

echo '<div class="epc-erp-section"><h3 style="margin-top:0;"><i class="fa fa-handshake-o"></i> Account Receivable — setup</h3>';
echo '<p class="text-muted">Configure payment methods, payment terms and customer groups, then post customer invoice &amp; payment journals. Per-tenant and fully configurable.</p></div>';

epc_erp_pm_module_tabs($erpUrl, 'ar_setup', 'sales', $date_from_str, $date_to_str, $subs, $view);

$termOpts = array('0' => '— none —');
try {
	foreach (epc_erp_pm_list($db_link, 'epc_erp_pm_pay_terms', true) as $t) {
		$termOpts[(string) $t['id']] = $t['code'] . ' · ' . $t['name'];
	}
} catch (Exception $e) {
}

switch ($view) {
	case 'customer':
		$leOptsC = array();
		$buOptsC = array();
		try {
			foreach ($db_link->query("SELECT `id`, `code`, `name` FROM `epc_erp_pm_legal_entities` WHERE `active` = 1 ORDER BY `name`")->fetchAll(PDO::FETCH_ASSOC) as $le) {
				$leOptsC[(int) $le['id']] = $le['code'] . ' · ' . $le['name'];
			}
		} catch (Exception $e) {
		}
		try {
			foreach ($db_link->query("SELECT `id`, `code`, `name` FROM `epc_erp_pm_business_units` WHERE `active` = 1 ORDER BY `name`")->fetchAll(PDO::FETCH_ASSOC) as $bu) {
				$buOptsC[(int) $bu['id']] = $bu['code'] . ' · ' . $bu['name'];
			}
		} catch (Exception $e) {
		}
		$custGroups = array();
		try {
			foreach (epc_erp_pm_list($db_link, 'epc_erp_pm_customer_groups', true) as $g) {
				$custGroups[] = $g['code'] . ' · ' . $g['name'];
			}
		} catch (Exception $e) {
		}
		$masters = array();
		try {
			epc_credit_ensure_schema($db_link);
			$masters = $db_link->query("SELECT * FROM `epc_credit_profiles` ORDER BY `time_updated` DESC LIMIT 100")->fetchAll(PDO::FETCH_ASSOC);
		} catch (Exception $e) {
		}
		?>
		<div id="epc_erp_msg" class="alert" style="display:none;"></div>
		<div class="epc-erp-section">
			<h4 style="margin-top:0;"><i class="fa fa-user"></i> Customer master</h4>
			<p class="text-muted">Maintain the full customer record — account &amp; group, legal entity / business unit, currency &amp; terms, tax registration, delivery, contact and address. The Customer ID is the platform account (user) ID this record extends.</p>
			<form id="epc_erp_customer_master" class="form-horizontal" style="max-width:960px;">
				<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrfLocal); ?>">
				<div class="row">
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Customer ID *</label><div class="col-sm-8"><input type="number" name="customer_id" class="form-control input-sm" placeholder="Platform account / user ID" required></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Customer account</label><div class="col-sm-8"><input type="text" name="customer_account" class="form-control input-sm" placeholder="e.g. C-0001"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Customer name</label><div class="col-sm-8"><input type="text" name="customer_name" class="form-control input-sm"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Customer group</label><div class="col-sm-8"><input type="text" name="customer_group" class="form-control input-sm" list="epc_cust_groups" placeholder="e.g. Wholesale / Retail">
						<datalist id="epc_cust_groups"><?php foreach ($custGroups as $g): ?><option value="<?php echo epc_erp_h($g); ?>"></option><?php endforeach; ?></datalist>
					</div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Legal entity</label><div class="col-sm-8"><select name="legal_entity_id" class="form-control input-sm"><option value="0">— none —</option>
						<?php foreach ($leOptsC as $v => $t): ?><option value="<?php echo (int) $v; ?>"><?php echo epc_erp_h($t); ?></option><?php endforeach; ?>
					</select></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Business unit</label><div class="col-sm-8"><select name="business_unit_id" class="form-control input-sm"><option value="0">— none —</option>
						<?php foreach ($buOptsC as $v => $t): ?><option value="<?php echo (int) $v; ?>"><?php echo epc_erp_h($t); ?></option><?php endforeach; ?>
					</select></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Currency</label><div class="col-sm-8"><input type="text" name="currency_code" class="form-control input-sm" placeholder="AED"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Country</label><div class="col-sm-8"><input type="text" name="country_code" class="form-control input-sm" placeholder="AE"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Credit limit</label><div class="col-sm-8"><input type="number" step="0.01" name="credit_limit" class="form-control input-sm" placeholder="0.00"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Terms (days)</label><div class="col-sm-8"><input type="number" name="terms_days" class="form-control input-sm" placeholder="30"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Payment method</label><div class="col-sm-8"><input type="text" name="payment_method" class="form-control input-sm" placeholder="e.g. Bank transfer"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Risk band</label><div class="col-sm-8"><select name="risk_band" class="form-control input-sm"><option value="normal">Normal</option><option value="watch">Watch</option><option value="high">High</option></select></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Delivery terms</label><div class="col-sm-8"><input type="text" name="delivery_terms" class="form-control input-sm" placeholder="Incoterms e.g. CIF / FOB"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Delivery mode</label><div class="col-sm-8"><input type="text" name="delivery_mode" class="form-control input-sm" placeholder="e.g. Courier / Road"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">TRN / Tax reg.</label><div class="col-sm-8"><input type="text" name="trn" class="form-control input-sm" placeholder="TRN (UAE VAT)"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Sales tax group</label><div class="col-sm-8"><input type="text" name="sales_tax_group" class="form-control input-sm" placeholder="e.g. STD / EXEMPT"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Contact person</label><div class="col-sm-8"><input type="text" name="contact_person" class="form-control input-sm"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">E-mail</label><div class="col-sm-8"><input type="email" name="contact_email" class="form-control input-sm"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Phone</label><div class="col-sm-8"><input type="text" name="contact_phone" class="form-control input-sm"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Website</label><div class="col-sm-8"><input type="text" name="website" class="form-control input-sm" placeholder="https://"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Address</label><div class="col-sm-8"><input type="text" name="address" class="form-control input-sm" placeholder="Street, building"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">City</label><div class="col-sm-8"><input type="text" name="city" class="form-control input-sm"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">State / region</label><div class="col-sm-8"><input type="text" name="state_region" class="form-control input-sm"></div></div>
					<div class="col-sm-6 form-group"><label class="col-sm-4 control-label">Postal code</label><div class="col-sm-8"><input type="text" name="postal_code" class="form-control input-sm"></div></div>
					<div class="col-sm-12 form-group"><label class="col-sm-2 control-label">Notes</label><div class="col-sm-10"><input type="text" name="notes" class="form-control input-sm"></div></div>
				</div>
				<label class="checkbox-inline"><input type="checkbox" name="on_hold" value="1"> On credit hold</label>
				<label class="checkbox-inline"><input type="checkbox" name="tax_exempt" value="1"> Tax exempt</label>
				<div style="margin-top:8px;"><button type="submit" class="btn btn-sm btn-primary">Save customer master</button></div>
			</form>
		</div>
		<div class="epc-erp-section">
			<h4><i class="fa fa-list"></i> Customer master records</h4>
			<table class="table table-condensed table-striped">
				<thead><tr><th>Cust ID</th><th>Account</th><th>Name</th><th>Group</th><th>Currency</th><th>Credit limit</th><th>Terms</th><th>On hold</th><th>TRN</th></tr></thead>
				<tbody>
				<?php if (empty($masters)): ?>
					<tr><td colspan="9" class="text-muted">No customer master records yet. Create one above.</td></tr>
				<?php else: foreach ($masters as $m): ?>
					<tr>
						<td><strong><?php echo (int) $m['customer_id']; ?></strong></td>
						<td><?php echo epc_erp_h((string) ($m['customer_account'] ?? '')); ?></td>
						<td><?php echo epc_erp_h((string) ($m['customer_name'] ?? '')); ?></td>
						<td><?php echo epc_erp_h((string) ($m['customer_group'] ?? '')); ?></td>
						<td><?php echo epc_erp_h((string) ($m['currency_code'] ?? '')); ?></td>
						<td><?php echo number_format((float) ($m['credit_limit'] ?? 0), 2); ?></td>
						<td><?php echo (int) ($m['terms_days'] ?? 0); ?>d</td>
						<td><?php echo !empty($m['on_hold']) ? '<span class="label label-warning">Hold</span>' : '—'; ?></td>
						<td><?php echo epc_erp_h((string) ($m['trn'] ?? '')); ?></td>
					</tr>
				<?php endforeach; endif; ?>
				</tbody>
			</table>
		</div>
		<script>
		(function(){
			var url = <?php echo json_encode(isset($erpAjaxEndpoint) ? $erpAjaxEndpoint : ('/' . (isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp') . '/content/shop/finance/erp/ajax_erp_endpoint.php')); ?>;
			function post(action, fd){ fd.append('action', action); return fetch(url,{method:'POST',body:fd,credentials:'same-origin'}).then(function(r){return r.json();}); }
			function msg(j){ var el=document.getElementById('epc_erp_msg'); if(el){ el.className='alert alert-'+(j.status?'success':'danger'); el.textContent=j.message||''; el.style.display='block'; el.scrollIntoView({behavior:'smooth',block:'center'}); } if(j.status) setTimeout(function(){ location.reload(); }, 800); }
			var f=document.getElementById('epc_erp_customer_master');
			if(f) f.addEventListener('submit', function(e){ e.preventDefault(); post('customer_master_save', new FormData(f)).then(msg); });
		})();
		</script>
		<?php
		break;
	case 'terms':
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_pay_terms', 'Terms of payment',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'NET30'),
				array('name' => 'name', 'label' => 'Name', 'required' => true),
				array('name' => 'net_days', 'label' => 'Net days', 'type' => 'number'),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name'), array('key' => 'net_days', 'label' => 'Net days')),
			'fa-calendar');
		break;
	case 'groups':
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_customer_groups', 'Customer groups',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'WHOLESALE'),
				array('name' => 'name', 'label' => 'Name', 'required' => true),
				array('name' => 'terms_id', 'label' => 'Default terms', 'type' => 'select', 'options' => $termOpts),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name')),
			'fa-users');
		break;
	case 'journals':
		echo '<div class="epc-erp-section"><h4><i class="fa fa-book"></i> Customer journals</h4>';
		echo '<p class="text-muted">Post and review customer invoices and receipts:</p><div class="btn-group" style="flex-wrap:wrap;">';
		echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'sales_orders', $date_from_str, $date_to_str, 'sales')) . '"><i class="fa fa-file-text-o"></i> Customer invoice journal (sales invoices)</a> ';
		echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'receivables', $date_from_str, $date_to_str, 'sales')) . '"><i class="fa fa-money"></i> Customer payment journal</a> ';
		echo '<a class="btn btn-default btn-sm" href="' . epc_erp_h(epc_erp_tab_url($erpUrl, 'aging', $date_from_str, $date_to_str, 'finance')) . '&amp;aging_view=ar"><i class="fa fa-hourglass-half"></i> Receivables aging</a>';
		echo '</div></div>';
		break;
	case 'methods':
	default:
		epc_erp_pm_section($db_link, $csrf, 'epc_erp_pm_pay_methods', 'Methods of payment',
			array(
				array('name' => 'code', 'label' => 'Code', 'required' => true, 'placeholder' => 'BANK'),
				array('name' => 'name', 'label' => 'Name', 'required' => true),
				array('name' => 'method_type', 'label' => 'Type', 'type' => 'select', 'options' => array('cash' => 'Cash', 'bank' => 'Bank transfer', 'cheque' => 'Cheque', 'card' => 'Card', 'online' => 'Online')),
				array('name' => 'account_code', 'label' => 'GL account'),
				array('name' => 'note', 'label' => 'Note'),
			),
			array(array('key' => 'code', 'label' => 'Code'), array('key' => 'name', 'label' => 'Name'), array('key' => 'method_type', 'label' => 'Type'), array('key' => 'account_code', 'label' => 'GL acct')),
			'fa-credit-card');
		break;
}
