<?php
/**
 * CP: SKU photos & multi-type specifications manager.
 * Route: /cp/shop/catalogue/sku_media
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/catalogue/epc_sku_media.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/shop/catalogue/epc_sku_media_cp_install.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

global $db_link, $DP_Config;
epc_sku_media_ensure_schema($db_link);
try {
	epc_sku_media_cp_install($db_link, (string) ($DP_Config->backend_dir ?? 'cp'), true);
} catch (Throwable $e) {
	// Menu install is best-effort on first open.
}

$session = DP_User::getAdminSession();
$csrf = is_array($session) ? (string) ($session['csrf_guard_key'] ?? '') : '';
$backend = trim((string) ($DP_Config->backend_dir ?? 'cp'), '/');
$base = '/' . $backend;
$profileId = (int) ($_GET['profile_id'] ?? 0);
$productId = (int) ($_GET['product_id'] ?? 0);
$brand = trim((string) ($_GET['brand'] ?? ''));
$article = trim((string) ($_GET['article'] ?? ''));

$cssHref = '/content/shop/catalogue/epc_sku_media.css?v=' . (string) @filemtime($_SERVER['DOCUMENT_ROOT'] . '/content/shop/catalogue/epc_sku_media.css');
$jsHref = '/content/shop/catalogue/epc_sku_media.js?v=' . (string) @filemtime($_SERVER['DOCUMENT_ROOT'] . '/content/shop/catalogue/epc_sku_media.js');

if (function_exists('epc_cp_page_frame_open')) {
	epc_cp_page_frame_open(array(
		'hero' => array(
			'title' => 'SKU photos & specifications',
			'sub' => 'Unlimited product photos and multi-type specification sheets for any SKU (brand + article or catalogue product).',
		),
	));
}
?>
<link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Source+Sans+3:wght@400;600;700&display=swap">
<link rel="stylesheet" href="<?php echo htmlspecialchars($cssHref, ENT_QUOTES, 'UTF-8'); ?>">

<div class="epc-sku-media" id="epc-sku-media">
	<div class="epc-sku-media__hero">
		<div>
			<h2>SKU photos &amp; specifications</h2>
			<p>Customers can see rich product photos and clear specification sheets. Add unlimited photos and any number of specification types (Technical, Dimensions, Packaging, Custom…) with unlimited rows.</p>
		</div>
		<div style="display:flex;gap:8px;flex-wrap:wrap;">
			<a class="epc-sku-media__btn epc-sku-media__btn--ghost" href="<?php echo htmlspecialchars($base . '/shop/catalogue/products', ENT_QUOTES, 'UTF-8'); ?>"><i class="fa fa-th-large"></i> Catalogue</a>
			<button type="button" class="epc-sku-media__btn" data-sku-action="new"><i class="fa fa-plus"></i> New SKU profile</button>
		</div>
	</div>

	<div class="epc-sku-media__layout">
		<div class="epc-sku-media__panel">
			<div class="epc-sku-media__panel-h">
				<strong>SKU library</strong>
			</div>
			<div class="epc-sku-media__panel-b">
				<div class="epc-sku-media__search">
					<input type="text" id="epc-sku-search" placeholder="Search brand, article, title…">
					<button type="button" class="epc-sku-media__btn epc-sku-media__btn--ghost" data-sku-action="search"><i class="fa fa-search"></i></button>
				</div>
				<ul class="epc-sku-media__list" id="epc-sku-list"></ul>
			</div>
		</div>

		<div class="epc-sku-media__panel">
			<div class="epc-sku-media__panel-h">
				<strong>SKU detail</strong>
				<div style="display:flex;gap:6px;">
					<button type="button" class="epc-sku-media__btn epc-sku-media__btn--sm" data-sku-action="save-profile"><i class="fa fa-save"></i> Save</button>
					<button type="button" class="epc-sku-media__btn epc-sku-media__btn--ghost epc-sku-media__btn--sm" data-sku-action="delete-profile">Delete</button>
				</div>
			</div>
			<div class="epc-sku-media__panel-b" id="epc-sku-editor-body">
				<input type="hidden" name="profile_id" value="">
				<div class="epc-sku-media__grid2">
					<div class="epc-sku-media__field">
						<label>Brand</label>
						<input type="text" name="brand" placeholder="e.g. Bosch">
					</div>
					<div class="epc-sku-media__field">
						<label>Article / SKU</label>
						<input type="text" name="article" placeholder="e.g. 0 986 494 053">
					</div>
					<div class="epc-sku-media__field">
						<label>Display title</label>
						<input type="text" name="title" placeholder="Customer-facing product name">
					</div>
					<div class="epc-sku-media__field">
						<label>Subtitle</label>
						<input type="text" name="subtitle" placeholder="Short supporting line">
					</div>
					<div class="epc-sku-media__field">
						<label>Catalogue product ID (optional)</label>
						<input type="number" name="product_id" min="0" step="1" placeholder="Links to shop product">
					</div>
					<div class="epc-sku-media__field">
						<label>Status</label>
						<select name="status">
							<option value="active">Active</option>
							<option value="draft">Draft</option>
							<option value="hidden">Hidden</option>
						</select>
					</div>
				</div>

				<hr style="border:0;border-top:1px solid #e2e8f0;margin:18px 0;">

				<h3 style="margin:0 0 8px;font-size:16px;"><i class="fa fa-camera"></i> Photos <span style="color:#64748b;font-weight:500;font-size:13px;">— unlimited</span></h3>
				<div class="epc-sku-media__drop">
					<strong>Add product photos</strong>
					<span>JPEG, PNG, GIF, WebP · primary photo shows first on the storefront</span>
					<div class="epc-sku-media__grid2" style="margin-top:10px;text-align:left;">
						<div class="epc-sku-media__field">
							<label>Photo type</label>
							<select id="epc-sku-photo-type">
								<option value="product">Product</option>
								<option value="packaging">Packaging</option>
								<option value="detail">Detail / close-up</option>
								<option value="diagram">Diagram / drawing</option>
								<option value="install">Installation</option>
								<option value="datasheet">Datasheet shot</option>
								<option value="other">Other</option>
							</select>
						</div>
						<div class="epc-sku-media__field">
							<label>Caption</label>
							<input type="text" id="epc-sku-photo-caption" placeholder="Optional caption">
						</div>
						<div class="epc-sku-media__field">
							<label>Alt text</label>
							<input type="text" id="epc-sku-photo-alt" placeholder="Accessibility text">
						</div>
						<div class="epc-sku-media__field" style="display:flex;align-items:flex-end;">
							<button type="button" class="epc-sku-media__btn" data-sku-action="pick-file"><i class="fa fa-upload"></i> Upload photo</button>
							<input type="file" id="epc-sku-photo-input" accept="image/jpeg,image/png,image/gif,image/webp" style="display:none;">
						</div>
					</div>
				</div>
				<div class="epc-sku-media__photos" id="epc-sku-photos"></div>

				<hr style="border:0;border-top:1px solid #e2e8f0;margin:18px 0;">

				<h3 style="margin:0 0 8px;font-size:16px;"><i class="fa fa-list-alt"></i> Specifications <span style="color:#64748b;font-weight:500;font-size:13px;">— multiple types, unlimited rows</span></h3>
				<p style="margin:0 0 6px;color:#64748b;font-size:13px;">Add a specification type, then add as many labelled rows as you need (text, number, yes/no, list, rich).</p>
				<div class="epc-sku-media__chips" id="epc-sku-type-chips"></div>
				<button type="button" class="epc-sku-media__btn epc-sku-media__btn--ghost epc-sku-media__btn--sm" data-sku-action="add-group"><i class="fa fa-plus"></i> Custom type</button>
				<div id="epc-sku-specs" style="margin-top:12px;"></div>

				<div class="epc-sku-media__empty" id="epc-sku-editor-empty" style="display:none;">Select a SKU from the left or create a new profile.</div>
			</div>
		</div>
	</div>
</div>

<script src="<?php echo htmlspecialchars($jsHref, ENT_QUOTES, 'UTF-8'); ?>"></script>
<script>
document.addEventListener('DOMContentLoaded', function () {
  new EpcSkuMedia({
    root: '#epc-sku-media',
    endpoint: '/content/shop/catalogue/ajax_epc_sku_media.php',
    csrf: <?php echo json_encode($csrf); ?>,
    profileId: <?php echo (int) $profileId; ?>,
    productId: <?php echo (int) $productId; ?>,
    brand: <?php echo json_encode($brand); ?>,
    article: <?php echo json_encode($article); ?>
  });
});
</script>
<?php
if (function_exists('epc_cp_page_frame_close')) {
	epc_cp_page_frame_close();
}
