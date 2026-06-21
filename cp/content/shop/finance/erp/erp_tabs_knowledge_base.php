<?php
defined('_ASTEXE_') or die('No access');
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_extended.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_erp_ui.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/finance/epc_uae_tax_knowledge.php';
epc_uae_tax_knowledge_seed_kb($db_link);

$cat = isset($_GET['kb_cat']) ? (string) $_GET['kb_cat'] : '';
$articles = epc_erp_kb_list($db_link, $cat);

erp_page_header(
	'<i class="fa fa-book"></i> Knowledge base',
	'Internal articles for tenant staff — processes, policies, and how-tos.',
	array(
		array('label' => 'ERP', 'url' => epc_erp_tab_url($erpUrl, 'dashboard', $date_from_str, $date_to_str)),
		array('label' => 'Knowledge base'),
	)
);
erp_filter_bar($erpUrl, 'knowledge_base', $date_from_str, $date_to_str,
	'<label>Category</label> <select name="kb_cat" class="form-control input-sm"><option value="">All</option>'
	. '<option value="finance">Finance</option><option value="sales">Sales</option><option value="operations">Operations</option><option value="general">General</option></select>'
);
ob_start();
if (empty($articles)) {
	erp_empty_state('No articles yet. Add internal documentation below.', 'fa-book');
} else {
	echo '<div class="epc-erp-report-grid">';
	foreach ($articles as $a) {
		echo '<div class="epc-erp-report-tile"><h5>' . epc_erp_h($a['title']) . '</h5>';
		echo '<p class="text-muted"><span class="label label-default">' . epc_erp_h($a['category']) . '</span></p>';
		echo '<p>' . epc_erp_h($a['summary'] ?: '') . '</p></div>';
	}
	echo '</div>';
}
erp_section_card('Articles', ob_get_clean(), array('icon' => 'fa-th-large'));
ob_start();
?>
<form id="epc_erp_form_kb" class="form-horizontal" style="max-width:720px;">
	<input type="hidden" name="csrf_guard_key" value="<?php echo epc_erp_h($csrf); ?>">
	<div class="form-group"><label class="col-sm-3">Title</label><div class="col-sm-9"><input name="title" class="form-control input-sm" required></div></div>
	<div class="form-group"><label class="col-sm-3">Category / summary</label><div class="col-sm-9 form-inline">
		<select name="category" class="form-control input-sm"><option value="general">General</option><option value="finance">Finance</option><option value="sales">Sales</option><option value="operations">Operations</option></select>
		<input name="summary" class="form-control input-sm" placeholder="Short summary"></div></div>
	<div class="form-group"><label class="col-sm-3">Body</label><div class="col-sm-9"><textarea name="body_html" class="form-control input-sm" rows="4"></textarea></div></div>
	<div class="form-group"><div class="col-sm-offset-3 col-sm-9"><button type="submit" class="btn btn-primary btn-sm">Publish article</button></div></div>
</form>
<?php
erp_section_card('New article', ob_get_clean(), array('icon' => 'fa-plus'));
