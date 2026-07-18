<?php
/**
 * WhatsApp wa.me share helpers — Phase 1 (prefilled messages, no Business API).
 */
defined('_ASTEXE_') or die('No access');

if (!function_exists('epc_agent_whatsapp_href')) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_parts_agent.php';
}

function epc_wa_h($v): string
{
	return htmlspecialchars((string)$v, ENT_QUOTES, 'UTF-8');
}

function epc_wa_digits(?string $phone): string
{
	return preg_replace('/[^0-9]/', '', (string)$phone);
}

function epc_wa_sales_digits($DP_Config): string
{
	$href = epc_agent_whatsapp_href($DP_Config);
	if ($href !== '' && preg_match('#/([0-9]+)$#', $href, $m)) {
		return $m[1];
	}
	return '971567607011';
}

function epc_wa_sales_display($DP_Config): string
{
	if (is_object($DP_Config) && !empty($DP_Config->epc_whatsapp_number)) {
		return trim((string)$DP_Config->epc_whatsapp_number);
	}
	return '+971 56 760 7011';
}

function epc_wa_share_url(string $digits, string $text): string
{
	$digits = epc_wa_digits($digits);
	if ($digits === '') {
		return '';
	}
	return 'https://wa.me/' . $digits . '?text=' . rawurlencode($text);
}

function epc_wa_bilingual(string $en, string $ar): string
{
	$en = trim($en);
	$ar = trim($ar);
	if ($en === '') {
		return $ar;
	}
	if ($ar === '') {
		return $en;
	}
	return $en . "\n" . $ar;
}

function epc_wa_site_name($DP_Config): string
{
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_branding.php';
	$name = epc_brand_trade_name();
	if ($name !== '') {
		return $name;
	}
	return is_object($DP_Config) && !empty($DP_Config->from_name)
		? (string) $DP_Config->from_name : 'Store';
}

function epc_wa_order_items(PDO $db, int $orderId): array
{
	$st = $db->prepare(
		'SELECT `id`, `t2_manufacturer`, `t2_article`, `t2_article_show`, `t2_name`, `count_need`, `price`, `t2_storage_id`
		 FROM `shop_orders_items` WHERE `order_id` = ? ORDER BY `id` ASC LIMIT 40'
	);
	$st->execute(array($orderId));
	return $st->fetchAll(PDO::FETCH_ASSOC) ?: array();
}

function epc_wa_order_lines_text(array $items, int $max = 12): string
{
	$lines = array();
	foreach ($items as $item) {
		if (count($lines) >= $max) {
			$lines[] = '…';
			break;
		}
		$brand = trim((string)($item['t2_manufacturer'] ?? ''));
		$article = trim((string)($item['t2_article_show'] ?? $item['t2_article'] ?? ''));
		$name = trim((string)($item['t2_name'] ?? ''));
		$qty = (int)($item['count_need'] ?? 0);
		$line = $brand . ' ' . $article;
		if ($name !== '') {
			$line .= ' — ' . $name;
		}
		if ($qty > 0) {
			$line .= ' ×' . $qty;
		}
		$lines[] = trim($line);
	}
	return implode("\n", $lines);
}

function epc_wa_order_status_message($DP_Config, int $orderId, string $statusName, array $order = array(), array $items = array()): string
{
	$site = epc_wa_site_name($DP_Config);
	$domain = rtrim((string)$DP_Config->domain_path, '/');
	$lines = epc_wa_order_lines_text($items, 8);
	$en = "Hello from {$site}.\n\nOrder #{$orderId} update: {$statusName}.";
	if ($lines !== '') {
		$en .= "\n\nItems:\n{$lines}";
	}
	$en .= "\n\nTrack your order: {$domain}/shop/orders";
	$ar = "تحديث طلب #{$orderId} من {$site}: {$statusName}.";
	if ($lines !== '') {
		$ar .= "\n\n{$lines}";
	}
	$ar .= "\n\n{$domain}/shop/orders";
	return epc_wa_bilingual($en, $ar);
}

/**
 * Log WhatsApp tracking template on order status change (wa.me deep link — no Business API).
 */
function epc_wa_notify_order_status_change(PDO $db, $DP_Config, int $orderId, string $statusName, array $order = array()): void
{
	if ($orderId <= 0 || $statusName === '') {
		return;
	}
	$items = epc_wa_order_items($db, $orderId);
	$msg = epc_wa_order_status_message($DP_Config, $orderId, $statusName, $order, $items);
	$phone = '';
	if (!empty($order['phone_not_auth'])) {
		$phone = epc_wa_digits((string)$order['phone_not_auth']);
	} elseif (!empty($order['user_id']) && (int)$order['user_id'] > 0) {
		try {
			$st = $db->prepare('SELECT `phone` FROM `users` WHERE `user_id` = ? LIMIT 1;');
			$st->execute(array((int)$order['user_id']));
			$phone = epc_wa_digits((string)$st->fetchColumn());
		} catch (Throwable $e) {
		}
	}
	$target = $phone !== '' ? $phone : epc_wa_sales_digits($DP_Config);
	$href = epc_wa_share_url($target, $msg);
	$log = 'WhatsApp tracking template ready: ' . $statusName;
	if ($href !== '') {
		$log .= ' — ' . $href;
	}
	try {
		$db->prepare('INSERT INTO `shop_orders_logs` (`order_id`,`time`,`user_id`,`is_manager`,`text`,`is_robot`) VALUES (?,?,?,?,?,?);')
			->execute(array($orderId, time(), 0, 0, $log, 1));
	} catch (Throwable $e) {
	}
}

function epc_wa_order_customer_message($DP_Config, int $orderId, array $order, array $items): string
{
	$site = epc_wa_site_name($DP_Config);
	$sum = isset($order['price_sum']) ? number_format((float)$order['price_sum'], 2, '.', ',') : '';
	$lines = epc_wa_order_lines_text($items);
	$en = "Hello from {$site}.\n\nYour order #{$orderId}";
	if ($sum !== '') {
		$en .= " — total {$sum} AED";
	}
	$en .= ".\n\nItems:\n{$lines}\n\nReply here if you have questions.";
	$ar = "مرحباً من {$site}.\n\nطلبكم رقم #{$orderId}";
	if ($sum !== '') {
		$ar .= " — الإجمالي {$sum} درهم";
	}
	$ar .= ".\n\nالأصناف:\n{$lines}\n\nردّوا على هذه الرسالة لأي استفسار.";
	return epc_wa_bilingual($en, $ar);
}

function epc_wa_order_sales_message($DP_Config, int $orderId, array $order, array $items, string $context = 'staff'): string
{
	$site = epc_wa_site_name($DP_Config);
	$sum = isset($order['price_sum']) ? number_format((float)$order['price_sum'], 2, '.', ',') : '';
	$lines = epc_wa_order_lines_text($items);
	$domain = rtrim((string)$DP_Config->domain_path, '/');
	$link = $domain . '/' . $DP_Config->backend_dir . '/shop/orders/order?order_id=' . $orderId;
	$en = "[{$site}] Order #{$orderId} ({$context})";
	if ($sum !== '') {
		$en .= " — {$sum} AED";
	}
	$en .= "\n\n{$lines}\n\nCP: {$link}";
	$ar = "[{$site}] طلب #{$orderId}\n\n{$lines}";
	return epc_wa_bilingual($en, $ar);
}

function epc_wa_supplier_lpo_message($DP_Config, int $orderId, string $storageName, array $items): string
{
	$site = epc_wa_site_name($DP_Config);
	$lines = array();
	foreach ($items as $item) {
		$qty = (int)($item['count_need'] ?? 0);
		if ($qty <= 0) {
			continue;
		}
		$lines[] = trim(
			(string)($item['t2_manufacturer'] ?? '') . ' '
			. (string)($item['t2_article_show'] ?? $item['t2_article'] ?? '')
			. ' ×' . $qty
		);
	}
	$body = implode("\n", $lines);
	$en = "LPO #{$orderId} — {$storageName}\n\nPlease supply:\n{$body}\n\nReference LPO {$orderId} on invoice.\n— {$site}";
	$ar = "أمر شراء #{$orderId} — {$storageName}\n\n{$body}\n\nرقم LPO: {$orderId}";
	return epc_wa_bilingual($en, $ar);
}

function epc_wa_product_message($DP_Config, string $brand, string $article, string $name, $price = null): string
{
	$site = epc_wa_site_name($DP_Config);
	$domain = rtrim((string)$DP_Config->domain_path, '/');
	$en = "Hello {$site}, please quote:\n\n{$brand} {$article}";
	if ($name !== '') {
		$en .= "\n{$name}";
	}
	if ($price !== null && $price !== '') {
		$en .= "\nListed: {$price} AED";
	}
	$en .= "\n\n{$domain}";
	$ar = "السلام عليكم {$site}، أرجو تسعير:\n\n{$brand} {$article}";
	if ($name !== '') {
		$ar .= "\n{$name}";
	}
	return epc_wa_bilingual($en, $ar);
}

function epc_wa_cart_message($DP_Config, array $lines, $total = null): string
{
	$site = epc_wa_site_name($DP_Config);
	$domain = rtrim((string)$DP_Config->domain_path, '/');
	$body = implode("\n", array_slice($lines, 0, 15));
	$en = "Hello {$site}, please assist with my cart:\n\n{$body}";
	if ($total !== null && $total !== '') {
		$en .= "\n\nEstimated total: {$total} AED";
	}
	$en .= "\n\n{$domain}/shop/cart";
	$ar = "مرحباً {$site}، أرجو المساعدة في سلة التسوق:\n\n{$body}";
	return epc_wa_bilingual($en, $ar);
}

function epc_wa_supplier_phone_for_storage(PDO $db, int $storageId): string
{
	if ($storageId <= 0) {
		return '';
	}
	try {
		$st = $db->prepare('SELECT `contact_phone` FROM `epc_erp_suppliers` WHERE `storage_id` = ? LIMIT 1');
		$st->execute(array($storageId));
		$phone = epc_wa_digits((string)$st->fetchColumn());
		if ($phone !== '') {
			return $phone;
		}
	} catch (Throwable $e) {
	}
	return '';
}

function epc_wa_storage_name(PDO $db, int $storageId): string
{
	if ($storageId <= 0) {
		return 'Warehouse';
	}
	$st = $db->prepare('SELECT `name` FROM `shop_storages` WHERE `id` = ? LIMIT 1');
	$st->execute(array($storageId));
	$name = trim((string)$st->fetchColumn());
	return $name !== '' ? $name : ('Warehouse #' . $storageId);
}

/**
 * @return array<int, array{storage_id:int,storage_name:string,items:array,supplier_phone:string,lpo_message:string,wa_href:string}>
 */
function epc_wa_order_lpo_groups(PDO $db, $DP_Config, int $orderId, array $items): array
{
	if (!function_exists('epc_order_item_storage_id')) {
		require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/usefull/epc_supplier_notifications.php';
	}
	$groups = array();
	foreach ($items as $item) {
		$sid = function_exists('epc_order_item_storage_id')
			? (int)epc_order_item_storage_id($db, $item)
			: (int)($item['t2_storage_id'] ?? 0);
		if (!isset($groups[$sid])) {
			$groups[$sid] = array(
				'storage_id' => $sid,
				'storage_name' => epc_wa_storage_name($db, $sid),
				'items' => array(),
				'supplier_phone' => epc_wa_supplier_phone_for_storage($db, $sid),
			);
		}
		$groups[$sid]['items'][] = $item;
	}
	$sales = epc_wa_sales_digits($DP_Config);
	$out = array();
	foreach ($groups as $g) {
		$msg = epc_wa_supplier_lpo_message($DP_Config, $orderId, $g['storage_name'], $g['items']);
		$target = $g['supplier_phone'] !== '' ? $g['supplier_phone'] : $sales;
		$g['lpo_message'] = $msg;
		$g['wa_href'] = epc_wa_share_url($target, $msg);
		$g['target_label'] = $g['supplier_phone'] !== '' ? 'supplier' : 'sales (forward LPO)';
		$out[] = $g;
	}
	return $out;
}

function epc_wa_button(string $href, string $label, string $extraClass = '', string $title = ''): string
{
	if ($href === '') {
		return '';
	}
	$cls = 'btn btn-sm epc-wa-share-btn ' . trim($extraClass);
	$t = $title !== '' ? ' title="' . epc_wa_h($title) . '"' : '';
	return '<a class="' . epc_wa_h($cls) . '" href="' . epc_wa_h($href) . '" target="_blank" rel="noopener noreferrer"' . $t . '>'
		. '<i class="fa fa-whatsapp"></i> ' . epc_wa_h($label) . '</a>';
}

function epc_wa_styles(): string
{
	return '<style>'
		. '.epc-wa-share-btn{background:#25D366!important;border-color:#1da851!important;color:#fff!important;margin:2px 4px 2px 0;}'
		. '.epc-wa-share-btn:hover,.epc-wa-share-btn:focus{background:#1da851!important;color:#fff!important;}'
		. '.epc-wa-share-row{margin:8px 0 0;display:flex;flex-wrap:wrap;gap:6px;align-items:center;}'
		. '.epc-wa-share-panel{background:#f0fdf4;border:1px solid #bbf7d0;border-radius:8px;padding:12px 14px;margin:12px 0;}'
		. '.epc-wa-share-panel h5{margin:0 0 8px;font-size:14px;font-weight:700;color:#166534;}'
		. '.epc-wa-share-panel .text-muted{font-size:12px;margin-bottom:8px;}'
		. '.epc-product-actions .epc-wa-share-btn{padding:6px 10px;font-size:11px;text-decoration:none!important;display:inline-flex;align-items:center;gap:4px;white-space:nowrap;}'
		. '.epc-product-actions{display:inline-flex;flex-direction:row;flex-wrap:nowrap;gap:6px;align-items:center;justify-content:flex-end;}'
		. '.epc-product-actions__tools,.epc-product-actions__buy{display:inline-flex;flex-wrap:nowrap;gap:6px;align-items:center;justify-content:flex-start;width:auto;flex:0 0 auto;}'
		. '.epc-product-actions__tools .epc-wa-share-btn,.epc-product-actions__tools .epc-fitment-check-btn--row{flex:0 0 auto;}'
		. '#all_table_products .td_add_to_cart .epc-product-actions{flex-wrap:nowrap;}'
		. '#all_table_products .td_price .epc-price-value{display:block;font-weight:700;white-space:nowrap;line-height:1.3;}'
		. '</style>';
}

function epc_wa_frontend_script($DP_Config): string
{
	$sales = epc_wa_sales_digits($DP_Config);
	$display = epc_wa_sales_display($DP_Config);
	$site = epc_wa_site_name($DP_Config);
	$domain = rtrim((string)$DP_Config->domain_path, '/');
	ob_start();
	?>
<script>
window.epcWaShare = {
	sales: <?php echo json_encode($sales); ?>,
	salesDisplay: <?php echo json_encode($display); ?>,
	site: <?php echo json_encode($site); ?>,
	domain: <?php echo json_encode($domain); ?>
};
function epcWaOpen(digits, text) {
	digits = String(digits || '').replace(/\D/g, '');
	if (!digits) { return; }
	window.open('https://wa.me/' + digits + '?text=' + encodeURIComponent(text || ''), '_blank', 'noopener,noreferrer');
}
function epcWaBilingual(en, ar) {
	en = (en || '').trim();
	ar = (ar || '').trim();
	if (!en) { return ar; }
	if (!ar) { return en; }
	return en + '\n' + ar;
}
function epcWaShareProductHref(brand, article, name, price) {
	var s = window.epcWaShare || {};
	var digits = String(s.sales || '').replace(/\D/g, '');
	if (!digits) { return ''; }
	var en = 'Hello ' + (s.site || 'eParts Cart') + ', please quote:\n\n' + brand + ' ' + article;
	if (name) { en += '\n' + name; }
	if (price !== undefined && price !== null && price !== '') { en += '\nListed: ' + price + ' AED'; }
	en += '\n\n' + (s.domain || '');
	var ar = 'السلام عليكم، أرجو تسعير:\n\n' + brand + ' ' + article;
	if (name) { ar += '\n' + name; }
	return 'https://wa.me/' + digits + '?text=' + encodeURIComponent(epcWaBilingual(en, ar));
}
function epcWaShareProduct(brand, article, name, price) {
	var href = epcWaShareProductHref(brand, article, name, price);
	if (href) { window.open(href, '_blank', 'noopener,noreferrer'); }
}
function epcWaShareCart() {
	var s = window.epcWaShare || {};
	if (typeof cart_records === 'undefined' || !cart_records || !cart_records.length) { return; }
	var lines = [];
	var total = 0;
	for (var i = 0; i < cart_records.length && lines.length < 15; i++) {
		var r = cart_records[i];
		var line = (r.manufacturer || '') + ' ' + (r.article || '');
		if (r.name) { line += ' — ' + r.name; }
		if (r.count_need) { line += ' ×' + r.count_need; }
		lines.push(line.trim());
		if (r.price && r.count_need) { total += parseFloat(r.price) * parseInt(r.count_need, 10); }
	}
	var en = 'Hello ' + (s.site || 'eParts Cart') + ', please assist with my cart:\n\n' + lines.join('\n');
	if (total > 0) { en += '\n\nEstimated total: ' + total.toFixed(2) + ' AED'; }
	en += '\n\n' + (s.domain || '') + '/shop/cart';
	var ar = 'مرحباً، أرجو المساعدة في سلة التسوق:\n\n' + lines.join('\n');
	epcWaOpen(s.sales, epcWaBilingual(en, ar));
}
function epcWaShareBtnHTML(brand, article, name, price) {
	if (typeof epc_storefront_prices_visible !== 'undefined' && !epc_storefront_prices_visible) {
		return '';
	}
	var href = epcWaShareProductHref(brand, article, name, price);
	if (!href) { return ''; }
	var disp = (window.epcWaShare && window.epcWaShare.salesDisplay) ? window.epcWaShare.salesDisplay : '';
	var safeHref = href.replace(/&/g, '&amp;').replace(/"/g, '&quot;');
	var safeDisp = String(disp).replace(/"/g, '&quot;');
	return '<a class="btn btn-sm epc-wa-share-btn" href="' + safeHref + '" target="_blank" rel="noopener noreferrer" title="WhatsApp ' + safeDisp + '">'
		+ '<i class="fa fa-whatsapp"></i> WhatsApp</a>';
}
</script>
	<?php
	return ob_get_clean();
}
