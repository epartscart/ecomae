<?php
/**
 * CP: Accessories Marketplace — list / add / edit / publish / unpublish / delete.
 * Storefront: /en/accessories-spare-parts
 *
 * Do not use `return` after output: page PHP is eval()'d with the template in dp_core.php.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/docpart/epc_accessories_db.php';

$user_session = DP_User::getAdminSession();
$backend = isset($DP_Config->backend_dir) ? $DP_Config->backend_dir : 'cp';
$baseUrl = '/' . $backend . '/shop/accessories';

epc_acc_ensure_schema($db_link);
// Seed filter terms once when empty (make/city/year/condition). Safe to call repeatedly.
try {
	$termCount = (int) $db_link->query("SELECT COUNT(*) FROM `epc_acc_terms`")->fetchColumn();
	if ($termCount < 1) {
		epc_acc_seed_terms_from_json($db_link);
	}
} catch (Exception $e) {
	epc_acc_seed_terms_from_json($db_link);
}

function epc_acc_cp_redirect($url)
{
	echo '<script>location = ' . json_encode($url) . ';</script>';
	exit;
}

function epc_acc_cp_back_url($fallback)
{
	$back = trim((string) ($_POST['back'] ?? ''));
	if ($back === '' || strpos($back, 'http') === 0) {
		return $fallback;
	}
	return $back;
}

function epc_acc_cp_payload_from_post(array $tree)
{
	$catSlug = trim((string) ($_POST['category'] ?? ''));
	$subSlug = trim((string) ($_POST['subcategory'] ?? ''));
	$catId = 0;
	$subId = 0;
	foreach ($tree as $parent) {
		if ($parent['slug'] === $catSlug) {
			$catId = (int) $parent['id'];
			foreach ($parent['children'] as $child) {
				if ($child['slug'] === $subSlug) {
					$subId = (int) $child['id'];
					break;
				}
			}
			break;
		}
	}
	return array(
		'category_id' => $catId,
		'subcategory_id' => $subId,
		'title' => trim((string) ($_POST['title'] ?? '')),
		'description' => trim((string) ($_POST['description'] ?? '')),
		'make' => trim((string) ($_POST['make'] ?? '')),
		'model' => trim((string) ($_POST['model'] ?? '')),
		'year' => trim((string) ($_POST['year'] ?? '')),
		'city' => trim((string) ($_POST['city'] ?? '')),
		'condition_type' => trim((string) ($_POST['condition_type'] ?? 'new')) ?: 'new',
		'price' => (float) ($_POST['price'] ?? 0),
		'compare_price' => (float) ($_POST['compare_price'] ?? 0),
		'currency' => trim((string) ($_POST['currency'] ?? 'PKR')) ?: 'PKR',
		'image_url' => trim((string) ($_POST['image_url'] ?? '')),
		'external_url' => trim((string) ($_POST['external_url'] ?? '')),
		'photo_count' => max(1, (int) ($_POST['photo_count'] ?? 1)),
		'featured' => !empty($_POST['featured']) ? 1 : 0,
		'stock_qty' => (int) ($_POST['stock_qty'] ?? 0),
		'status' => trim((string) ($_POST['status'] ?? 'published')) ?: 'published',
	);
}

if (!empty($_POST['action'])) {
	require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/stop_csrf.php';
	$tree = epc_acc_get_category_tree($db_link);
	$action = (string) $_POST['action'];

	if ($action === 'save') {
		$data = epc_acc_cp_payload_from_post($tree);
		$id = (int) ($_POST['id'] ?? 0);
		if ($data['title'] === '' || $data['category_id'] < 1) {
			epc_acc_cp_redirect($baseUrl . '?edit=' . $id . '&error_message=' . rawurlencode('Title and category are required'));
		}
		if ($id > 0) {
			epc_acc_update_listing($db_link, $id, $data);
			$msg = 'Listing updated';
		} else {
			$id = epc_acc_add_listing($db_link, $data);
			$msg = 'Listing created';
		}
		epc_acc_cp_redirect($baseUrl . '?edit=' . $id . '&success_message=' . rawurlencode($msg));
	}

	if ($action === 'set_status') {
		$id = (int) ($_POST['id'] ?? 0);
		$status = trim((string) ($_POST['status'] ?? 'draft'));
		if ($id > 0) {
			epc_acc_set_listing_status($db_link, $id, $status);
		}
		$back = epc_acc_cp_back_url($baseUrl);
		epc_acc_cp_redirect($back . (strpos($back, '?') !== false ? '&' : '?') . 'success_message=' . rawurlencode('Status → ' . $status));
	}

	if ($action === 'delete') {
		$id = (int) ($_POST['id'] ?? 0);
		if ($id > 0) {
			epc_acc_delete_listing($db_link, $id);
		}
		epc_acc_cp_redirect($baseUrl . '?success_message=' . rawurlencode('Listing deleted'));
	}

	// —— Taxonomy management (categories + filter terms) ——
	if ($action === 'save_category') {
		$id = (int) ($_POST['id'] ?? 0);
		$parentId = (int) ($_POST['parent_id'] ?? 0);
		$label = trim((string) ($_POST['label'] ?? ''));
		$sort = (int) ($_POST['sort_order'] ?? 0);
		$newId = epc_acc_save_category($db_link, $label, $parentId, $id, $sort);
		$back = epc_acc_cp_back_url($baseUrl . '?tab=taxonomy');
		if ($newId < 1) {
			epc_acc_cp_redirect($back . (strpos($back, '?') !== false ? '&' : '?') . 'error_message=' . rawurlencode('Category label required'));
		}
		epc_acc_cp_redirect($back . (strpos($back, '?') !== false ? '&' : '?') . 'success_message=' . rawurlencode($id > 0 ? 'Category saved' : 'Category added'));
	}

	if ($action === 'set_category_active') {
		$id = (int) ($_POST['id'] ?? 0);
		$active = !empty($_POST['active']);
		if ($id > 0) {
			epc_acc_set_category_active($db_link, $id, $active);
		}
		$back = epc_acc_cp_back_url($baseUrl . '?tab=taxonomy');
		epc_acc_cp_redirect($back . (strpos($back, '?') !== false ? '&' : '?') . 'success_message=' . rawurlencode($active ? 'Category activated' : 'Category deactivated'));
	}

	if ($action === 'delete_category') {
		$id = (int) ($_POST['id'] ?? 0);
		$res = epc_acc_delete_category($db_link, $id);
		$back = epc_acc_cp_back_url($baseUrl . '?tab=taxonomy');
		$param = !empty($res['ok']) ? 'success_message' : 'error_message';
		epc_acc_cp_redirect($back . (strpos($back, '?') !== false ? '&' : '?') . $param . '=' . rawurlencode((string) ($res['message'] ?? 'Done')));
	}

	if ($action === 'save_term') {
		$type = preg_replace('/[^a-z_]/', '', strtolower((string) ($_POST['term_type'] ?? '')));
		$id = (int) ($_POST['id'] ?? 0);
		$parentId = (int) ($_POST['parent_id'] ?? 0);
		$label = trim((string) ($_POST['label'] ?? ''));
		$sort = (int) ($_POST['sort_order'] ?? 0);
		$newId = epc_acc_save_term($db_link, $type, $label, $id, $parentId, $sort);
		$back = epc_acc_cp_back_url($baseUrl . '?tab=taxonomy&term=' . rawurlencode($type ?: 'make'));
		if ($newId < 1) {
			epc_acc_cp_redirect($back . (strpos($back, '?') !== false ? '&' : '?') . 'error_message=' . rawurlencode('Term label required'));
		}
		epc_acc_cp_redirect($back . (strpos($back, '?') !== false ? '&' : '?') . 'success_message=' . rawurlencode($id > 0 ? 'Term saved' : 'Term added'));
	}

	if ($action === 'set_term_active') {
		$id = (int) ($_POST['id'] ?? 0);
		$active = !empty($_POST['active']);
		if ($id > 0) {
			epc_acc_set_term_active($db_link, $id, $active);
		}
		$back = epc_acc_cp_back_url($baseUrl . '?tab=taxonomy');
		epc_acc_cp_redirect($back . (strpos($back, '?') !== false ? '&' : '?') . 'success_message=' . rawurlencode($active ? 'Term activated' : 'Term deactivated'));
	}

	if ($action === 'delete_term') {
		$id = (int) ($_POST['id'] ?? 0);
		if ($id > 0) {
			epc_acc_delete_term($db_link, $id);
		}
		$back = epc_acc_cp_back_url($baseUrl . '?tab=taxonomy');
		epc_acc_cp_redirect($back . (strpos($back, '?') !== false ? '&' : '?') . 'success_message=' . rawurlencode('Term deleted'));
	}
}

require_once 'content/control/actions_alert.php';

$makes = epc_acc_term_labels($db_link, 'make');
$cities = epc_acc_term_labels($db_link, 'city');
$years = epc_acc_term_labels($db_link, 'year');
$conditions = epc_acc_get_terms($db_link, 'condition', false);
$modelTerms = epc_acc_get_terms($db_link, 'model', false);
$tree = epc_acc_get_category_tree($db_link);
$cpTab = preg_replace('/[^a-z_]/', '', strtolower((string) ($_GET['tab'] ?? 'listings')));
if (!in_array($cpTab, array('listings', 'taxonomy'), true)) {
	$cpTab = 'listings';
}
$editId = isset($_GET['edit']) ? (int) $_GET['edit'] : -1;
$showForm = isset($_GET['edit']) || isset($_GET['new']);
$showTaxonomy = (!$showForm && $cpTab === 'taxonomy');

?>
<?php if (!$showForm) { ?>
<div class="col-lg-12 epc-acc-cp">
	<ul class="epc-acc-tabs">
		<li<?php echo !$showTaxonomy ? ' class="active"' : ''; ?>><a href="<?php echo htmlspecialchars($baseUrl, ENT_QUOTES, 'UTF-8'); ?>">Listings</a></li>
		<li<?php echo $showTaxonomy ? ' class="active"' : ''; ?>><a href="<?php echo htmlspecialchars($baseUrl . '?tab=taxonomy', ENT_QUOTES, 'UTF-8'); ?>">Categories &amp; filters</a></li>
	</ul>
</div>
<?php } ?>

<?php if ($showForm) {
	$listing = array(
		'id' => 0,
		'category_slug' => '',
		'subcategory_slug' => '',
		'title' => '',
		'description' => '',
		'make' => '',
		'model' => '',
		'year' => '',
		'city' => '',
		'condition_type' => 'new',
		'price' => '',
		'compare_price' => '',
		'currency' => 'PKR',
		'image_url' => '',
		'external_url' => '',
		'photo_count' => 1,
		'featured' => 0,
		'stock_qty' => 0,
		'status' => 'published',
	);
	if ($editId > 0) {
		$row = epc_acc_get_listing($db_link, $editId);
		if (!$row) {
			echo '<div class="col-lg-12"><div class="alert alert-danger">Listing not found.</div></div>';
			$showForm = false;
		} else {
			$listing = $row;
			$listing['category_slug'] = $row['category_slug'] ?? '';
			$listing['subcategory_slug'] = $row['subcategory_slug'] ?? '';
		}
	}
}

if ($showForm) {
	$isNew = ((int) $listing['id'] < 1);
	?>
	<div class="col-lg-12 epc-acc-cp">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo $isNew ? 'Add accessories listing' : ('Edit listing #' . (int) $listing['id']); ?>
				<span class="pull-right"><a href="<?php echo htmlspecialchars($baseUrl, ENT_QUOTES, 'UTF-8'); ?>">← Back to list</a></span>
			</div>
			<div class="panel-body">
				<p class="muted">Fill one category at a time. Published ads appear on <a href="/en/accessories-spare-parts" target="_blank">/en/accessories-spare-parts</a>.</p>
				<form method="post">
					<input type="hidden" name="action" value="save" />
					<input type="hidden" name="id" value="<?php echo (int) $listing['id']; ?>" />
					<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />

					<div class="form-grid">
						<div>
							<label>Category *</label>
							<select name="category" id="epc-acc-cp-cat" class="form-control" required>
								<option value="">Select…</option>
								<?php foreach ($tree as $p) { ?>
									<option value="<?php echo htmlspecialchars($p['slug'], ENT_QUOTES, 'UTF-8'); ?>" <?php echo ($listing['category_slug'] ?? '') === $p['slug'] ? 'selected' : ''; ?>>
										<?php echo htmlspecialchars($p['label'], ENT_QUOTES, 'UTF-8'); ?>
									</option>
								<?php } ?>
							</select>
						</div>
						<div>
							<label>Sub category</label>
							<select name="subcategory" id="epc-acc-cp-sub" class="form-control">
								<option value="">Optional…</option>
							</select>
						</div>
						<div class="full">
							<label>Title *</label>
							<input class="form-control" name="title" required maxlength="255" value="<?php echo htmlspecialchars((string) $listing['title'], ENT_QUOTES, 'UTF-8'); ?>" placeholder="Car Brake Pads for Toyota Corolla - 2018 | Karachi" />
						</div>
						<div class="full">
							<label>Description</label>
							<textarea class="form-control" name="description" rows="3"><?php echo htmlspecialchars((string) ($listing['description'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></textarea>
						</div>
						<div>
							<label>Make</label>
							<select name="make" class="form-control">
								<option value="">—</option>
								<?php foreach ($makes as $m) { ?>
									<option value="<?php echo htmlspecialchars($m, ENT_QUOTES, 'UTF-8'); ?>" <?php echo (($listing['make'] ?? '') === $m) ? 'selected' : ''; ?>><?php echo htmlspecialchars($m, ENT_QUOTES, 'UTF-8'); ?></option>
								<?php } ?>
							</select>
						</div>
						<div>
							<label>Model</label>
							<?php if ($modelTerms) { ?>
								<select name="model" class="form-control">
									<option value="">—</option>
									<?php foreach ($modelTerms as $mt) {
										$mv = (string) $mt['label'];
										?>
										<option value="<?php echo htmlspecialchars($mv, ENT_QUOTES, 'UTF-8'); ?>" <?php echo (($listing['model'] ?? '') === $mv) ? 'selected' : ''; ?>><?php echo htmlspecialchars($mv, ENT_QUOTES, 'UTF-8'); ?></option>
									<?php } ?>
								</select>
							<?php } else { ?>
								<input class="form-control" name="model" value="<?php echo htmlspecialchars((string) ($listing['model'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
							<?php } ?>
						</div>
						<div>
							<label>Year</label>
							<?php if ($years) { ?>
								<select name="year" class="form-control">
									<option value="">—</option>
									<?php foreach ($years as $y) { ?>
										<option value="<?php echo htmlspecialchars($y, ENT_QUOTES, 'UTF-8'); ?>" <?php echo (($listing['year'] ?? '') === $y) ? 'selected' : ''; ?>><?php echo htmlspecialchars($y, ENT_QUOTES, 'UTF-8'); ?></option>
									<?php } ?>
								</select>
							<?php } else { ?>
								<input class="form-control" name="year" maxlength="16" value="<?php echo htmlspecialchars((string) ($listing['year'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" placeholder="2018" />
							<?php } ?>
						</div>
						<div>
							<label>City</label>
							<select name="city" class="form-control">
								<option value="">—</option>
								<?php foreach ($cities as $c) { ?>
									<option value="<?php echo htmlspecialchars($c, ENT_QUOTES, 'UTF-8'); ?>" <?php echo (($listing['city'] ?? '') === $c) ? 'selected' : ''; ?>><?php echo htmlspecialchars($c, ENT_QUOTES, 'UTF-8'); ?></option>
								<?php } ?>
							</select>
						</div>
						<div>
							<label>Condition</label>
							<select name="condition_type" class="form-control">
								<?php
								$condOpts = $conditions ?: array(
									array('value' => 'new', 'label' => 'New'),
									array('value' => 'used', 'label' => 'Used'),
								);
								foreach ($condOpts as $co) {
									$cv = (string) $co['value'];
									$cl = (string) $co['label'];
									?>
									<option value="<?php echo htmlspecialchars($cv, ENT_QUOTES, 'UTF-8'); ?>" <?php echo (($listing['condition_type'] ?? '') === $cv) ? 'selected' : ''; ?>><?php echo htmlspecialchars($cl, ENT_QUOTES, 'UTF-8'); ?></option>
								<?php } ?>
							</select>
						</div>
						<div>
							<label>Status</label>
							<select name="status" class="form-control">
								<?php foreach (array('published' => 'Published', 'draft' => 'Draft', 'unpublished' => 'Unpublished') as $sv => $sl) { ?>
									<option value="<?php echo $sv; ?>" <?php echo (($listing['status'] ?? '') === $sv) ? 'selected' : ''; ?>><?php echo $sl; ?></option>
								<?php } ?>
							</select>
						</div>
						<div>
							<label>Price</label>
							<input class="form-control" type="number" step="1" min="0" name="price" value="<?php echo htmlspecialchars((string) ($listing['price'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
						<div>
							<label>Compare price</label>
							<input class="form-control" type="number" step="1" min="0" name="compare_price" value="<?php echo htmlspecialchars((string) ($listing['compare_price'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
						<div>
							<label>Currency</label>
							<input class="form-control" name="currency" value="<?php echo htmlspecialchars((string) ($listing['currency'] ?? 'PKR'), ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
						<div>
							<label>Stock qty</label>
							<input class="form-control" type="number" min="0" name="stock_qty" value="<?php echo (int) ($listing['stock_qty'] ?? 0); ?>" />
						</div>
						<div>
							<label>Photo count</label>
							<input class="form-control" type="number" min="1" name="photo_count" value="<?php echo max(1, (int) ($listing['photo_count'] ?? 1)); ?>" />
						</div>
						<div>
							<label>Featured</label>
							<select name="featured" class="form-control">
								<option value="0" <?php echo empty($listing['featured']) ? 'selected' : ''; ?>>No</option>
								<option value="1" <?php echo !empty($listing['featured']) ? 'selected' : ''; ?>>Yes</option>
							</select>
						</div>
						<div class="full">
							<label>Image URL</label>
							<input class="form-control" name="image_url" value="<?php echo htmlspecialchars((string) ($listing['image_url'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" />
						</div>
						<div class="full">
							<label>Detail / external URL</label>
							<input class="form-control" name="external_url" value="<?php echo htmlspecialchars((string) ($listing['external_url'] ?? ''), ENT_QUOTES, 'UTF-8'); ?>" placeholder="/en/accessories-spare-parts?category=brakes" />
						</div>
					</div>
					<div style="margin-top:16px;">
						<button type="submit" class="btn btn-primary"><?php echo $isNew ? 'Publish listing' : 'Save changes'; ?></button>
						<a class="btn btn-default" href="<?php echo htmlspecialchars($baseUrl, ENT_QUOTES, 'UTF-8'); ?>">Cancel</a>
					</div>
				</form>
			</div>
		</div>
	</div>
	<script>
	(function () {
		var tree = <?php echo json_encode($tree, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
		var cat = document.getElementById('epc-acc-cp-cat');
		var sub = document.getElementById('epc-acc-cp-sub');
		var selectedSub = <?php echo json_encode((string) ($listing['subcategory_slug'] ?? '')); ?>;
		function fillSubs() {
			var slug = cat.value;
			sub.innerHTML = '<option value="">Optional…</option>';
			for (var i = 0; i < tree.length; i++) {
				if (tree[i].slug !== slug) continue;
				(tree[i].children || []).forEach(function (c) {
					var o = document.createElement('option');
					o.value = c.slug;
					o.textContent = c.label;
					if (c.slug === selectedSub) o.selected = true;
					sub.appendChild(o);
				});
				break;
			}
		}
		cat.addEventListener('change', function () { selectedSub = ''; fillSubs(); });
		fillSubs();
	})();
	</script>
	<?php
} elseif ($showTaxonomy) {
	require $_SERVER['DOCUMENT_ROOT'] . '/' . $backend . '/content/shop/accessories/accessories_taxonomy_panel.php';
} else {
	$filters = array(
		'q' => isset($_GET['q']) ? (string) $_GET['q'] : '',
		'category' => isset($_GET['category']) ? (string) $_GET['category'] : '',
		'subcategory' => isset($_GET['subcategory']) ? (string) $_GET['subcategory'] : '',
		'status' => isset($_GET['status']) ? (string) $_GET['status'] : '',
		'make' => isset($_GET['make']) ? (string) $_GET['make'] : '',
		'page' => isset($_GET['page']) ? (int) $_GET['page'] : 1,
		'per_page' => 50,
	);
	$result = epc_acc_admin_search($db_link, $filters);
	$items = $result['items'];
	$statusCounts = $result['status_counts'];
	$listQs = http_build_query(array_filter(array(
		'q' => $filters['q'],
		'category' => $filters['category'],
		'subcategory' => $filters['subcategory'],
		'status' => $filters['status'],
		'make' => $filters['make'],
	), function ($v) { return $v !== '' && $v !== null; }));
	$backList = $baseUrl . ($listQs !== '' ? ('?' . $listQs) : '');
	?>
	<div class="col-lg-12 epc-acc-cp">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Accessories Marketplace
				<span class="pull-right">
					<a class="btn btn-xs btn-primary" href="<?php echo htmlspecialchars($baseUrl . '?new=1', ENT_QUOTES, 'UTF-8'); ?>">+ Add listing</a>
					<a class="btn btn-xs btn-default" href="<?php echo htmlspecialchars($baseUrl . '?tab=taxonomy', ENT_QUOTES, 'UTF-8'); ?>">Manage categories &amp; filters</a>
					<a class="btn btn-xs btn-default" href="/en/accessories-spare-parts" target="_blank">View storefront</a>
				</span>
			</div>
			<div class="panel-body">
				<p>
					<strong><?php echo (int) $result['total']; ?></strong> listings
					<?php
					$bits = array();
					foreach ($statusCounts as $st => $cnt) {
						$bits[] = htmlspecialchars($st) . ': ' . (int) $cnt;
					}
					if ($bits) {
						echo ' <span class="muted">(' . implode(' · ', $bits) . ')</span>';
					}
					?>
					— put real products into PakWheels-style categories one by one.
				</p>

				<form method="get" class="filters form-inline" style="margin-bottom:12px;">
					<input type="text" class="form-control w-q" name="q" value="<?php echo htmlspecialchars($filters['q'], ENT_QUOTES, 'UTF-8'); ?>" placeholder="Search title / make / id" />
					<select name="category" id="epc-acc-cp-f-cat" class="form-control">
						<option value="">All categories</option>
						<?php foreach ($tree as $p) { ?>
							<option value="<?php echo htmlspecialchars($p['slug'], ENT_QUOTES, 'UTF-8'); ?>" <?php echo $filters['category'] === $p['slug'] ? 'selected' : ''; ?>>
								<?php echo htmlspecialchars($p['label'], ENT_QUOTES, 'UTF-8'); ?>
							</option>
						<?php } ?>
					</select>
					<select name="subcategory" id="epc-acc-cp-f-sub" class="form-control">
						<option value="">All sub categories</option>
					</select>
					<select name="status" class="form-control">
						<option value="">All statuses</option>
						<?php foreach (array('published', 'draft', 'unpublished') as $st) { ?>
							<option value="<?php echo $st; ?>" <?php echo $filters['status'] === $st ? 'selected' : ''; ?>><?php echo $st; ?></option>
						<?php } ?>
					</select>
					<select name="make" class="form-control">
						<option value="">All makes</option>
						<?php foreach ($makes as $m) { ?>
							<option value="<?php echo htmlspecialchars($m, ENT_QUOTES, 'UTF-8'); ?>" <?php echo $filters['make'] === $m ? 'selected' : ''; ?>><?php echo htmlspecialchars($m, ENT_QUOTES, 'UTF-8'); ?></option>
						<?php } ?>
					</select>
					<button type="submit" class="btn btn-default">Filter</button>
					<a class="btn btn-link" href="<?php echo htmlspecialchars($baseUrl, ENT_QUOTES, 'UTF-8'); ?>">Reset</a>
				</form>

				<table class="table table-striped table-condensed">
					<thead>
						<tr>
							<th>ID</th>
							<th>Title</th>
							<th>Category</th>
							<th>Vehicle</th>
							<th>City</th>
							<th>Price</th>
							<th>Status</th>
							<th>Updated</th>
							<th style="min-width:220px;">Actions</th>
						</tr>
					</thead>
					<tbody>
					<?php if (!$items) { ?>
						<tr><td colspan="9">No listings match these filters. <a href="<?php echo htmlspecialchars($baseUrl . '?new=1', ENT_QUOTES, 'UTF-8'); ?>">Add the first one</a>.</td></tr>
					<?php } ?>
					<?php foreach ($items as $row) {
						$st = (string) $row['status'];
						$badge = $st === 'published' ? 'badge-pub' : ($st === 'draft' ? 'badge-draft' : 'badge-unpub');
						?>
						<tr>
							<td><?php echo (int) $row['id']; ?><?php if (!empty($row['featured'])) { ?> <span class="feat" title="Featured">★</span><?php } ?></td>
							<td>
								<strong><?php echo htmlspecialchars((string) $row['title'], ENT_QUOTES, 'UTF-8'); ?></strong>
								<?php if (!empty($row['subcategory_label'])) { ?>
									<div class="muted"><?php echo htmlspecialchars((string) $row['subcategory_label'], ENT_QUOTES, 'UTF-8'); ?></div>
								<?php } ?>
							</td>
							<td><?php echo htmlspecialchars((string) ($row['category_label'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></td>
							<td class="muted"><?php echo htmlspecialchars(trim(($row['make'] ?? '') . ' ' . ($row['model'] ?? '') . ' ' . ($row['year'] ?? '')), ENT_QUOTES, 'UTF-8'); ?></td>
							<td><?php echo htmlspecialchars((string) ($row['city'] ?? ''), ENT_QUOTES, 'UTF-8'); ?></td>
							<td><?php echo htmlspecialchars((string) ($row['currency'] ?? 'PKR'), ENT_QUOTES, 'UTF-8'); ?> <?php echo number_format((float) $row['price'], 0); ?></td>
							<td><span class="badge <?php echo $badge; ?>"><?php echo htmlspecialchars($st, ENT_QUOTES, 'UTF-8'); ?></span></td>
							<td class="muted"><?php echo !empty($row['updated_at']) ? date('Y-m-d H:i', (int) $row['updated_at']) : ''; ?></td>
							<td>
								<a class="btn btn-xs btn-default" href="<?php echo htmlspecialchars($baseUrl . '?edit=' . (int) $row['id'], ENT_QUOTES, 'UTF-8'); ?>">Edit</a>
								<?php if ($st === 'published') { ?>
									<form method="post" style="display:inline;" onsubmit="return confirm('Unpublish this ad?');">
										<input type="hidden" name="action" value="set_status" />
										<input type="hidden" name="id" value="<?php echo (int) $row['id']; ?>" />
										<input type="hidden" name="status" value="unpublished" />
										<input type="hidden" name="back" value="<?php echo htmlspecialchars($backList, ENT_QUOTES, 'UTF-8'); ?>" />
										<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />
										<button type="submit" class="btn btn-xs btn-warning">Unpublish</button>
									</form>
								<?php } else { ?>
									<form method="post" style="display:inline;">
										<input type="hidden" name="action" value="set_status" />
										<input type="hidden" name="id" value="<?php echo (int) $row['id']; ?>" />
										<input type="hidden" name="status" value="published" />
										<input type="hidden" name="back" value="<?php echo htmlspecialchars($backList, ENT_QUOTES, 'UTF-8'); ?>" />
										<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />
										<button type="submit" class="btn btn-xs btn-success">Publish</button>
									</form>
								<?php } ?>
								<form method="post" style="display:inline;" onsubmit="return confirm('Delete listing #<?php echo (int) $row['id']; ?> permanently?');">
									<input type="hidden" name="action" value="delete" />
									<input type="hidden" name="id" value="<?php echo (int) $row['id']; ?>" />
									<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars($user_session['csrf_guard_key'] ?? '', ENT_QUOTES, 'UTF-8'); ?>" />
									<button type="submit" class="btn btn-xs btn-danger">Delete</button>
								</form>
							</td>
						</tr>
					<?php } ?>
					</tbody>
				</table>

				<?php if ($result['pages'] > 1) { ?>
					<ul class="pagination">
						<?php for ($p = 1; $p <= $result['pages']; $p++) {
							$qs = $listQs;
							$href = $baseUrl . '?' . ($qs !== '' ? $qs . '&' : '') . 'page=' . $p;
							$cls = $p === (int) $result['page'] ? ' class="active"' : '';
							echo '<li' . $cls . '><a href="' . htmlspecialchars($href, ENT_QUOTES, 'UTF-8') . '">' . $p . '</a></li>';
						} ?>
					</ul>
				<?php } ?>
			</div>
		</div>

		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Categories ready to fill
				<span class="pull-right"><a class="btn btn-xs btn-default" href="<?php echo htmlspecialchars($baseUrl . '?tab=taxonomy', ENT_QUOTES, 'UTF-8'); ?>">Add / remove categories &amp; filters</a></span>
			</div>
			<div class="panel-body">
				<table class="table table-condensed">
					<thead><tr><th>Category</th><th>Sub categories</th><th></th></tr></thead>
					<tbody>
					<?php foreach ($tree as $p) { ?>
						<tr>
							<td><strong><?php echo htmlspecialchars($p['label'], ENT_QUOTES, 'UTF-8'); ?></strong></td>
							<td><?php echo count($p['children']); ?></td>
							<td>
								<a href="<?php echo htmlspecialchars($baseUrl . '?category=' . rawurlencode($p['slug']), ENT_QUOTES, 'UTF-8'); ?>">View ads</a>
								·
								<a href="<?php echo htmlspecialchars($baseUrl . '?new=1', ENT_QUOTES, 'UTF-8'); ?>">Add</a>
							</td>
						</tr>
					<?php } ?>
					</tbody>
				</table>
			</div>
		</div>
	</div>
	<script>
	(function () {
		var tree = <?php echo json_encode($tree, JSON_UNESCAPED_UNICODE | JSON_UNESCAPED_SLASHES); ?>;
		var cat = document.getElementById('epc-acc-cp-f-cat');
		var sub = document.getElementById('epc-acc-cp-f-sub');
		var selectedSub = <?php echo json_encode($filters['subcategory']); ?>;
		function fill() {
			var slug = cat.value;
			sub.innerHTML = '<option value="">All sub categories</option>';
			for (var i = 0; i < tree.length; i++) {
				if (tree[i].slug !== slug) continue;
				(tree[i].children || []).forEach(function (c) {
					var o = document.createElement('option');
					o.value = c.slug;
					o.textContent = c.label;
					if (c.slug === selectedSub) o.selected = true;
					sub.appendChild(o);
				});
				break;
			}
		}
		cat.addEventListener('change', function () { selectedSub = ''; fill(); });
		fill();
	})();
	</script>
	<?php
}
?>
