<?php
/**
 * Topic-related brochure photos — keyword → real Unsplash scenes.
 * Inventory → warehouse/parts; currency → banknotes; orders → parcels; etc.
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

	return array(
		'autoparts' => array(
			'label' => 'Auto parts',
			'queries' => array(
				'part', 'parts', 'cross', 'oem', 'vin', 'catalogue', 'catalog', 'sku', 'article',
				'laximo', 'tecdoc', 'accessor', 'engine', 'brake', 'filter', 'garage', 'mechanic',
			),
			'photos' => array(
				$u('photo-1486262715619-67b85e0b08d3'),
				$u('photo-1492144534655-ae79c964c9d7'),
				$u('photo-1503376780353-7e6692767b70'),
				$u('photo-1619642751034-765df4ed3f09'),
			),
		),
		'inventory' => array(
			'label' => 'Inventory & warehouse',
			'queries' => array(
				'inventor', 'warehouse', 'storage', 'stock', 'bin', 'shelf', 'replenish',
				'location', 'wms', 'pallet', 'fulfilivendor', 'price list', 'pricelist',
			),
			'photos' => array(
				$u('photo-1553413077-190dd305871c'),
				$u('photo-1586528116311-ad8dd3c8310d'),
				$u('photo-1566576721346-d4a3b4eaeb55'),
				$u('photo-1578575437130-527eed3abbec'),
			),
		),
		'money' => array(
			'label' => 'Money & currency',
			'queries' => array(
				'currenc', 'money', 'cash', 'banknote', 'treasury', 'ledger', 'finance',
				'account', 'invoice', 'billing', 'payment', 'gateway', 'stripe', 'checkout',
				'pos', 'till', 'price', 'pricing', 'markup', 'discount', 'vat', 'tax', 'gst',
				'salary', 'payroll', 'wage', 'receivable', 'payable', 'bank', 'card',
			),
			'photos' => array(
				$u('photo-1554224155-6726b3ff858f'),
				$u('photo-1579621970563-ebda9807a8a3'),
				$u('photo-1556742049-0cfed4f6a45d'),
				$u('photo-1633155585460-bab0b5eb0f1b'),
			),
		),
		'orders' => array(
			'label' => 'Orders & fulfilment',
			'queries' => array(
				'order', 'oms', 'cart', 'fulfilment', 'package', 'shipment', 'desk',
				'multivendor',
			),
			'photos' => array(
				$u('photo-1566576912321-d58ddd7a6088'),
				$u('photo-1556740758-90de374c12ad'),
				$u('photo-1607083206869-4c7672e72a8a'),
				$u('photo-1586528116311-ad8dd3c8310d'),
			),
		),
		'logistics' => array(
			'label' => 'Logistics & shipping',
			'queries' => array(
				'logistic', 'ship', 'courier', 'carrier', 'truck', 'delivery', 'freight',
				'branch', 'office', 'dispatch', 'tracking',
			),
			'photos' => array(
				$u('photo-1601584115197-04ecc0da31d7'),
				$u('photo-1519003722824-194d4455a60c'),
				$u('photo-1578575437130-527eed3abbec'),
				$u('photo-1566576912321-d58ddd7a6088'),
			),
		),
		'customers' => array(
			'label' => 'Customers & CRM',
			'queries' => array(
				'customer', 'client', 'crm', 'contact', 'lead', 'user', 'staff', 'employee',
				'workforce', 'hr', 'profile', 'account manager',
			),
			'photos' => array(
				$u('photo-1521737711867-e3b97375f902'),
				$u('photo-1600880292203-757bb62b4baf'),
				$u('photo-1556761175-5973dc0f32e7'),
				$u('photo-1573164713714-d95e436ab8d6'),
			),
		),
		'ai' => array(
			'label' => 'AI & assistants',
			'queries' => array(
				'ai', 'agent', 'chat', 'copilot', 'openai', 'gpt', 'assistant', 'automation',
				'machine learning', 'neural',
			),
			'photos' => array(
				$u('photo-1677442136019-21780ecad995'),
				$u('photo-1620712943543-bcc4688e7485'),
				$u('photo-1485827404703-89b55fcc595e'),
				$u('photo-1531746790176-3b4d7f5c0e0e'),
			),
		),
		'marketing' => array(
			'label' => 'Marketing & growth',
			'queries' => array(
				'market', 'broadcast', 'email', 'whatsapp', 'social', 'campaign', 'promo',
				'newsletter', 'seo', 'ads', 'facebook', 'instagram',
			),
			'photos' => array(
				$u('photo-1432888622747-4eb9a8efeb07'),
				$u('photo-1460925895917-afdab827c52f'),
				$u('photo-1557804506-669a67965ba0'),
				$u('photo-1611162617474-5b21e879e113'),
			),
		),
		'documents' => array(
			'label' => 'Documents & print',
			'queries' => array(
				'document', 'pdf', 'print', 'invoice pack', 'contract', 'report', 'export',
				'file', 'folder', 'archive', 'grn', 'rma', 'bos', 'blockchain',
			),
			'photos' => array(
				$u('photo-1450101499163-c8848c66ca85'),
				$u('photo-1586281380349-632531db7ed4'),
				$u('photo-1554224154-26032ffc0d07'),
				$u('photo-1507925921958-8a62f3d1a50d'),
			),
		),
		'platform' => array(
			'label' => 'Platform & cloud',
			'queries' => array(
				'platform', 'tenant', 'super cp', 'boc', 'cloud', 'server', 'host', 'deploy',
				'health', 'failover', 'governance', 'audit', 'api', 'integrat', 'webhook',
				'demo', 'industry', 'template',
			),
			'photos' => array(
				$u('photo-1451187580459-43490279c0fa'),
				$u('photo-1558494949-ef010cbdcc31'),
				$u('photo-1518770660439-4636190af475'),
				$u('photo-1544197150-b99a5804f8d5'),
			),
		),
		'settings' => array(
			'label' => 'Settings & security',
			'queries' => array(
				'setting', 'config', 'admin', 'role', 'permission', 'security', 'auth',
				'mfa', 'password', 'smtp', 'notification', 'brand', 'theme',
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
				'erp', 'module', 'asset', 'fixed asset', 'budget', 'close', 'period',
				'coa', 'chart of account',
			),
			'photos' => array(
				$u('photo-1454165804606-c3d57bc86b40'),
				$u('photo-1460925895917-afdab827c52f'),
				$u('photo-1554224155-8d04cb21cd6c'),
				$u('photo-1507679799987-c73779587ccf'),
			),
		),
		'procurement' => array(
			'label' => 'Procurement & suppliers',
			'queries' => array(
				'procure', 'supplier', 'vendor', 'purchase', 'rfq', 'po ', 'buying',
			),
			'photos' => array(
				$u('photo-1586528116311-ad8dd3c8310d'),
				$u('photo-1578575437130-527eed3abbec'),
				$u('photo-1566576721346-d4a3b4eaeb55'),
				$u('photo-1553413077-190dd305871c'),
			),
		),
		'content' => array(
			'label' => 'Content & CMS',
			'queries' => array(
				'cms', 'content', 'page', 'block', 'editor', 'visual', 'storefront', 'banner',
				'menu', 'seo page',
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
				$u('photo-1454165804606-c3d57bc86b40'),
				$u('photo-1522071820081-009f0129c71c'),
				$u('photo-1556761175-b413da4baf72'),
				$u('photo-1460925895917-afdab827c52f'),
			),
		),
	);
}

/**
 * Resolve topic key from process name / url / description first, then area.
 * (Area titles like "Prices & Catalogue" must not override "Warehouse stock".)
 */
function epc_cp_brochure_resolve_topic(string $name, string $area = '', string $url = '', string $does = ''): string
{
	$norm = static function (string $s): string {
		$s = strtolower(trim($s));
		$s = preg_replace('/[^a-z0-9\/\s_\-]+/', ' ', $s) ?? $s;
		return trim(preg_replace('/\s+/', ' ', $s) ?? $s);
	};

	// Priority order matters (more specific first).
	$order = array(
		'autoparts', 'inventory', 'money', 'orders', 'logistics', 'procurement',
		'ai', 'marketing', 'documents', 'customers', 'erp', 'content', 'platform', 'settings',
	);
	$catalog = epc_cp_brochure_topic_catalog();
	$match = static function (string $hay) use ($order, $catalog): string {
		if ($hay === '') {
			return '';
		}
		foreach ($order as $topic) {
			$queries = $catalog[$topic]['queries'] ?? array();
			foreach ($queries as $q) {
				$q = strtolower(trim((string) $q));
				if ($q !== '' && strpos($hay, $q) !== false) {
					return $topic;
				}
			}
		}
		return '';
	};

	// 1) Name + URL + description (process-specific)
	$hit = $match($norm($name . ' ' . $url . ' ' . $does));
	if ($hit !== '') {
		return $hit;
	}
	// 2) Area fallback map
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
	if (strpos($areaL, 'ai') !== false) {
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
	// 3) Keyword match on area text last
	$hit = $match($areaL);
	return $hit !== '' ? $hit : 'default';
}

/**
 * Pick a stable photo URL for an item (same process → same photo).
 *
 * @param array{name?:string,does?:string,url?:string,image?:string,icon?:string} $item
 * @return array{topic:string,label:string,photo:string}
 */
function epc_cp_brochure_item_topic_photo(array $item, string $area): array
{
	$custom = trim((string) ($item['image'] ?? ''));
	$name = trim((string) ($item['name'] ?? 'Process'));
	$does = trim((string) ($item['does'] ?? ''));
	$url = trim((string) ($item['url'] ?? ''));
	$topic = epc_cp_brochure_resolve_topic($name, $area, $url, $does);
	$catalog = epc_cp_brochure_topic_catalog();
	$meta = $catalog[$topic] ?? $catalog['default'];
	$photos = $meta['photos'] ?? array();
	if ($custom !== '' && (strpos($custom, '/') === 0 || preg_match('#^https?://#i', $custom))) {
		return array(
			'topic' => $topic,
			'label' => (string) ($meta['label'] ?? 'Operations'),
			'photo' => $custom,
		);
	}
	if (!$photos) {
		$photos = $catalog['default']['photos'];
	}
	$idx = abs(crc32($name . '|' . $area . '|' . $url)) % count($photos);
	return array(
		'topic' => $topic,
		'label' => (string) ($meta['label'] ?? 'Operations'),
		'photo' => $photos[$idx],
	);
}
