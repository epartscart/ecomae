<?php
/**
 * Storefront: search warehouse products by custom fields
 * (engine code, country, size, cross reference, OE, other…).
 *
 * Route: /shop/warehouse-search?field=engine_code&q=2JZGE
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_price_extra_fields.php';

$langHref = '/';
if (isset($multilang_params) && is_array($multilang_params) && !empty($multilang_params['lang_href'])) {
	$langHref = rtrim((string) $multilang_params['lang_href'], '/') . '/';
} elseif (isset($DP_Content) && is_object($DP_Content) && !empty($DP_Content->lang_href)) {
	$langHref = rtrim((string) $DP_Content->lang_href, '/') . '/';
}

$field = isset($_GET['field']) ? trim((string) $_GET['field']) : 'all';
$q = isset($_GET['q']) ? trim((string) $_GET['q']) : '';
if ($q === '' && isset($_GET['query'])) {
	$q = trim((string) $_GET['query']);
}

$options = epc_price_extra_search_options();
$validKeys = array();
foreach ($options as $opt) {
	$validKeys[$opt['key']] = true;
}
if ($field === '' || (!isset($validKeys[$field]) && !preg_match('/^[a-z0-9_]{1,48}$/', $field))) {
	$field = 'all';
}

$result = array('ok' => true, 'message' => '', 'count' => 0, 'items' => array(), 'field' => $field, 'q' => $q);
if ($q !== '') {
	$result = epc_price_attr_search($db_link, $field, $q, 100);
}

$partSearchBase = $langHref . 'shop/part_search';
$selfUrl = $langHref . 'shop/warehouse-search';
$fieldLabel = ($field === 'all') ? 'All fields' : epc_price_extra_label($field);
?>
<style>
.epc-wh-attr {
	max-width: 1100px;
	margin: 0 auto 40px;
	padding: 8px 12px 24px;
}
.epc-wh-attr__head h1 {
	font-size: 26px;
	font-weight: 800;
	margin: 8px 0 6px;
	color: #0f172a;
}
.epc-wh-attr__head p {
	color: #64748b;
	margin: 0 0 18px;
	max-width: 62ch;
}
.epc-wh-attr__form {
	display: flex;
	flex-wrap: wrap;
	gap: 10px;
	align-items: stretch;
	background: linear-gradient(180deg, #f8fafc 0%, #fff 100%);
	border: 1px solid #e2e8f0;
	border-radius: 12px;
	padding: 14px;
	margin-bottom: 22px;
}
.epc-wh-attr__form label {
	display: flex;
	flex-direction: column;
	gap: 4px;
	font-size: 12px;
	font-weight: 700;
	color: #475569;
	margin: 0;
}
.epc-wh-attr__form select,
.epc-wh-attr__form input[type="search"] {
	min-height: 42px;
	border: 1px solid #cbd5e1;
	border-radius: 8px;
	padding: 8px 12px;
	font-size: 14px;
	min-width: 180px;
}
.epc-wh-attr__form .epc-wh-attr__q {
	flex: 1 1 220px;
}
.epc-wh-attr__form .epc-wh-attr__q input {
	width: 100%;
	min-width: 0;
}
.epc-wh-attr__form button {
	align-self: flex-end;
	min-height: 42px;
	border: 0;
	border-radius: 8px;
	background: #ef4444;
	color: #fff;
	font-weight: 800;
	padding: 0 18px;
}
.epc-wh-attr__meta {
	color: #64748b;
	font-size: 13px;
	margin: 0 0 12px;
}
.epc-wh-attr__table {
	width: 100%;
	border-collapse: collapse;
	background: #fff;
}
.epc-wh-attr__table th,
.epc-wh-attr__table td {
	border-bottom: 1px solid #e2e8f0;
	padding: 10px 12px;
	text-align: left;
	vertical-align: top;
	font-size: 13px;
}
.epc-wh-attr__table th {
	background: #f8fafc;
	color: #475569;
	font-size: 11px;
	letter-spacing: .04em;
	text-transform: uppercase;
}
.epc-wh-attr__table a {
	color: #0f172a;
	font-weight: 800;
	text-decoration: none;
}
.epc-wh-attr__table a:hover {
	color: #ef4444;
}
.epc-wh-attr__empty {
	padding: 28px 16px;
	border: 1px dashed #cbd5e1;
	border-radius: 12px;
	color: #64748b;
	text-align: center;
}
.epc-wh-attr__chip {
	display: inline-block;
	background: #f1f5f9;
	border-radius: 6px;
	padding: 2px 8px;
	font-size: 12px;
	font-weight: 700;
	color: #334155;
}
@media (max-width: 720px) {
	.epc-wh-attr__form {
		flex-direction: column;
	}
	.epc-wh-attr__form button {
		align-self: stretch;
	}
	.epc-wh-attr__table th:nth-child(4),
	.epc-wh-attr__table td:nth-child(4) {
		display: none;
	}
}
</style>

<div class="epc-wh-attr">
	<div class="epc-wh-attr__head">
		<h1>Warehouse product search</h1>
		<p>Find parts by engine code, country code, size, cross reference, OE number, and other product information from multivendor warehouse data.</p>
	</div>

	<form class="epc-wh-attr__form" method="GET" action="<?php echo htmlspecialchars($selfUrl, ENT_QUOTES, 'UTF-8'); ?>">
		<label>
			Search in
			<select name="field">
				<?php foreach ($options as $opt) { ?>
				<option value="<?php echo htmlspecialchars($opt['key'], ENT_QUOTES, 'UTF-8'); ?>"<?php echo ($field === $opt['key']) ? ' selected' : ''; ?>>
					<?php echo htmlspecialchars($opt['label'], ENT_QUOTES, 'UTF-8'); ?>
				</option>
				<?php } ?>
			</select>
		</label>
		<label class="epc-wh-attr__q">
			Value
			<input type="search" name="q" value="<?php echo htmlspecialchars($q, ENT_QUOTES, 'UTF-8'); ?>" placeholder="e.g. 2JZGE, JP, 15&quot;, 04465…" maxlength="120" autocomplete="off" required />
		</label>
		<button type="submit"><i class="fa fa-search" aria-hidden="true"></i> Search</button>
	</form>

	<?php if ($q === '') { ?>
		<div class="epc-wh-attr__empty">Choose a field and enter a value to search warehouse products.</div>
	<?php } elseif (empty($result['ok']) && (string) ($result['message'] ?? '') !== '') { ?>
		<div class="epc-wh-attr__empty"><?php echo htmlspecialchars((string) $result['message'], ENT_QUOTES, 'UTF-8'); ?></div>
	<?php } elseif ((int) ($result['count'] ?? 0) === 0) { ?>
		<div class="epc-wh-attr__empty">No products matched <strong><?php echo htmlspecialchars($fieldLabel, ENT_QUOTES, 'UTF-8'); ?></strong> for “<?php echo htmlspecialchars($q, ENT_QUOTES, 'UTF-8'); ?>”.</div>
	<?php } else { ?>
		<p class="epc-wh-attr__meta">
			<?php echo (int) $result['count']; ?> product<?php echo ((int) $result['count'] === 1) ? '' : 's'; ?>
			for <strong><?php echo htmlspecialchars($fieldLabel, ENT_QUOTES, 'UTF-8'); ?></strong>
			= “<?php echo htmlspecialchars($q, ENT_QUOTES, 'UTF-8'); ?>”. Click a part number to open price search.
		</p>
		<div class="table-responsive">
			<table class="epc-wh-attr__table">
				<thead>
					<tr>
						<th>Brand</th>
						<th>Article</th>
						<th>Name</th>
						<th>Matched</th>
						<th></th>
					</tr>
				</thead>
				<tbody>
				<?php foreach ($result['items'] as $item) {
					$mfr = (string) ($item['manufacturer'] ?? '');
					$artShow = (string) ($item['article_show'] ?? $item['article'] ?? '');
					$name = (string) ($item['name'] ?? '');
					$href = $partSearchBase . '?article=' . rawurlencode($artShow);
					if ($mfr !== '') {
						$href .= '&brend=' . rawurlencode($mfr);
					}
					$matchLbl = (string) ($item['matched_field_label'] ?? '');
					$matchVal = (string) ($item['matched_value'] ?? '');
					?>
					<tr>
						<td><?php echo htmlspecialchars($mfr !== '' ? $mfr : '—', ENT_QUOTES, 'UTF-8'); ?></td>
						<td><a href="<?php echo htmlspecialchars($href, ENT_QUOTES, 'UTF-8'); ?>"><?php echo htmlspecialchars($artShow, ENT_QUOTES, 'UTF-8'); ?></a></td>
						<td><?php echo htmlspecialchars($name !== '' ? $name : '—', ENT_QUOTES, 'UTF-8'); ?></td>
						<td>
							<span class="epc-wh-attr__chip"><?php echo htmlspecialchars($matchLbl, ENT_QUOTES, 'UTF-8'); ?></span>
							<div><?php echo htmlspecialchars($matchVal, ENT_QUOTES, 'UTF-8'); ?></div>
						</td>
						<td><a href="<?php echo htmlspecialchars($href, ENT_QUOTES, 'UTF-8'); ?>">View prices</a></td>
					</tr>
				<?php } ?>
				</tbody>
			</table>
		</div>
	<?php } ?>
</div>
