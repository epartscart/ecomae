<?php
/**
 * Topic-related brochure photos — unique assignment, no repeats.
 * Strict keyword → topic; each process gets one exclusive related image.
 */
defined('_ASTEXE_') or die('No access');

/**
 * @return array<string, array{label:string,queries:list<string>,photos:list<string>}>
 */
function epc_cp_brochure_topic_catalog(): array
{
	$u = static function (string $id): string {
		return 'https://images.unsplash.com/' . $id . '?auto=format&fit=crop&w=960&h=540&q=80';
	};

	// Verified Unsplash IDs only. Each ID appears in exactly ONE topic.
	return array(
		'autoparts' => array(
			'label' => 'Auto parts',
			'queries' => array(
				'laximo', 'tecdoc', 'oem', 'aftermarket', 'spare part', 'auto part',
				'car part', 'cross-ref', 'cross ref', 'vin decode', 'vin lookup',
				'brake', 'filter', 'engine part', 'garage', 'mechanic', 'workshop',
				'vehicle catalog', 'make model', 'chassis', 'accessor',
			),
			'photos' => array(
				$u('photo-1486262715619-67b85e0b08d3'),
				$u('photo-1492144534655-ae79c964c9d7'),
				$u('photo-1503376780353-7e6692767b70'),
				$u('photo-1619642751034-765df4ed3f09'),
				$u('photo-1487754180451-c456f719a1fc'),
				$u('photo-1493238792370-ee1c630f8e27'),
				$u('photo-1549317661-bd32c8ce0db2'),
				$u('photo-1552519507-da3b142c6e3d'),
			),
		),
		'inventory' => array(
			'label' => 'Inventory & warehouse',
			'queries' => array(
				'inventor', 'warehouse', 'storage', 'stock level', 'stock ',
				'bin loc', 'shelving', 'shelf', 'replenish', 'wms', 'pallet',
				'price list', 'pricelist', 'price upload', 'upload price',
				'nomenclature', 'assortment', 'sku', 'barcode', 'qr code',
			),
			'photos' => array(
				$u('photo-1553413077-190dd305871c'),
				$u('photo-1586528116311-ad8dd3c8310d'),
				$u('photo-1566576721346-d4a3b4eaeb55'),
				$u('photo-1578575437130-527eed3abbec'),
				$u('photo-1558618666-fcd25c85f82e'),
				$u('photo-1581091226825-a6a2a5aee158'),
				$u('photo-1581092160562-40aa08e78837'),
				$u('photo-1504328343380-e939cecc7d7b'),
			),
		),
		'money' => array(
			'label' => 'Money & currency',
			'queries' => array(
				'currenc', 'money', 'cash', 'banknote', 'treasury', 'ledger',
				'finance', 'invoice', 'billing', 'payment', 'gateway', 'stripe',
				'markup', 'discount', 'vat', 'tax', 'gst', 'payroll', 'salary',
				'receivable', 'payable', 'bank ', 'credit card', 'accounting',
				'settlement', 'commission', 'exchange rate', 'wallet',
			),
			'photos' => array(
				$u('photo-1554224155-6726b3ff858f'),
				$u('photo-1579621970563-ebda9807a8a3'),
				$u('photo-1556742049-0cfed4f6a45d'),
				$u('photo-1633155585460-bab0b5eb0f1b'),
				$u('photo-1560472354-b33ff0c44a43'),
				$u('photo-1553729459-efe9148caef8'),
				$u('photo-1611974789855-9c2a0a7236a3'),
				$u('photo-1526304640581-d334cdbbf45e'),
			),
		),
		'orders' => array(
			'label' => 'Orders & fulfilment',
			'queries' => array(
				'order', 'oms', 'cart', 'checkout', 'fulfilment', 'fulfilment',
				'quotation', 'quote', 'rfq', 'sales desk', 'retailer', 'pos terminal',
			),
			'photos' => array(
				$u('photo-1556742044-3c52d6e88c62'),
				$u('photo-1472851294608-062f824d29a9'),
				$u('photo-1607083206869-4c7672e72a8a'),
				$u('photo-1556742111-a301076d9d18'),
				$u('photo-1441986300917-64674bd600d8'),
				$u('photo-1563013544-824ae1b704d3'),
				$u('photo-1483985988355-763728e1935b'),
				$u('photo-1556740758-90de374c12ad'),
			),
		),
		'logistics' => array(
			'label' => 'Logistics & shipping',
			'queries' => array(
				'logistic', 'shipping', 'courier', 'carrier', 'truck', 'delivery',
				'freight', 'dispatch', 'tracking', 'parcel', 'last mile', 'fleet',
			),
			'photos' => array(
				$u('photo-1601584115197-04ecc0da31d7'),
				$u('photo-1519003722824-194d4455a60c'),
				$u('photo-1566576912321-d58ddd7a6088'),
				$u('photo-1494412574643-ff11b0a5c1c3'),
				$u('photo-1587293855441-ad85a7efed90'),
				$u('photo-1616401784845-5806697d3da0'),
			),
		),
		'customers' => array(
			'label' => 'Customers & CRM',
			'queries' => array(
				'customer', 'client', 'crm', 'contact', 'lead', 'prospect',
				'loyalty', 'helpdesk', 'service desk', 'account manager',
			),
			'photos' => array(
				$u('photo-1521737711867-e3b97375f902'),
				$u('photo-1600880292203-757bb62b4baf'),
				$u('photo-1556761175-5973dc0f32e7'),
				$u('photo-1573164713714-d95e436ab8d6'),
				$u('photo-1552664730-d307ca884978'),
				$u('photo-1573496359142-b8d87734a5a2'),
				$u('photo-1560250097-0b93528c311a'),
				$u('photo-1542744173-8e7e53415bb0'),
			),
		),
		'ai' => array(
			'label' => 'AI & assistants',
			'queries' => array(
				'chatbot', 'copilot', 'openai', 'gpt', 'assistant',
				'machine learning', 'neural', 'parts expert', 'ai agent',
			),
			'photos' => array(
				$u('photo-1677442136019-21780ecad995'),
				$u('photo-1620712943543-bcc4688e7485'),
				$u('photo-1485827404703-89b55fcc595e'),
				$u('photo-1535378917042-10a22c959096'),
				$u('photo-1555255707-c07966088b7b'),
				$u('photo-1526374965328-7f61d4dc18c5'),
			),
		),
		'marketing' => array(
			'label' => 'Marketing & growth',
			'queries' => array(
				'marketing', 'broadcast', 'newsletter', 'campaign', 'promo',
				'whatsapp', 'social hub', 'facebook', 'instagram',
				'notification', 'email blast', 'ads manager',
			),
			'photos' => array(
				$u('photo-1432888622747-4eb9a8efeb07'),
				$u('photo-1460925895917-afdab827c52f'),
				$u('photo-1557804506-669a67965ba0'),
				$u('photo-1611162617474-5b21e879e113'),
				$u('photo-1533750349088-cd871a87b4a4'),
				$u('photo-1516321318423-f06f85e504b3'),
				$u('photo-1557200134-90327eea07b4'),
				$u('photo-1611224923853-80b023f02d71'),
			),
		),
		'documents' => array(
			'label' => 'Documents & print',
			'queries' => array(
				'document', 'pdf', 'print pack', 'contract', 'export report',
				'folder', 'archive', 'grn', 'rma', 'bos', 'blockchain proof',
				'attachment', 'file library',
			),
			'photos' => array(
				$u('photo-1450101499163-c8848c66ca85'),
				$u('photo-1586281380349-632531db7ed4'),
				$u('photo-1554224154-26032ffc0d07'),
				$u('photo-1507925921958-8a62f3d1a50d'),
				$u('photo-1568667256549-094345857637'),
			),
		),
		'platform' => array(
			'label' => 'Platform & cloud',
			'queries' => array(
				'platform', 'tenant', 'super cp', 'boc', 'cloud', 'server',
				'host', 'deploy', 'failover', 'governance', 'audit', 'webhook',
				'industry pack', 'template pack', 'demo site',
			),
			'photos' => array(
				$u('photo-1451187580459-43490279c0fa'),
				$u('photo-1558494949-ef010cbdcc31'),
				$u('photo-1544197150-b99a5804f8d5'),
				$u('photo-1518770660439-4636190af475'),
			),
		),
		'settings' => array(
			'label' => 'Settings & security',
			'queries' => array(
				'setting', 'config', 'permission', 'security', 'auth', 'mfa',
				'password', 'smtp', 'role ', 'acl', 'sso', 'timezone',
			),
			'photos' => array(
				$u('photo-1555949963-aa79dcee981c'),
				$u('photo-1563986768609-322da13575f3'),
				$u('photo-1510511459019-5ddd3729f54a'),
				$u('photo-1639322537228-f710d846310a'),
			),
		),
		'erp' => array(
			'label' => 'ERP modules',
			'queries' => array(
				'erp', 'fixed asset', 'budget', 'period close', 'chart of account',
				'coa', 'general ledger', 'trial balance', 'journal',
			),
			'photos' => array(
				$u('photo-1454165804606-c3d57bc86b40'),
				$u('photo-1554224155-8d04cb21cd6c'),
				$u('photo-1507679799987-c73779587ccf'),
				$u('photo-1551288049-bebda4e38f71'),
				$u('photo-1543286386-713bdd548793'),
			),
		),
		'procurement' => array(
			'label' => 'Procurement & suppliers',
			'queries' => array(
				'procure', 'supplier', 'vendor', 'purchase order', 'buying',
				'sourcing', 'manufacturer', 'distributor',
			),
			'photos' => array(
				$u('photo-1504917595217-d4dc5ebe6122'),
				$u('photo-1581092918056-0c4c3acd3789'),
				$u('photo-1621905251189-08b45d6a269e'),
				$u('photo-1513828583688-c52646db42da'),
			),
		),
		'content' => array(
			'label' => 'Content & CMS',
			'queries' => array(
				'cms', 'content', 'page builder', 'block', 'editor', 'storefront',
				'banner', 'menu item', 'seo page', 'visual editor',
			),
			'photos' => array(
				$u('photo-1499951360447-b19be8fe80f5'),
				$u('photo-1467232004584-a241de8bcf5d'),
				$u('photo-1547658719-da2b51169166'),
				$u('photo-1504639725590-34d0984388bd'),
			),
		),
		'default' => array(
			'label' => 'Operations',
			'queries' => array(),
			'photos' => array(
				$u('photo-1522071820081-009f0129c71c'),
				$u('photo-1556761175-b413da4baf72'),
				$u('photo-1551836022-d5d88e9218df'),
				$u('photo-1517245386807-bb43f82c33c4'),
			),
		),
	);
}

/**
 * Unique topic illustration URL (always related; unique via seed).
 */
function epc_cp_brochure_topic_svg_url(string $topic, string $seed, string $title = '', string $area = ''): string
{
	$topic = preg_replace('/[^a-z]/', '', strtolower($topic)) ?: 'platform';
	$q = array(
		'topic' => $topic,
		'seed' => preg_replace('/[^a-zA-Z0-9_\-]/', '', $seed) ?: 'x',
	);
	if ($title !== '') {
		$q['t'] = $title;
	}
	if ($area !== '') {
		$q['a'] = $area;
	}
	return '/content/general_pages/epc_brochure_process_photo.php?' . http_build_query($q);
}

/**
 * Resolve topic from process name / url / description first, then area.
 */
function epc_cp_brochure_resolve_topic(string $name, string $area = '', string $url = '', string $does = ''): string
{
	$norm = static function (string $s): string {
		$s = strtolower(trim($s));
		$s = preg_replace('/[^a-z0-9\/\s_\-]+/', ' ', $s) ?? $s;
		return ' ' . trim(preg_replace('/\s+/', ' ', $s) ?? $s) . ' ';
	};

	$order = array(
		'autoparts', 'inventory', 'money', 'orders', 'logistics', 'procurement',
		'ai', 'marketing', 'documents', 'customers', 'erp', 'content', 'platform', 'settings',
	);
	$catalog = epc_cp_brochure_topic_catalog();

	$matchBest = static function (string $hay) use ($order, $catalog): string {
		if (trim($hay) === '') {
			return '';
		}
		$best = '';
		$bestLen = 0;
		foreach ($order as $topic) {
			foreach (($catalog[$topic]['queries'] ?? array()) as $q) {
				$q = strtolower(trim((string) $q));
				if ($q === '' || strlen($q) < 3) {
					continue;
				}
				if (strpos($hay, $q) !== false && strlen($q) > $bestLen) {
					$best = $topic;
					$bestLen = strlen($q);
				}
			}
		}
		return $best;
	};

	// 1) Name first — process title wins over long marketing blurbs in "does".
	$nameHay = $norm($name);
	if (preg_match('/\bai\b/', $nameHay)) {
		return 'ai';
	}
	$hit = $matchBest($nameHay);
	if ($hit !== '') {
		return $hit;
	}
	// 2) Name + URL
	$hit = $matchBest($norm($name . ' ' . $url));
	if ($hit !== '') {
		return $hit;
	}
	// 3) Full text including description
	$hay = $norm($name . ' ' . $url . ' ' . $does);
	$hit = $matchBest($hay);
	if ($hit !== '') {
		return $hit;
	}
	if (preg_match('/\bai\b/', $hay)) {
		return 'ai';
	}

	$areaL = $norm($area);
	if (strpos($areaL, 'logistic') !== false) {
		return 'logistics';
	}
	if (strpos($areaL, 'oms') !== false || strpos($areaL, 'shop') !== false) {
		return 'orders';
	}
	if (strpos($areaL, 'payment') !== false || strpos($areaL, 'finance') !== false || strpos($areaL, 'tax') !== false) {
		return 'money';
	}
	if (strpos($areaL, 'price') !== false || strpos($areaL, 'catalog') !== false) {
		return 'inventory';
	}
	if (strpos($areaL, 'customer') !== false || strpos($areaL, 'crm') !== false) {
		return 'customers';
	}
	if (preg_match('/\bai\b/', $areaL)) {
		return 'ai';
	}
	if (strpos($areaL, 'market') !== false) {
		return 'marketing';
	}
	if (strpos($areaL, 'document') !== false) {
		return 'documents';
	}
	if (strpos($areaL, 'super') !== false || strpos($areaL, 'portal') !== false || strpos($areaL, 'integrat') !== false) {
		return 'platform';
	}
	if (strpos($areaL, 'erp') !== false) {
		return 'erp';
	}
	if (strpos($areaL, 'procure') !== false) {
		return 'procurement';
	}
	if (strpos($areaL, 'content') !== false || strpos($areaL, 'cms') !== false) {
		return 'content';
	}
	if (strpos($areaL, 'system') !== false || strpos($areaL, 'admin') !== false) {
		return 'settings';
	}

	$hit = $matchBest($areaL);
	return $hit !== '' ? $hit : 'default';
}

/**
 * Assign one unique related photo to every item (mutates $areas in place).
 * Never reuses a URL. Topic illustrations: money→notes, inventory→shelves, etc.
 *
 * @param array<string, array<int, array<string,mixed>>> $areas
 * @return array<string, array{topic:string,label:string,photo:string}>
 */
function epc_cp_brochure_assign_unique_photos(array &$areas): array
{
	$catalog = epc_cp_brochure_topic_catalog();
	$used = array();
	$map = array();
	$seq = 0;

	foreach ($areas as $area => &$items) {
		if (!is_array($items)) {
			continue;
		}
		foreach ($items as &$item) {
			if (!is_array($item)) {
				continue;
			}
			$id = trim((string) ($item['id'] ?? ''));
			$name = trim((string) ($item['name'] ?? 'Process'));
			$does = trim((string) ($item['does'] ?? ''));
			$urlPath = trim((string) ($item['url'] ?? ''));
			if ($id === '') {
				$id = 'fn-' . substr(md5($area . '|' . $name . '|' . $urlPath), 0, 12);
				$item['id'] = $id;
			}
			$seq++;

			$topic = epc_cp_brochure_resolve_topic($name, (string) $area, $urlPath, $does);
			if (!isset($catalog[$topic])) {
				$topic = 'default';
			}
			$label = (string) ($catalog[$topic]['label'] ?? 'Operations');

			$custom = trim((string) ($item['image'] ?? ''));
			// Ignore previously stamped/shared images — always mint a unique related photo.
			if ($custom !== '' && strpos($custom, 'epc_brochure_process_photo.php') === false
				&& (strpos($custom, '/') === 0 || preg_match('#^https?://#i', $custom))
				&& !isset($used[$custom])) {
				$photo = $custom;
			} else {
				$seed = preg_replace('/[^a-zA-Z0-9_\-]/', '', $id) . '-u' . $seq;
				$photo = epc_cp_brochure_topic_svg_url($topic, $seed, $name, (string) $area);
				$n = 0;
				while (isset($used[$photo]) && $n < 8) {
					$n++;
					$photo = epc_cp_brochure_topic_svg_url($topic, $seed . 'x' . $n, $name, (string) $area);
				}
			}

			$used[$photo] = 1;
			$meta = array(
				'topic' => $topic,
				'label' => $label,
				'photo' => $photo,
			);
			$item['image'] = $photo;
			$item['photo_topic'] = $topic;
			$item['photo_label'] = $label;
			$map[$id . '#u' . $seq] = $meta;
		}
		unset($item);
	}
	unset($items);

	return $map;
}

/**
 * @param array{name?:string,does?:string,url?:string,image?:string,icon?:string,id?:string} $item
 * @return array{topic:string,label:string,photo:string}
 */
function epc_cp_brochure_item_topic_photo(array $item, string $area): array
{
	$id = trim((string) ($item['id'] ?? ''));
	$name = trim((string) ($item['name'] ?? 'Process'));
	$does = trim((string) ($item['does'] ?? ''));
	$url = trim((string) ($item['url'] ?? ''));
	if ($id === '') {
		$id = 'fn-' . substr(md5($area . '|' . $name . '|' . $url), 0, 12);
	}

	// Prefer photo stamped on the item during inventory build (guaranteed unique).
	if (!empty($item['image']) && !empty($item['photo_label'])) {
		return array(
			'topic' => (string) ($item['photo_topic'] ?? 'default'),
			'label' => (string) $item['photo_label'],
			'photo' => (string) $item['image'],
		);
	}

	$map = $GLOBALS['epc_cp_brochure_photo_map'] ?? null;
	$na = 'na:' . md5(strtolower($name) . '|' . strtolower($area) . '|' . $url);
	if (is_array($map) && isset($map[$na])) {
		return $map[$na];
	}
	if (is_array($map) && isset($map[$id])) {
		return $map[$id];
	}

	$topic = epc_cp_brochure_resolve_topic($name, $area, $url, $does);
	$catalog = epc_cp_brochure_topic_catalog();
	$meta = $catalog[$topic] ?? $catalog['default'];

	$custom = trim((string) ($item['image'] ?? ''));
	if ($custom !== '' && (strpos($custom, '/') === 0 || preg_match('#^https?://#i', $custom))) {
		return array(
			'topic' => $topic,
			'label' => (string) ($meta['label'] ?? 'Operations'),
			'photo' => $custom,
		);
	}

	// Standalone fallback: unique related illustration (never a shared Unsplash index).
	return array(
		'topic' => $topic,
		'label' => (string) ($meta['label'] ?? 'Operations'),
		'photo' => epc_cp_brochure_topic_svg_url($topic, $id . '-' . substr(md5($area . '|' . $url), 0, 6), $name, $area),
	);
}
