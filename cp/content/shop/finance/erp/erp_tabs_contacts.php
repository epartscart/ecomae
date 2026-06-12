<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_phase8.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_tax_toolkit.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/epc_countries.php';

$partyFilter = isset($_GET['party']) ? (string)$_GET['party'] : '';
$qSearch = isset($_GET['q']) ? trim((string)$_GET['q']) : '';
$contacts = epc_erp_contacts_list($db_link, $partyFilter, $qSearch);
$countryOptions = function_exists('epc_countries_iso3166_alpha2') ? epc_countries_iso3166_alpha2() : array('AE' => 'United Arab Emirates', 'OM' => 'Oman', 'SA' => 'Saudi Arabia', 'IN' => 'India', 'PK' => 'Pakistan', 'GB' => 'United Kingdom', 'US' => 'United States');

erp_page_header(
	'<i class="fa fa-address-book-o"></i> Contacts &amp; third parties',
	'Unified address book — customers, suppliers, and linked shop users.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Contacts'),
	),
	array(
		array('label' => 'Sync from masters', 'id' => 'epc_erp_sync_contacts', 'class' => 'btn-default', 'icon' => 'fa-refresh'),
	)
);
erp_filter_bar($erpUrl, 'contacts', $date_from_str, $date_to_str,
	'<label>Type</label> <select name="party" class="form-control input-sm"><option value="">All</option>'
	. '<option value="customer"' . ($partyFilter === 'customer' ? ' selected' : '') . '>Customers</option>'
	. '<option value="supplier"' . ($partyFilter === 'supplier' ? ' selected' : '') . '>Suppliers</option></select>'
	. ' <label>Search</label> <input type="text" name="q" class="form-control input-sm" value="' . epc_erp_h($qSearch) . '" placeholder="Name, email, phone">'
);
erp_stat_cards(array(
	array('label' => 'Total contacts', 'value' => (string)count($contacts)),
));
ob_start();
if (empty($contacts)) {
	erp_empty_state('No contacts yet. Add one below or sync from suppliers and customers.');
} else {
	erp_table_open(array('Name', 'Type', 'Company', 'Country', 'Tax kit', 'Email', 'Phone', 'TRN', 'Links'));
	foreach ($contacts as $c) {
		$taxProf = epc_tax_toolkit_get_profile($db_link, (int)$c['linked_user_id'], (int)$c['id']);
		$kitLabel = $taxProf ? ($taxProf['kit_code'] ?? '—') : epc_tax_toolkit_country_to_kit_code((string)($c['country_code'] ?? 'AE'));
		echo '<tr><td><strong>' . epc_erp_h($c['name']) . '</strong></td>';
		echo '<td>' . epc_erp_h($c['party_type']) . '</td>';
		echo '<td>' . epc_erp_h($c['company'] ?: '—') . '</td>';
		echo '<td>' . epc_erp_h($c['country_code'] ?: 'AE') . '</td>';
		echo '<td><code style="font-size:11px">' . epc_erp_h($kitLabel) . '</code></td>';
		echo '<td>' . epc_erp_h($c['email'] ?: '—') . '</td>';
		echo '<td>' . epc_erp_h($c['phone'] ?: '—') . '</td>';
		echo '<td>' . epc_erp_h($c['trn'] ?: '—') . '</td><td>';
		if ((int)$c['linked_user_id'] > 0) {
			echo 'User #' . (int)$c['linked_user_id'] . ' ';
		}
		if ((int)$c['linked_supplier_id'] > 0) {
			echo 'Supplier #' . (int)$c['linked_supplier_id'];
		}
		echo '</td></tr>';
	}
	erp_table_close();
}
$listHtml = ob_get_clean();
erp_section_card('Address book', $listHtml, array('icon' => 'fa-list'));
ob_start();
?>
<form id="epc_erp_form_contact" class="form-horizontal" style="max-width:720px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Type</label><div class="col-sm-9">
		<select name="party_type" class="form-control input-sm">
			<option value="customer">Customer</option><option value="supplier">Supplier</option>
			<option value="both">Both</option><option value="other">Other</option>
		</select></div></div>
	<div class="form-group"><label class="col-sm-3">Name</label><div class="col-sm-9"><input name="name" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Company</label><div class="col-sm-9"><input name="company" class="form-control input-sm"></div></div>
	<div class="form-group"><label class="col-sm-3">Email / Phone</label><div class="col-sm-9 form-inline">
		<input name="email" type="email" class="form-control input-sm" placeholder="Email">
		<input name="phone" class="form-control input-sm" placeholder="Phone"></div></div>
	<div class="form-group"><label class="col-sm-3">TRN / Country / FX</label><div class="col-sm-9 form-inline">
		<input name="trn" class="form-control input-sm" placeholder="TRN">
		<select name="country_code" id="epc_erp_contact_country" class="form-control input-sm" style="width:140px;">
			<?php foreach ($countryOptions as $cc => $cname) { ?>
			<option value="<?php echo epc_erp_h($cc); ?>"<?php echo $cc === 'AE' ? ' selected' : ''; ?>><?php echo epc_erp_h($cc . ' — ' . $cname); ?></option>
			<?php } ?>
		</select>
		<input name="currency_code" class="form-control input-sm" value="AED" style="width:70px;" placeholder="CCY"></div></div>
	<div class="form-group"><label class="col-sm-3">Tax kit</label><div class="col-sm-9">
		<p class="form-control-static" id="epc_erp_contact_tax_kit_hint" style="margin:0;font-size:13px"><code>AE-UAE-VAT</code> — auto-assigned from country on save</p>
	</div></div>
	<div class="form-group"><label class="col-sm-3">Address</label><div class="col-sm-9"><textarea name="address" class="form-control input-sm" rows="2"></textarea></div></div>
	<div class="form-group"><label class="col-sm-3">Link user ID</label><div class="col-sm-9"><input name="linked_user_id" type="number" class="form-control input-sm" placeholder="Optional shop user"></div></div>
	<div class="form-group"><label class="col-sm-3">Link supplier ID</label><div class="col-sm-9"><input name="linked_supplier_id" type="number" class="form-control input-sm" placeholder="Optional ERP supplier"></div></div>
	<?php echo epc_erp_dim_render_fields($db_link); ?>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Save contact</button></div></div>
</form>
<?php
$formHtml = ob_get_clean();
erp_section_card('Add contact', $formHtml, array('icon' => 'fa-plus'));
?>
<script>
(function(){
	var map={AE:'AE-UAE-VAT',OM:'OM-OMAN-VAT',SA:'SA-KSA-VAT',IN:'IN-INDIA-GST',PK:'PK-PAKISTAN-GST',GB:'GB-UK-VAT',UK:'GB-UK-VAT',US:'US-SALES-TAX',DE:'EU-VAT-GENERIC',FR:'EU-VAT-GENERIC',IT:'EU-VAT-GENERIC',ES:'EU-VAT-GENERIC',NL:'EU-VAT-GENERIC'};
	var eu=['AT','BE','BG','HR','CY','CZ','DK','EE','FI','FR','DE','GR','HU','IE','IT','LV','LT','LU','MT','NL','PL','PT','RO','SK','SI','ES','SE'];
	eu.forEach(function(c){map[c]='EU-VAT-GENERIC';});
	var sel=document.getElementById('epc_erp_contact_country');
	var hint=document.getElementById('epc_erp_contact_tax_kit_hint');
	function refresh(){if(!sel||!hint)return;var cc=sel.value||'AE';var kit=map[cc]||'AE-UAE-VAT';hint.innerHTML='<code>'+kit+'</code> — auto-assigned from country on save';}
	if(sel){sel.addEventListener('change',refresh);refresh();}
})();
</script>
<?php
