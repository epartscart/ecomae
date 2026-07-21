<?php
/**
 * Скрипт для управления таблицей кроссов
*/
defined('_ASTEXE_') or die('No access');


//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();

//Определяем текущую сортировку и обозначаем ее:
$crosses_sort = null;
if( isset($_COOKIE["crosses_sort"]) )
{
	$crosses_sort = $_COOKIE["crosses_sort"];
}
$sort_field = "id";
$sort_asc_desc = "desc";
if($crosses_sort != NULL)
{
	$crosses_sort = json_decode($crosses_sort, true);
	$sort_field = $crosses_sort["field"];
	$sort_asc_desc = $crosses_sort["asc_desc"];
}

if( strtolower($sort_asc_desc) == "asc" )
{
	$sort_asc_desc = "asc";
}
else
{
	$sort_asc_desc = "desc";
}

if( array_search($sort_field, array('id', 'article', 'manufacturer_article', 'analog', 'manufacturer_analog')) === false )
{
	$sort_field = "article";
}

$epc_cx_approx = 0;
if (isset($db_link) && $db_link instanceof PDO) {
	try {
		$stApprox = $db_link->query(
			"SELECT TABLE_ROWS FROM information_schema.TABLES
			 WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'shop_docpart_articles_analogs_list' LIMIT 1"
		);
		$epc_cx_approx = (int) $stApprox->fetchColumn();
	} catch (Throwable $e) {
		$epc_cx_approx = 0;
	}
}
$epc_cx_src_local = !empty($DP_Config->local_crosses);
$epc_cx_src_ucats = !empty($DP_Config->ucats_crosses);
$epc_cx_src_api = (isset($DP_Config->storages_of_crosses) && trim((string) $DP_Config->storages_of_crosses) !== '');
$epc_cx_src_brands = !empty($DP_Config->list_brends_crosses);
$epc_cx_sources_on = (int) $epc_cx_src_local + (int) $epc_cx_src_ucats + (int) $epc_cx_src_api + (int) $epc_cx_src_brands;
$epc_cx_csv = '/' . $DP_Config->backend_dir . '/content/shop/crosses/crosses.csv';
?>
<link rel="stylesheet" href="/content/general_pages/epc_crosses_cp_css.php?v=cx1" />
<div class="epc-cx">
	<header class="epc-cx__hero">
		<div>
			<div class="epc-cx__eyebrow"><i class="fa fa-exchange"></i> Catalogue · Interchange</div>
			<h2>Spare-parts cross references</h2>
			<p>Link OEM and aftermarket part numbers so storefront search finds interchangeable brands. Add pairs manually, import CSV, or pull from the interchange catalog into the CP table.</p>
		</div>
		<div class="epc-cx__stats">
			<div class="epc-cx__stat">
				<b><?php echo $epc_cx_approx > 0 ? ('~' . number_format($epc_cx_approx)) : '—'; ?></b>
				<span>CP links</span>
			</div>
			<div class="epc-cx__stat">
				<b><?php echo (int) $epc_cx_sources_on; ?>/4</b>
				<span>Sources on</span>
			</div>
		</div>
	</header>

	<section class="epc-cx__workbench" aria-label="Add and lookup crosses">
		<div class="epc-cx__card">
			<div class="epc-cx__card-head">
				<h3><i class="fa fa-plus"></i> <?php echo translate_str_by_id(3115); ?></h3>
				<span class="epc-cx__step">Add</span>
			</div>
			<div class="epc-cx__card-body">
				<div class="epc-cx__pair-visual" aria-hidden="true">
					<span class="epc-cx__pair-chip">Article + Brand</span>
					<i class="fa fa-arrows-h"></i>
					<span class="epc-cx__pair-chip">Analog + Brand</span>
				</div>
				<div class="epc-cx__fields">
					<div class="epc-cx__field">
						<label for="new_article"><?php echo translate_str_by_id(2071); ?></label>
						<input class="form-control" type="text" id="new_article" name="article" placeholder="e.g. 45114STD" autocomplete="off"/>
					</div>
					<div class="epc-cx__field">
						<label for="new_manufacturer_article"><?php echo translate_str_by_id(2070); ?></label>
						<input class="form-control" type="text" id="new_manufacturer_article" name="manufacturer" placeholder="e.g. TEIKINP" autocomplete="off"/>
					</div>
					<div class="epc-cx__field">
						<label for="new_analog"><?php echo translate_str_by_id(3113); ?></label>
						<input class="form-control" type="text" id="new_analog" name="analog" placeholder="Interchange article" autocomplete="off"/>
					</div>
					<div class="epc-cx__field">
						<label for="new_manufacturer_analog"><?php echo translate_str_by_id(2070); ?></label>
						<input class="form-control" type="text" id="new_manufacturer_analog" name="manufacturer_analog" placeholder="Interchange brand" autocomplete="off"/>
					</div>
				</div>
				<div class="epc-cx__divider">or CSV import</div>
				<div class="epc-cx__field">
					<label for="file_csv"><?php echo translate_str_by_id(3116); ?></label>
					<input class="form-control" type="file" id="file_csv" name="file" accept=".csv,text/csv"/>
					<p class="epc-cx__hint">
						CSV (semicolon): <code>manufacturer;article;manufacturer_cross;article_cross</code>
						· <a href="<?php echo htmlspecialchars($epc_cx_csv, ENT_QUOTES, 'UTF-8'); ?>" download><?php echo translate_str_by_id(3117); ?></a>
					</p>
				</div>
			</div>
			<div class="epc-cx__card-foot">
				<img id="img_crosses_add" class="hidden" style="height:28px;" src="/content/files/images/ajax-loader-transparent.gif" alt=""/>
				<img id="img_crosses_add_of_csv" class="hidden" style="height:28px;" src="/content/files/images/ajax-loader-transparent.gif" alt=""/>
				<a id="btn_crosses_add_of_csv" onclick="crosses_add_of_csv();" class="btn btn-ar btn-default"><i class="fa fa-upload"></i> Import CSV</a>
				<a id="btn_crosses_add" onclick="crosses_add();" class="btn btn-ar btn-primary"><i class="fa fa-plus"></i> <?php echo translate_str_by_id(2267); ?></a>
			</div>
		</div>

		<div class="epc-cx__card" id="epc_cp_cross_lookup_panel">
			<div class="epc-cx__card-head">
				<h3><i class="fa fa-search"></i> Interchange lookup</h3>
				<span class="epc-cx__step">Catalog</span>
			</div>
			<div class="epc-cx__card-body">
				<div class="epc-cx__fields">
					<div class="epc-cx__field">
						<label for="epc_cross_lookup_article">Part number</label>
						<input class="form-control" type="text" id="epc_cross_lookup_article" placeholder="e.g. 45114STD" autocomplete="off"/>
					</div>
					<div class="epc-cx__field">
						<label for="epc_cross_lookup_manufacturer">Brand / manufacturer</label>
						<input class="form-control" type="text" id="epc_cross_lookup_manufacturer" placeholder="e.g. TEIKINP" autocomplete="off"/>
					</div>
				</div>
				<div class="epc-cx__tools epc-cx__tools-primary" style="margin-top:12px;">
					<a class="btn btn-ar btn-primary" onclick="epcCpCrossLookup();" id="epc_btn_cross_lookup"><i class="fa fa-search"></i> Search interchange</a>
					<a class="btn btn-ar btn-success" onclick="epcCpCrossSyncCrossbase();" id="epc_btn_cross_sync"><i class="fa fa-sync"></i> Sync to CP</a>
					<img id="epc_img_cross_lookup" class="hidden" style="height:28px;" src="/content/files/images/ajax-loader-transparent.gif" alt=""/>
				</div>
				<div class="epc-cx__tools epc-cx__tools-secondary">
					<a class="btn btn-ar btn-warning btn-sm" onclick="epcCpCrossAddAllMissing();" id="epc_btn_cross_add_all" title="Link missing crosses from the preview table"><i class="fa fa-plus-square"></i> Add missing</a>
					<a class="btn btn-ar btn-warning btn-sm" onclick="epcCpCrossImportFullCatalog();" id="epc_btn_cross_import_full" title="Fetch full catalog (up to 5000) into CP"><i class="fa fa-database"></i> Import full catalog</a>
					<a class="btn btn-ar btn-default btn-sm" onclick="epcCpCrossVerify();" id="epc_btn_cross_verify"><i class="fa fa-check-circle"></i> Verify</a>
					<a class="btn btn-ar btn-danger btn-sm" onclick="epcCpCrossRepairEmpty();" id="epc_btn_cross_repair"><i class="fa fa-wrench"></i> Fix empty brands</a>
				</div>
				<div id="epc_cp_cross_lookup_meta" class="epc-cx__meta"></div>
				<div id="epc_cp_cross_results_wrap" class="epc-cx__results"></div>
			</div>
		</div>
	</section>

	<section class="epc-cx__main" aria-label="CP crosses table">
		<aside class="epc-cx__side">
			<div class="epc-cx__card">
				<div class="epc-cx__card-head">
					<h3><i class="fa fa-filter"></i> <?php echo translate_str_by_id(5227); ?></h3>
					<span class="epc-cx__step">Filter</span>
				</div>
				<div class="epc-cx__card-body">
					<div class="epc-cx__filter-grid">
						<div class="epc-cx__field">
							<label for="search_article"><?php echo translate_str_by_id(3119); ?></label>
							<input class="form-control" type="text" id="search_article" name="article" placeholder="Part number" autocomplete="off"/>
						</div>
						<div class="epc-cx__field">
							<label for="search_manufacturer"><?php echo translate_str_by_id(3120); ?></label>
							<select id="search_manufacturer" class="form-control"><option id="all"><?php echo translate_str_by_id(3121); ?></option></select>
						</div>
						<label class="epc-cx__check" for="search_null">
							<input type="checkbox" id="search_null" name="null"/>
							<span><?php echo translate_str_by_id(5228); ?></span>
						</label>
						<div class="epc-cx__id-row">
							<div class="epc-cx__field">
								<label for="search_id_from">ID <?php echo translate_str_by_id(4143); ?></label>
								<input class="form-control" type="number" id="search_id_from" name="id_from"/>
							</div>
							<div class="epc-cx__field">
								<label for="search_id_before">ID <?php echo translate_str_by_id(4144); ?></label>
								<input class="form-control" type="number" id="search_id_before" name="id_before"/>
							</div>
						</div>
					</div>
				</div>
				<div class="epc-cx__card-foot">
					<a onclick="clear_search();" class="btn btn-ar btn-default"><i class="fa fa-eraser"></i> <?php echo translate_str_by_id(2762); ?></a>
					<a onclick="epcCpCrossSyncLookupFromSearch(); show_table_crosses();" class="btn btn-ar btn-primary"><i class="fa fa-search"></i> <?php echo translate_str_by_id(2763); ?></a>
				</div>
			</div>

			<div class="epc-cx__card">
				<div class="epc-cx__card-head">
					<h3><i class="fa fa-trash"></i> <?php echo translate_str_by_id(5229); ?></h3>
				</div>
				<div class="epc-cx__card-body">
					<p class="epc-cx__hint" style="margin-top:0;"><?php echo translate_str_by_id(5230); ?>. <?php echo translate_str_by_id(5231); ?>.</p>
					<a onclick="del_search_crosses();" class="btn btn-ar btn-danger btn-block"><i class="fa fa-trash"></i> <?php echo translate_str_by_id(3122); ?></a>
					<img id="img_crosses_del" class="hidden" style="height:28px;margin-top:8px;" src="/content/files/images/ajax-loader-transparent.gif" alt=""/>
				</div>
			</div>

			<div class="epc-cx__card">
				<div class="epc-cx__card-head">
					<h3><i class="fa fa-sliders"></i> <?php echo translate_str_by_id(5232); ?></h3>
				</div>
				<div class="epc-cx__card-body">
					<div class="epc-cx__sources">
						<div class="epc-cx__source <?php echo $epc_cx_src_local ? '' : 'is-off'; ?>">
							<i class="fa <?php echo $epc_cx_src_local ? 'fa-check-circle' : 'fa-circle-o'; ?>"></i>
							<div><strong><?php echo translate_str_by_id(5233); ?></strong><small>Local CP table on this page</small></div>
						</div>
						<div class="epc-cx__source <?php echo $epc_cx_src_ucats ? '' : 'is-off'; ?>">
							<i class="fa <?php echo $epc_cx_src_ucats ? 'fa-check-circle' : 'fa-circle-o'; ?>"></i>
							<div><strong><?php echo translate_str_by_id(5234); ?></strong><small>Ucats catalog</small></div>
						</div>
						<div class="epc-cx__source <?php echo $epc_cx_src_api ? '' : 'is-off'; ?>">
							<i class="fa <?php echo $epc_cx_src_api ? 'fa-check-circle' : 'fa-circle-o'; ?>"></i>
							<div><strong><?php echo translate_str_by_id(5235); ?></strong><small>Supplier API crosses</small></div>
						</div>
						<div class="epc-cx__source <?php echo $epc_cx_src_brands ? '' : 'is-off'; ?>">
							<i class="fa <?php echo $epc_cx_src_brands ? 'fa-check-circle' : 'fa-circle-o'; ?>"></i>
							<div><strong><?php echo translate_str_by_id(5236); ?></strong><small>Brand list on article search</small></div>
						</div>
					</div>
					<p class="epc-cx__hint"><i class="fa fa-exclamation-triangle"></i> <?php echo translate_str_by_id(5237); ?></p>
				</div>
			</div>
		</aside>

		<div class="epc-cx__card epc-cx__table-card">
			<div class="epc-cx__card-head">
				<h3><i class="fa fa-table"></i> <?php echo translate_str_by_id(791); ?>
					<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo htmlspecialchars(translate_str_by_id(5238), ENT_QUOTES, 'UTF-8'); ?>');"><i class="fa fa-info"></i></button>
				</h3>
				<a href="javascript:void(0);" onclick="download_crosses();" title="Download crosses CSV"><i class="fa fa-download"></i> <?php echo translate_str_by_id(5239); ?></a>
			</div>
			<div id="div_table_crosses"></div>
		</div>
	</section>
</div>




<script>
	var epcCpCrossLastReferences = [];
	var epcCpCrossAjaxUrl = <?php echo json_encode('/' . $DP_Config->backend_dir . '/content/shop/crosses/ajax_epc_cross_cp.php'); ?>;
	var epcCpCrossCsrf = <?php echo json_encode((string) $user_session['csrf_guard_key']); ?>;

	function epcCpCrossEscapeHtml(value) {
		return String(value || '')
			.replace(/&/g, '&amp;')
			.replace(new RegExp('<', 'g'), '&lt;')
			.replace(new RegExp('>', 'g'), '&gt;')
			.replace(/"/g, '&quot;');
	}

	function epcCpCrossGetAnchorFields() {
		var article = document.getElementById('epc_cross_lookup_article').value.trim();
		var manufacturer = document.getElementById('epc_cross_lookup_manufacturer').value.trim();
		if (!article && document.getElementById('search_article')) {
			article = document.getElementById('search_article').value.trim();
		}
		return { article: article, manufacturer: manufacturer };
	}

	function epcCpCrossPost(action, extra, onSuccess) {
		var anchor = epcCpCrossGetAnchorFields();
		if (!anchor.article) {
			alert('Enter a part number to search.');
			return;
		}
		var request_object = {
			action: action,
			article: encodeURIComponent(anchor.article),
			manufacturer: encodeURIComponent(anchor.manufacturer)
		};
		if (extra) {
			for (var key in extra) {
				if (Object.prototype.hasOwnProperty.call(extra, key)) {
					request_object[key] = extra[key];
				}
			}
		}
		jQuery('#epc_img_cross_lookup').removeClass('hidden');
		jQuery.ajax({
			type: 'POST',
			url: epcCpCrossAjaxUrl,
			dataType: 'json',
			data: 'request_object=' + encodeURIComponent(JSON.stringify(request_object)) + '&csrf_guard_key=' + encodeURIComponent(epcCpCrossCsrf),
			success: function(answer) {
				jQuery('#epc_img_cross_lookup').addClass('hidden');
				if (typeof onSuccess === 'function') {
					onSuccess(answer);
				}
			},
			error: function() {
				jQuery('#epc_img_cross_lookup').addClass('hidden');
				alert('Request failed. Try again.');
			}
		});
	}

	function epcCpCrossRenderResults(answer, anchorFields) {
		anchorFields = anchorFields || epcCpCrossGetAnchorFields();
		if (!answer || answer.status !== true) {
			var metaEl = document.getElementById('epc_cp_cross_lookup_meta');
			if (metaEl) {
				metaEl.innerHTML = '<span class="text-danger">' + epcCpCrossEscapeHtml(answer && answer.message ? answer.message : 'Search failed') + '</span>';
			}
			var wrapFail = document.getElementById('epc_cp_cross_results_wrap');
			if (wrapFail) { wrapFail.innerHTML = ''; }
			return;
		}
		epcCpCrossLastReferences = answer.references || [];
		var loaded = answer.reference_count || answer.references_loaded || 0;
		var catalogTotal = answer.total_catalog || loaded;
		var meta = '<strong>Anchor:</strong> ' + epcCpCrossEscapeHtml(answer.manufacturer) + ' ' + epcCpCrossEscapeHtml(answer.article);
		if (catalogTotal > loaded) {
			meta += ' &nbsp;|&nbsp; <strong>Preview:</strong> ' + loaded + ' of ' + catalogTotal.toLocaleString() + ' crosses';
			meta += ' <span class="text-muted">(use <em>Import full catalog to CP</em> to link all parsed rows)</span>';
		} else {
			meta += ' &nbsp;|&nbsp; <strong>Results:</strong> ' + loaded;
		}
		meta += ' &nbsp;|&nbsp; <span class="label label-linked">In CP: ' + (answer.cp_linked_in_results || 0) + '</span>';
		meta += ' <span class="label label-missing">Missing: ' + (answer.cp_missing_in_results || 0) + '</span>';
		meta += ' &nbsp;|&nbsp; CP rows for article: <strong>' + (answer.cp_links_for_article || 0) + '</strong>';
		if (answer.source) {
			meta += ' &nbsp;|&nbsp; Source: ' + epcCpCrossEscapeHtml(answer.source);
		}
		if (answer.crossbase_persisted) {
			meta += ' &nbsp;|&nbsp; Auto-saved on storefront: ' + answer.crossbase_persisted;
		}
		document.getElementById('epc_cp_cross_lookup_meta').innerHTML = meta;

		var rows = '';
		var refs = answer.references || [];
		for (var i = 0; i < refs.length; i++) {
			var ref = refs[i];
			var linked = !!ref.cp_linked;
			var brandComplete = ref.brand_complete !== false;
			var brandCell = epcCpCrossEscapeHtml(ref.brand || '');
			if (!brandCell) {
				brandCell = '<span class="text-danger">(missing)</span>';
			} else if (ref.brand_inferred) {
				brandCell += ' <span class="text-muted">(auto)</span>';
			}
			var statusLabel = linked
				? '<span class="label label-success">In CP</span>'
				: '<span class="label label-warning">Not in CP</span>';
			if (!brandComplete) {
				statusLabel += ' <span class="label label-danger">No brand</span>';
			}
			var sameNumber = (String(ref.article || '').replace(/[\s\-_]/g, '').toUpperCase() === String(anchorFields.article || '').replace(/[\s\-_]/g, '').toUpperCase());
			var addLabel = sameNumber ? '<i class="fa fa-link"></i> Link same # diff brand' : '<i class="fa fa-link"></i> Add to CP';
			var addBtn = linked
				? ''
				: (brandComplete
					? '<a class="btn btn-xs btn-primary" onclick="epcCpCrossAddOne(' + i + ');">' + addLabel + '</a>'
					: '<span class="text-muted" title="Brand required on all 4 columns">Add blocked</span>');
			rows += '<tr' + (brandComplete ? '' : ' class="danger"') + '>'
				+ '<td>' + brandCell + '</td>'
				+ '<td>' + epcCpCrossEscapeHtml(ref.article || '') + (sameNumber ? ' <span class="label label-info">same #</span>' : '') + '</td>'
				+ '<td><span class="epc-cross-source">' + epcCpCrossEscapeHtml(ref.source || '') + '</span></td>'
				+ '<td>' + statusLabel + '</td>'
				+ '<td class="text-right">' + addBtn + '</td>'
				+ '</tr>';
		}
		if (!rows) {
			document.getElementById('epc_cp_cross_results_wrap').innerHTML = '<p class="text-muted">No cross references returned.</p>';
			return;
		}
		document.getElementById('epc_cp_cross_results_wrap').innerHTML =
			'<table class="table table-striped table-bordered" id="epc_cp_cross_results_table">'
			+ '<thead><tr><th>Brand</th><th>Article</th><th>Source</th><th>CP status</th><th></th></tr></thead>'
			+ '<tbody>' + rows + '</tbody></table>';
	}

	function epcCpCrossLookup() {
		var anchor = epcCpCrossGetAnchorFields();
		document.getElementById('search_article').value = anchor.article;
		epcCpCrossPost('lookup_crosses', null, function(answer) {
			epcCpCrossRenderResults(answer, anchor);
		});
	}

	function epcCpCrossAddOne(index) {
		var ref = epcCpCrossLastReferences[index];
		if (!ref) {
			return;
		}
		epcCpCrossPost('add_cross_link', {
			ref_article: encodeURIComponent(ref.article || ''),
			ref_brand: encodeURIComponent(ref.brand || '')
		}, function(answer) {
			if (answer.status === true) {
				epcCpCrossLookup();
				show_table_crosses();
			} else {
				alert('Could not add cross link.');
			}
		});
	}

	function epcCpCrossAddAllMissing() {
		epcCpCrossPost('add_cross_bulk', {
			references: epcCpCrossLastReferences,
			only_missing: 1
		}, function(answer) {
			if (answer.status === true) {
				var msg = 'Preview table - added: ' + (answer.inserted || 0) + ', already linked: ' + (answer.already || 0) + ', skipped: ' + (answer.skipped || 0);
				if (answer.reason) { msg += ' (' + answer.reason + ')'; }
				alert(msg);
				epcCpCrossLookup();
				show_table_crosses();
			} else {
				alert(answer.message || 'Bulk add failed');
			}
		});
	}

	function epcCpCrossImportFullCatalog() {
		var anchor = epcCpCrossGetAnchorFields();
		if (!confirm('Import the full interchange catalog for ' + anchor.manufacturer + ' ' + anchor.article + ' into CP crosses?\n\nThis may take 1-3 minutes and can link thousands of rows (up to 5000 parsed from the catalog).')) {
			return;
		}
		jQuery('#epc_btn_cross_import_full').prop('disabled', true);
		epcCpCrossPost('import_full_catalog', { only_missing: 1 }, function(answer) {
			jQuery('#epc_btn_cross_import_full').prop('disabled', false);
			if (answer.status !== true) {
				alert(answer.message || 'Full catalog import failed');
				return;
			}
			var msg = 'Full catalog import finished.\n\n';
			msg += 'Parsed from interchange catalog: ' + (answer.references_loaded || 0);
			if (answer.total_catalog && answer.total_catalog > (answer.references_loaded || 0)) {
				msg += ' (catalog reports ' + answer.total_catalog + ' total)';
			}
			msg += '\nProcessed: ' + (answer.processed || 0);
			msg += '\nNew CP links: ' + (answer.inserted || 0);
			msg += '\nAlready linked: ' + (answer.already || 0);
			msg += '\nSkipped: ' + (answer.skipped || 0);
			msg += '\nCP rows for article: ' + (answer.cp_links_for_article || 0);
			if (answer.catalog_note) {
				msg += '\n\n' + answer.catalog_note;
			}
			alert(msg);
			epcCpCrossLookup();
			show_table_crosses();
		});
	}

	function epcCpCrossSyncCrossbase() {
		epcCpCrossPost('sync_from_crossbase', null, function(answer) {
			if (answer.status === true) {
				alert('Interchange sync - added: ' + (answer.inserted || 0) + ', already: ' + (answer.already || 0));
				epcCpCrossLookup();
				show_table_crosses();
			} else {
				alert(answer.message || 'Sync failed');
			}
		});
	}

	function epcCpCrossRepairEmpty() {
		if (!confirm('Fill empty Manufacturer / Analog manufacturer cells in CP crosses using price lists and article patterns?')) {
			return;
		}
		var anchor = epcCpCrossGetAnchorFields();
		epcCpCrossPost('repair_empty_brands', { limit: 1000 }, function(answer) {
			if (answer.status === true) {
				alert('Repaired: ' + (answer.updated || 0) + ' row(s). Still unresolved: ' + (answer.skipped || 0));
				epcCpCrossLookup();
				show_table_crosses();
			} else {
				alert(answer.message || 'Repair failed');
			}
		});
	}

	function epcCpCrossVerify() {
		epcCpCrossPost('verify_crosses', null, function(answer) {
			if (answer.status !== true) {
				alert(answer.message || 'Verify failed');
				return;
			}
			var msg = 'CP verification for ' + answer.manufacturer + ' ' + answer.article + '\n\n';
			msg += 'CP rows in database (article): ' + answer.cp_links_in_db + '\n';
			msg += 'Cross results checked: ' + answer.results_total + '\n';
			msg += 'Linked in CP: ' + answer.cp_linked_in_results + '\n';
			msg += 'Still missing: ' + answer.cp_missing_in_results + '\n';
			msg += 'Coverage: ' + answer.coverage_percent + '%\n';
			if (answer.fully_linked) {
				msg += '\nAll returned crosses are linked in CP.';
			}
			alert(msg);
			epcCpCrossLookup();
		});
	}

	function epcCpCrossSyncLookupFromSearch() {
		var article = document.getElementById('search_article').value.trim();
		if (article) {
			document.getElementById('epc_cross_lookup_article').value = article;
		}
	}

	var page = 1;// Текущая страница таблицы кроссов
	var epcCxTableReq = 0;
	var epcCxMfrTimer = null;
	
	// Функция перехода по страницам таблицы кроссов
	function go_to_page(p){
		page = Math.max(1, parseInt(p, 10) || 1);
		show_table_crosses(1);
	}
	
	// Функция отображает таблицу кроссов с условиями фильтрации
	function show_table_crosses(flag){
		var box = document.getElementById('div_table_crosses');
		if (!box) { return; }
		box.innerHTML = '<div class="panel-body epc-cross-loading"><div class="spinner-border" role="status"></div><div style="margin-top:8px;">Loading crosses…</div></div>';

		// Если заданы ограничения фильтрации
		var article = document.getElementById("search_article").value;
		var manufacturer = '';
		if(article){
			var n = document.getElementById("search_manufacturer").options.selectedIndex;
			if(n > 0){
				var val = document.getElementById("search_manufacturer").options[n].value;
				manufacturer = val;
			}
			if(flag != 1){
			page = 1;
			}
		}
		
		let search_null = 0;
		if(document.getElementById("search_null").checked){
			search_null = 1;
		}
		
		let search_id_from = 'null';
		if(document.getElementById("search_id_from").value){
			search_id_from = document.getElementById("search_id_from").value;
		}
		
		let search_id_before = 'null';
		if(document.getElementById("search_id_before").value){
			search_id_before = document.getElementById("search_id_before").value;
		}
		
		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'get_table_crosses';
		request_object.page = page;
		request_object.article = encodeURIComponent(article);
		request_object.manufacturer = encodeURIComponent(manufacturer);
		request_object.null = search_null;
		request_object.id_from = search_id_from;
		request_object.id_before = search_id_before;

		var reqId = ++epcCxTableReq;
		// Отправляем запрос
		jQuery.ajax({
            type: "POST",
            async: true,
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/crosses/ajax_operations.php",
            dataType: "text",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
				if (reqId !== epcCxTableReq) { return; }
				var raw = (answer == null) ? '' : String(answer);
				var trimmed = raw.replace(/^\uFEFF/, '').trim();
				if(trimmed.charAt(0) === '{')
				{
					try {
						var parsed = JSON.parse(trimmed);
						var msg = (parsed && parsed.message) ? parsed.message : 'Could not load crosses.';
						box.innerHTML = '<div class="panel-body"><div class="alert alert-danger mb-0">'+epcCpCrossEscapeHtml(msg)+'</div></div>';
						return;
					} catch (e) {}
				}
				if(trimmed === '')
				{
					box.innerHTML = '<div class="panel-body"><div class="alert alert-danger mb-0">Empty response from crosses API. Reload or search by article.</div></div>';
					return;
				}
				// Вставляем сформированный html на страницу
				box.innerHTML = answer;
				
				let sort_field = "article";
				let sort_asc_desc = "desc";
				
				//Берем из куки текущий вариант сортировки
				var current_sort_cookie = getCookie("crosses_sort");
				if(current_sort_cookie != undefined)
				{
					current_sort_cookie = JSON.parse(getCookie("crosses_sort"));
					sort_field = current_sort_cookie.field;
					sort_asc_desc = current_sort_cookie.asc_desc;
				}
				
				if(document.getElementById(sort_field +"_sorter"))
				{
					document.getElementById(sort_field +"_sorter").innerHTML += "<img src=\"/content/files/images/sort_"+ sort_asc_desc +".png\" style=\"width:15px\" />";
				}
		    },
			error: function(xhr)
			{
				if (reqId !== epcCxTableReq) { return; }
				var detail = (xhr && xhr.status) ? ('HTTP ' + xhr.status) : 'network error';
				box.innerHTML = '<div class="panel-body"><div class="alert alert-danger mb-0">Crosses table failed ('+detail+'). Try searching by article/brand, or reload.</div></div>';
			}
        });
	}
	
	
	
	// Функция ручного добавления кросса
	function crosses_add(){
		var article 			 = document.getElementById('new_article').value;
		var manufacturer_article = document.getElementById('new_manufacturer_article').value;
		var analog 				 = document.getElementById('new_analog').value;
		var manufacturer_analog  = document.getElementById('new_manufacturer_analog').value;

		if(article === '' || manufacturer_article === '' || analog === '' || manufacturer_analog === ''){
			alert("<?php echo translate_str_by_id(3127); ?>");
			return;
		}
		
		// Очищаем форму
		document.getElementById("new_article").value = '';
		document.getElementById("new_manufacturer_article").value = '';
		document.getElementById("new_analog").value = '';
		document.getElementById("new_manufacturer_analog").value = '';
		
		$('#btn_crosses_add').addClass('disabled');// Блокируем кнопку
		$('#img_crosses_add').removeClass('hidden');// Отображаем индикатор загрузки
		
		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'add_crosses';
		request_object.article = encodeURIComponent(article);
		request_object.manufacturer_article = encodeURIComponent(manufacturer_article);
		request_object.analog = encodeURIComponent(analog);
		request_object.manufacturer_analog = encodeURIComponent(manufacturer_analog);
    
        jQuery.ajax({
            type: "POST",
            async: true, //Запрос синхронный
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/crosses/ajax_operations.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
                $('#btn_crosses_add').removeClass('disabled');// Разблокируем кнопку
				$('#img_crosses_add').addClass('hidden');// Убираем индикатор загрузки
				
				//console.log(answer);
                if(answer.status == true)
                {
                   show_table_crosses();
                }
                else
                {
					alert("<?php echo translate_str_by_id(3124); ?>");
                }
            }
        });
	}
	
	

	// Функция загрузки кроссов из csv файла
	function crosses_add_of_csv(){
		
		//Проверка наличия файла
		var csv_file = document.getElementById("file_csv").value;
		if( csv_file == "" )
		{
			alert("<?php echo translate_str_by_id(3125); ?>");
			return;
		}
		
		//Отправляем файл на сервер
		var csv_file = $("#file_csv");//Input с файлом
		var formData = new FormData;//Объект данных формы
		formData.append('csv_file', csv_file.prop('files')[0]);//Добавляем в объект формы - файл
		formData.append('csrf_guard_key', '<?php echo $user_session["csrf_guard_key"]; ?>');
		
		document.getElementById("file_csv").value = '';// Очищаем форму
		$('#btn_crosses_add_of_csv').addClass('disabled');// Блокируем кнопку
		$('#img_crosses_add_of_csv').removeClass('hidden');// Отображаем индикатор загрузки
		
		$.ajax({
			url: '/<?php echo $DP_Config->backend_dir; ?>/content/shop/crosses/ajax_upload_file_to_tmp.php',
			data: formData,
			processData: false,
			contentType: false,
			type: 'POST',
			dataType: "json",//Тип возвращаемого значения
			success: function (result) 
			{
				if( result.status != true )
				{
					alert("<?php echo translate_str_by_id(3126); ?>");
					$('#btn_crosses_add_of_csv').removeClass('disabled');// Разблокируем кнопку
					$('#img_crosses_add_of_csv').addClass('hidden');// Убираем индикатор загрузки
				}
				else//Файл загружен, запускаем импорт
				{
					//Создаем объект с параметрами импорта
					var import_options = new Object;
					import_options.file_full_path = result.file_full_path;//Полный путь к файлу
					
					//Передаем запрос на сервер для запуска процесса
					jQuery.ajax({
						type: "POST",
						async: true,
						url: '/<?php echo $DP_Config->backend_dir; ?>/content/shop/crosses/ajax_handle_file.php',
						dataType: "json",//Тип возвращаемого значения
						data: "import_options="+encodeURI( JSON.stringify(import_options) )+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
						success: function(result)
						{
							$('#btn_crosses_add_of_csv').removeClass('disabled');// Разблокируем кнопку
							$('#img_crosses_add_of_csv').addClass('hidden');// Убираем индикатор загрузки
							
							if(result.status != true)
							{
								alert("<?php echo translate_str_by_id(2122); ?>. "+result.message);
							}
							else
							{
								alert("<?php echo translate_str_by_id(2300); ?>");
								go_to_page(1);
							}
						}
					});
				}
			}
		});
	}


	
	
	// Функция отображает форму редактирования
	function crosses_edit(id){
        $('#show_line_'+id).addClass('hidden');
        $('#edit_line_'+id).removeClass('hidden');
	}

	// Функция отменяет редактирование
	function crosses_edit_otmena(id){
        $('#edit_line_'+id).addClass('hidden');
		$('#show_line_'+id).removeClass('hidden');
	}

	// Функция сохранения редактируемого кросса
	function crosses_edit_save(id){
		var article = document.getElementById('article_edit_'+id).value;
		var manufacturer_article = document.getElementById('manufacturer_article_edit_'+id).value;
		var analog = document.getElementById('analog_edit_'+id).value;
		var manufacturer_analog = document.getElementById('manufacturer_analog_edit_'+id).value;
		
		if(article === '' || manufacturer_article === '' || analog === '' || manufacturer_analog === ''){
			alert("<?php echo translate_str_by_id(3127); ?>");
			return;
		}
		
		//Объект для запроса
        var request_object = new Object;
		request_object.action = 'save_crosses';
		request_object.id = id;
		request_object.article = encodeURIComponent(article);
		request_object.manufacturer_article = encodeURIComponent(manufacturer_article);
		request_object.analog = encodeURIComponent(analog);
		request_object.manufacturer_analog = encodeURIComponent(manufacturer_analog);
    
        jQuery.ajax({
            type: "POST",
            async: false, //Запрос синхронный
            url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/crosses/ajax_operations.php",
            dataType: "json",//Тип возвращаемого значения
            data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
            success: function(answer)
            {
                //console.log(answer);
                if(answer.status == true)
                {
                    show_table_crosses();
                }
                else
                {
					alert("<?php echo translate_str_by_id(3128); ?>");
                }
            }
        });
	}
	
	// Функция удаления
	function crosses_del(id){
        if(confirm('<?php echo translate_str_by_id(3129); ?>')){
			//Объект для запроса
			var request_object = new Object;
			request_object.action = 'del_crosses';
			request_object.id = id;
		
			jQuery.ajax({
				type: "POST",
				async: false, //Запрос синхронный
				url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/crosses/ajax_operations.php",
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
				success: function(answer)
				{
					//console.log(answer);
					if(answer.status == true)
					{
						show_table_crosses();
					}
					else
					{
						alert("<?php echo translate_str_by_id(2610); ?>");
					}
				}
			});
		}
	}
	
	// Функция удаления с учетом поиска
	function del_search_crosses(){
        if(confirm('<?php echo translate_str_by_id(3130); ?>')){
			
			// Если заданы ограничения фильтрации
			var article = document.getElementById("search_article").value;
			var manufacturer = '';
			if(article){
				var n = document.getElementById("search_manufacturer").options.selectedIndex;
				if(n > 0){
					var val = document.getElementById("search_manufacturer").options[n].value;
					manufacturer = val;
				}
				page = 1;
			}
			
			let search_null = 0;
			if(document.getElementById("search_null").checked){
				search_null = 1;
			}
			
			let search_id_from = 'null';
			if(document.getElementById("search_id_from").value){
				search_id_from = document.getElementById("search_id_from").value;
			}
			
			let search_id_before = 'null';
			if(document.getElementById("search_id_before").value){
				search_id_before = document.getElementById("search_id_before").value;
			}
			
			//Объект для запроса
			var request_object = new Object;
			request_object.action = 'del_search_crosses';
			request_object.page = page;
			request_object.article = encodeURIComponent(article);
			request_object.manufacturer = encodeURIComponent(manufacturer);
			request_object.null = search_null;
			request_object.id_from = search_id_from;
			request_object.id_before = search_id_before;
			
			$('#img_crosses_del').removeClass('hidden');// Отображаем индикатор
			
			jQuery.ajax({
				type: "POST",
				async: true, //Запрос синхронный
				url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/crosses/ajax_operations.php",
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
				success: function(answer)
				{
					//console.log(answer);
					if(answer.status == true)
					{
						show_table_crosses();
					}
					else
					{
						alert("<?php echo translate_str_by_id(2610); ?>");
					}
					
					$('#img_crosses_del').addClass('hidden');// Убираем индикатор
				}
			});
		}
	}
	
	
	
	
	// Функция запроса списка производителей (debounced)
	function get_search_manufacturer(){
		if (epcCxMfrTimer) { clearTimeout(epcCxMfrTimer); }
		epcCxMfrTimer = setTimeout(epcCxFetchSearchManufacturers, 280);
	}
	function epcCxFetchSearchManufacturers(){
		var article = document.getElementById('search_article').value;
		if(article.length >= 2){
			//Объект для запроса
			var request_object = new Object;
			request_object.action = 'get_search_manufacturer';
			request_object.article = encodeURIComponent(article);
			
			jQuery.ajax({
				type: "POST",
				async: true,
				url: "/<?php echo $DP_Config->backend_dir; ?>/content/shop/crosses/ajax_operations.php",
				dataType: "json",//Тип возвращаемого значения
				data: "request_object="+encodeURI(JSON.stringify(request_object))+"&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
				success: function(answer)
				{
					//console.log(answer);
					if(answer.status == true)
					{
						var select = '<option id="all"><?php echo translate_str_by_id(3121); ?></option>';
						var list_manufacturer = JSON.parse(answer.list_manufacturer);
						if(list_manufacturer){
							for(var i = 0; list_manufacturer.length > i; i++){
								select += '<option id="'+ list_manufacturer[i] +'">'+ list_manufacturer[i] +'</option>';
							}
						}
						document.getElementById('search_manufacturer').innerHTML = select;
					}
				}
			});
		}else{
			document.getElementById('search_manufacturer').innerHTML = '<option id="all"><?php echo translate_str_by_id(3121); ?></option>';
		}
	}
	
	
	
	// Функция сбрасывает фильтры поиска
	function clear_search(){
		page = 1;
		document.getElementById('search_article').value = '';
		document.getElementById('search_id_from').value = '';
		document.getElementById('search_id_before').value = '';
		document.getElementById('search_null').checked = false;
		document.getElementById('search_manufacturer').innerHTML = '<option id="all"><?php echo translate_str_by_id(3121); ?></option>';
		show_table_crosses();
	}

	
	
	// Скачать файл кроссов
	function download_crosses(){
		window.open('/<?=$DP_Config->backend_dir;?>/content/shop/crosses/download_crosses.php?csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>', '_blank');
	}
	
	
	 // ------------------------------------------------------------------------------------------------
    //Установка куки сортировки заказов
    function sortCrosses(field)
    {
        var asc_desc = "asc";//Направление по умолчанию
        
        //Берем из куки текущий вариант сортировки
        var current_sort_cookie = getCookie("crosses_sort");
        if(current_sort_cookie != undefined)
        {
            current_sort_cookie = JSON.parse(getCookie("crosses_sort"));
            //Если поле это же - обращаем направление
            if(current_sort_cookie.field == field)
            {
                if(current_sort_cookie.asc_desc == "asc")
                {
                    asc_desc = "desc";
                }
                else
                {
                    asc_desc = "asc";
                }
            }
        }
        
        
        var crosses_sort = new Object;
        crosses_sort.field = field;//Поле, по которому сортировать
        crosses_sort.asc_desc = asc_desc;//Направление сортировки
        
        //Устанавливаем cookie (на полгода)
        var date = new Date(new Date().getTime() + 15552000 * 1000);
        document.cookie = "crosses_sort="+JSON.stringify(crosses_sort)+"; path=/; expires=" + date.toUTCString();
        
        //Обновляем страницу
        go_to_page(1);
    }
    // ------------------------------------------------------------------------------------------------
	
	// returns cookie named name, or undefined
    function getCookie(name) 
    {
		name = String(name || '');
		var prefix = name + '=';
		var parts = String(document.cookie || '').split(';');
		for (var i = 0; i < parts.length; i++) {
			var part = parts[i].replace(/^\s+/, '');
			if (part.indexOf(prefix) === 0) {
				return decodeURIComponent(part.substring(prefix.length));
			}
		}
		return undefined;
    }
	
    // ------------------------------------------------------------------------------------------------
	

	// Filter / lookup keyboard shortcuts
	(function(){
		var searchArt = document.getElementById('search_article');
		if (searchArt) {
			searchArt.addEventListener('input', get_search_manufacturer);
			searchArt.addEventListener('keydown', function(e){
				if (e.key === 'Enter') {
					e.preventDefault();
					epcCpCrossSyncLookupFromSearch();
					show_table_crosses();
				}
			});
		}
		['epc_cross_lookup_article', 'epc_cross_lookup_manufacturer'].forEach(function(id){
			var el = document.getElementById(id);
			if (!el) { return; }
			el.addEventListener('keydown', function(e){
				if (e.key === 'Enter') {
					e.preventDefault();
					epcCpCrossLookup();
				}
			});
		});
	})();

	// После открытия страницы отображаем таблицу кроссов
	show_table_crosses();
</script>