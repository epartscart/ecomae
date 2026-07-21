<?php
/**
 * Страница редактирования config.php
 * 
*/
defined('_ASTEXE_') or die('No access');

require_once("content/control/dp_configeditor.php");


//Массив с именами параметров, значения которых являются ID строк из мультиязычности. Пока таких параметров всего 3, можно хранить их в этом массиве вместо таблицы config_items (что потребовалобы изменения схемы данных)
$translated_items = array('site_name', 'description_tag', 'keywords_tag', 'retention_percentage_text');
?>

<?php
//ИЗМЕНЕНИЕ config.php
//DP_ConfigEditor::setParameter('site_name_first', 'false');

//ПЕРЕХОД ПОСЛЕ НАЖАТИЯ "СОХРАНИТЬ"
if(!empty($_POST["save_config"]))
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	
	//Для возможности работы с настройками только определенной группы
	$need_config_group = 0;//Работаем со всеми настройками
	if( isset($_POST['need_config_group']) )
	{
		$need_config_group = (int)$_POST['need_config_group'];
	}
	if( $need_config_group < 0 )
	{
		$need_config_group = 0;
	}
	
	
    //Получаем перечень всех параметров:
	$config_parameters_query = $db_link->prepare("SELECT * FROM `config_items`;");
    $config_parameters_query->execute();
    while( $item = $config_parameters_query->fetch() )
    {
		//Если работаем только с настройками определенной группы, то, настройки других групп пропускаем
		if( $need_config_group > 0 && $item['config_group'] != $need_config_group )
		{
			continue;
		}
		
		//Если это параметр, который должен содержать ID строки из мультиязычности, то, здесь в $value содержится перевод на текущем языке
		$value = trim($_POST[$item["name"]]);
		
		
		if( $item["htmlentities"] == 1 )
		{
			$value = htmlentities($value);
		}
		
		$value = str_replace("'", "&#039;", $value);
		$value = str_replace('"', "&quot;", $value);
		if( $item["name"] == "epc_head_office_address" || $item["name"] == "epc_global_locations_countries" )
		{
			$value = str_replace(array("\r\n", "\r", "\n"), "\\n", $value);
			$value = str_replace("\t", " ", $value);
		}
		else
		{
			$value = str_replace(array("\r", "\n", "\t"), "", $value);
		}
		
		//Предотвращаем запись в config.php операторов начала и конца php-скрипта:
		do
		{
			$value = str_replace('<?', '[CODE]', $value);
		}while( strpos($value, '<?') !== false );
		do
		{
			$value = str_replace('?>', '[/CODE]', $value);
		}while( strpos($value, '?>') !== false );
		
		
		
		//Обработка параметров, которые содержат ID строк из мультиязычности
		if( array_search($item["name"], $translated_items) !== false )
		{
			//Вызов функции сохранения строки в виде перевода на текущий язык панели управления. В ответ вернется ID этой строки, который нужно будет сохранить в config.php
			
			$value = save_custom_translation($_POST[$item["name"]."_lang_str_id"], $value);
		}
		
		
		
        //С некоторыми типами параметров необходимо работать особым образом:
        if($item["type"]=="password")//Для паролей: если передан пустой - оставляем как есть
        {
            if( $value != "" ) DP_ConfigEditor::setParameter($item["name"], $value);
        }
        else if($item["type"]=="checkbox")//Для чекбоксов приводим к булевому типу
        {
            DP_ConfigEditor::setParameter($item["name"], filter_var($value, FILTER_VALIDATE_BOOLEAN));
        }
        else//Для все остальных типов - как есть
        {
            DP_ConfigEditor::setParameter($item["name"], $value);
        }
    }
    
    
    $success_message = translate_str_by_id(2441);
	$need_config_group_arg = "";
	if( $need_config_group > 0 )
	{
		$need_config_group_arg = "&need_config_group=".$need_config_group;
	}
    ?>
    <script>
        location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/control/config?success_message=<?php echo $success_message.$need_config_group_arg; ?>";
    </script>
    <?php
    exit();
}
else//Если нет перехода после нажатия "Сохранить" - выводим форму с настройками
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	// CP pages are eval()'d from core — never use __DIR__ for local requires.
	require_once($_SERVER['DOCUMENT_ROOT']."/content/general_pages/epc_cp_page_frame.php");
	require_once($_SERVER['DOCUMENT_ROOT']."/content/general_pages/epc_cp_page_assets.php");

	$epcCfgAssetVer = epc_cp_page_asset_version() . 'cfg2';
	$epcCfgBackend = isset($DP_Config->backend_dir) ? trim((string) $DP_Config->backend_dir, '/') : 'cp';
	if ($epcCfgBackend === '') {
		$epcCfgBackend = 'cp';
	}
	require_once($_SERVER['DOCUMENT_ROOT']."/".$epcCfgBackend."/content/control/epc_config_edit_meta.php");
	epc_cp_register_page_assets(
		array('/content/general_pages/epc_config_edit_css.php?v=' . rawurlencode($epcCfgAssetVer)),
		array('/' . $epcCfgBackend . '/content/control/epc_config_edit.js?v=' . rawurlencode($epcCfgAssetVer))
	);

	require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий

	$tabs = array();

	//Для возможности работы с настройками ТОЛЬКО определенной группы
	$need_config_group = 0;//Отображаем настройки для всех групп
	if( isset($_GET["need_config_group"]) )
	{
		$need_config_group = (int)$_GET["need_config_group"];
	}
	if( $need_config_group < 0 )
	{
		$need_config_group = 0;
	}

	//Получаем перечнь групп параметров config.php:
	$config_groups_query = $db_link->prepare('SELECT * FROM `config_groups` WHERE `visible` = ? ORDER BY `order` ASC;');
	$config_groups_query->execute(array(1));
	while( $group = $config_groups_query->fetch() )
	{
		//Если работаем с определенной группой, то, остальные пропускам
		if( $need_config_group > 0 && $group["id"] != $need_config_group )
		{
			continue;
		}

		$tabs[(string)$group["id"]] = array("caption"=>translate_str_by_id($group["caption"]), "items"=>array());
	}

	//Получаем перечень всех параметров:
	$config_parameters_query = $db_link->prepare("SELECT * FROM `config_items` WHERE `visible` = ? ORDER BY `order` ASC;");
	$config_parameters_query->execute(array(1));
	while( $item = $config_parameters_query->fetch() )
	{
		//Если работаем с определенной группой, то, остальные пропускам
		if( $need_config_group > 0 && $item["config_group"] != $need_config_group )
		{
			continue;
		}

		if( !isset($tabs[(string)$item["config_group"]]["items"]) )
		{
			$tabs[(string)$item["config_group"]]["items"] = array();
		}

		array_push($tabs[(string)$item["config_group"]]["items"], $item);
	}

	require_once("content/control/get_widget.php");//Скрипт для получения html-кода виджетов различных типов
	?>
	<script>
	function save_config()
	{
		document.forms["save_config_form"].submit();
	}
	</script>

	<div class="col-lg-12 epc-cfg">
		<div class="epc-cfg-hero">
			<h3><i class="fa fa-sliders"></i> <?php echo htmlspecialchars(translate_str_by_id(2113), ENT_QUOTES, 'UTF-8'); ?></h3>
			<p>Site-wide settings that power the control panel and the public storefront. Blue hints mark values customers see or that change storefront behaviour.</p>
			<div class="epc-cfg-legend">
				<span class="epc-cfg-pill"><i class="fa fa-desktop"></i> Frontend — visible on the public site</span>
				<span class="epc-cfg-pill"><i class="fa fa-server"></i> Backend — CP / integrations only</span>
			</div>
		</div>

		<div class="epc-cfg-actions">
			<button type="button" class="btn btn-primary" onclick="save_config();">
				<i class="fa fa-save"></i> <?php echo htmlspecialchars(translate_str_by_id(2114), ENT_QUOTES, 'UTF-8'); ?>
			</button>
			<a class="btn btn-default" href="/<?php echo htmlspecialchars($epcCfgBackend, ENT_QUOTES, 'UTF-8'); ?>">
				<i class="fa fa-home"></i> <?php echo htmlspecialchars(translate_str_by_id(2116), ENT_QUOTES, 'UTF-8'); ?>
			</a>
			<button type="button" class="btn btn-white" onclick="$('.epc-cfg-card .showhide').click();">
				<i class="fa fa-arrows-v"></i> <?php echo htmlspecialchars(translate_str_by_id(5219), ENT_QUOTES, 'UTF-8'); ?>
			</button>
		</div>

		<form method="POST" name="save_config_form">
			<input type="hidden" name="csrf_guard_key" value="<?php echo htmlspecialchars((string)$user_session["csrf_guard_key"], ENT_QUOTES, 'UTF-8'); ?>" />
			<input type="hidden" name="save_config" value="save_config" />
			<input type="hidden" name="need_config_group" value="<?php echo (int)$need_config_group; ?>" />

			<div class="epc-cfg-layout">
				<aside class="epc-cfg-nav" aria-label="Settings groups">
					<div class="epc-cfg-nav__title">Jump to group</div>
					<?php
					foreach ($tabs as $navKey => $navTab) {
						$navMeta = epc_config_group_meta_for((int)$navKey);
						$navCount = isset($navTab['items']) ? count($navTab['items']) : 0;
						?>
						<a href="#epc-cfg-group-<?php echo (int)$navKey; ?>">
							<i class="fa <?php echo htmlspecialchars($navMeta['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i>
							<span><?php echo htmlspecialchars((string)$navTab['caption'], ENT_QUOTES, 'UTF-8'); ?> <small style="opacity:.7;">(<?php echo (int)$navCount; ?>)</small></span>
						</a>
						<?php
					}
					?>
				</aside>

				<div class="epc-cfg-main">
					<?php
					foreach ($tabs as $key => $tab) {
						$groupMeta = epc_config_group_meta_for((int)$key);
						$itemCount = isset($tab['items']) ? count($tab['items']) : 0;
						?>
						<div class="hpanel epc-cfg-card" id="epc-cfg-group-<?php echo (int)$key; ?>">
							<div class="panel-heading hbuilt">
								<div class="panel-tools">
									<a class="showhide"><i class="fa fa-chevron-up"></i></a>
								</div>
								<div class="epc-cfg-card__title">
									<i class="fa <?php echo htmlspecialchars($groupMeta['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i>
									<span><?php echo htmlspecialchars((string)$tab['caption'], ENT_QUOTES, 'UTF-8'); ?> <small style="font-weight:500;color:#64748b;">· <?php echo (int)$itemCount; ?></small></span>
								</div>
								<?php if ($groupMeta['blurb'] !== '') { ?>
									<p class="epc-cfg-card__blurb"><?php echo htmlspecialchars($groupMeta['blurb'], ENT_QUOTES, 'UTF-8'); ?></p>
								<?php } ?>
								<?php if ($groupMeta['frontend'] !== '') { ?>
									<div class="epc-cfg-card__frontend"><strong>Effect on frontend:</strong> <?php echo htmlspecialchars($groupMeta['frontend'], ENT_QUOTES, 'UTF-8'); ?></div>
								<?php } ?>
							</div>
							<div class="panel-body">
								<?php
								for ($i = 0; $i < $itemCount; $i++) {
									$item = $tab['items'][$i];
									$itemName = (string)$item['name'];
									$current_value = $DP_Config->{$itemName};
									if ($itemName === 'epc_head_office_address' || $itemName === 'epc_global_locations_countries') {
										$current_value = str_replace('\\n', "\n", $current_value);
									}

									if (array_search($itemName, $translated_items) !== false) {
										?>
										<input type="hidden" name="<?php echo htmlspecialchars($itemName . '_lang_str_id', ENT_QUOTES, 'UTF-8'); ?>" value="<?php echo htmlspecialchars((string)$current_value, ENT_QUOTES, 'UTF-8'); ?>" />
										<?php
										$current_value = translate_str_by_id($current_value);
									}

									$widget = get_widget($item['type'], $itemName, $current_value, json_decode($item['options'], true));
									$epc_config_label = epc_config_item_label($itemName, translate_str_by_id($item['caption']));
									$frontendEffect = epc_config_item_frontend_effect($itemName);
									$effectIsBackend = ($frontendEffect !== '' && (
										stripos($frontendEffect, 'not a storefront') !== false
										|| stripos($frontendEffect, 'backend only') !== false
										|| stripos($frontendEffect, 'CP /') !== false
										|| stripos($frontendEffect, 'CP update') !== false
										|| stripos($frontendEffect, 'never expose') !== false
										|| stripos($frontendEffect, 'not shown on storefront') !== false
										|| stripos($frontendEffect, 'CP folder only') !== false
										|| stripos($frontendEffect, 'credential') !== false
									));
									?>
									<div class="form-group epc-cfg-field">
										<label for="<?php echo htmlspecialchars($itemName, ENT_QUOTES, 'UTF-8'); ?>" class="epc-cfg-field__label control-label">
											<?php echo htmlspecialchars($epc_config_label, ENT_QUOTES, 'UTF-8'); ?>
											<?php
											if (isset($item['hint']) && (int)$item['hint'] !== 0) {
												?>
												<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo htmlentities(translate_str_by_id($item['hint']), ENT_QUOTES, 'UTF-8'); ?>');"><i class="fa fa-info"></i></button>
												<?php
											}
											?>
										</label>
										<div class="epc-cfg-field__ctrl">
											<?php echo $widget; ?>
											<?php if ($frontendEffect !== '') { ?>
												<span class="epc-cfg-effect<?php echo $effectIsBackend ? ' is-backend' : ''; ?>">
													<?php echo $effectIsBackend ? 'Backend: ' : 'Frontend: '; ?>
													<?php echo htmlspecialchars($frontendEffect, ENT_QUOTES, 'UTF-8'); ?>
												</span>
											<?php } ?>
											<?php
											if ($itemName === 'epc_global_locations_countries') {
												?>
												<div style="margin-top:8px; padding:10px 12px; border:1px solid #d9e2ef; border-radius:8px; background:#f8fafc; color:#475569; font-size:12px; line-height:1.55;">
													<strong>How to enter multiple locations:</strong><br>
													Write one complete location block, leave one blank line, then write the next location block.<br>
													Example:<br>
													<code>UAE - Dubai Head Office<br>Address: Dubai, United Arab Emirates<br>Contact person: Sales Manager<br>Phone: +971-567607011<br>Email: partsdoc2025@gmail.com<br><br>Oman - Muscat Location<br>Address: Muscat, Oman<br>Contact person: Branch Coordinator<br>Phone: +968-XXXXXXX</code>
												</div>
												<?php
											}
											if ($itemName === 'epc_head_office_address') {
												?>
												<div style="margin-top:8px; color:#64748b; font-size:12px;">You can use multiple lines for full street, city, country, and office notes.</div>
												<?php
											}
											if ($itemName === 'umapi_api_key') {
												?>
												<div id="epc_umapi_cp_status" style="margin-top:10px; padding:12px 14px; border:1px solid #d9e2ef; border-radius:8px; background:#f8fafc; color:#334155; font-size:13px;">
													<strong>Epart catalog connection:</strong> checking...
												</div>
												<script>
												(function(){
													var box = document.getElementById('epc_umapi_cp_status');
													if(!box){ return; }
													function timeText(ts){
														if(!ts){ return 'never'; }
														var date = new Date(ts * 1000);
														return date.toLocaleString();
													}
													fetch('/api/umapi_proxy.php?action=status', {credentials:'same-origin', cache:'no-store'})
														.then(function(response){ return response.json(); })
														.then(function(data){
															var ok = data && data.connected;
															var counts = data && data.counts ? data.counts : {};
															var sections = data && data.sections ? data.sections : {};
															var usage = data && data.usage ? data.usage : {};
															var usageBar = '';
															if (usage && usage.daily_limit) {
																var pct = usage.pct_used || 0;
																var barColor = pct >= 100 ? '#ef4444' : (pct >= 80 ? '#f59e0b' : '#22c55e');
																usageBar =
																	'<br><strong>Daily API usage:</strong> ' + (usage.today_live || 0) + ' / ' + usage.daily_limit +
																	' live calls (' + pct + '%)' +
																	(usage.remaining != null ? ', remaining ' + usage.remaining : '') +
																	'<div style="margin-top:6px; height:8px; background:#e2e8f0; border-radius:4px; overflow:hidden;">' +
																	'<div style="width:' + Math.min(100, pct) + '%; height:100%; background:' + barColor + ';"></div></div>';
																if (usage.by_source_today && usage.by_source_today.length) {
																	usageBar += '<br><strong>Top sources today:</strong> ' + usage.by_source_today.slice(0, 5).map(function(x){
																		return x.source + ' (' + x.live + ' live)';
																	}).join(', ');
																}
																if (usage.by_action_today && usage.by_action_today.length) {
																	usageBar += '<br><strong>By action today:</strong> ' + usage.by_action_today.slice(0, 6).map(function(x){
																		return x.action + ' (' + x.live + ' live)';
																	}).join(', ');
																}
																usageBar += '<br><strong>Full report:</strong> /epc-umapi-daily-report.php (tech key required)';
															}
															box.style.borderColor = ok ? '#bbf7d0' : '#fed7aa';
															box.style.background = ok ? '#f0fdf4' : '#fff7ed';
															box.innerHTML =
																'<strong>Epart catalog connection:</strong> ' + (ok ? '<span style="color:#15803d;">Connected</span>' : '<span style="color:#b45309;">Not connected / using saved data if available</span>') +
																'<br><strong>Status:</strong> ' + (data && data.message ? data.message : 'No status yet') +
																'<br><strong>Last check:</strong> ' + timeText(data && data.last_checked) +
																usageBar +
																'<br><strong>Saved catalog:</strong> Manufacturers ' + (counts.manufacturers || 0) + ', Brands ' + (counts.brands || 0) + ', Models ' + (counts.models || 0) + ', Modifications ' + (counts.modifications || 0) + ', VINs ' + (counts.vins || 0) +
																(data.cache_rows ? ('<br><strong>API cache rows:</strong> ' + data.cache_rows) : '') +
																'<br><strong>Manufacturer sections:</strong> Passenger ' + (sections.passenger || 0) + ', Commercial ' + (sections.commercial || 0) + ', Motorbike ' + (sections.motorbike || 0) +
																(data.offline_ready ? '<br><strong>Offline mode:</strong> <span style="color:#15803d;">Saved data available</span>' : '<br><strong>Offline mode:</strong> <span style="color:#b91c1c;">Not ready — run warm script</span>') +
																((data.action_required && data.action_required.length) ? ('<br><strong style="color:#b45309;">Action required:</strong><br>' + data.action_required.map(function(x){ return '• ' + x; }).join('<br>')) : '');
														})
														.catch(function(){
															box.innerHTML = '<strong>Epart catalog connection:</strong> status unavailable';
														});
												})();
												</script>
												<div id="epc_crossbase_cp_status" style="margin-top:10px; padding:12px 14px; border:1px solid #d9e2ef; border-radius:8px; background:#f8fafc; color:#334155; font-size:13px;">
													<strong>Cross-reference API:</strong> checking...
												</div>
												<script>
												(function(){
													var box = document.getElementById('epc_crossbase_cp_status');
													if(!box){ return; }
													function timeText(ts){
														if(!ts){ return 'never'; }
														var date = new Date(ts * 1000);
														return date.toLocaleString();
													}
													fetch('/api/crossbase_status.php?sample=C110J', {credentials:'same-origin', cache:'no-store'})
														.then(function(response){ return response.json(); })
														.then(function(data){
															var ok = data && data.connected;
															box.style.borderColor = ok ? '#bbf7d0' : '#fed7aa';
															box.style.background = ok ? '#f0fdf4' : '#fff7ed';
															box.innerHTML =
																'<strong>Cross-reference API:</strong> ' + (ok ? '<span style="color:#15803d;">Connected</span>' : (data.used_stale_cache ? '<span style="color:#b45309;">Offline — using saved cache</span>' : '<span style="color:#b45309;">Not connected</span>')) +
																'<br><strong>Data source:</strong> Epart catalog cross-reference service' +
																'<br><strong>Status:</strong> ' + (data && data.message ? data.message : 'No status yet') +
																'<br><strong>HTTP code:</strong> ' + (data && data.status_code ? data.status_code : 0) +
																'<br><strong>Sample check:</strong> ' + (data && data.sample ? data.sample : 'C110J') + ' - ' + (data && data.references_total ? data.references_total : 0) + ' references, ' + (data && data.rows_parsed ? data.rows_parsed : 0) + ' rows parsed' +
																'<br><strong>Response time:</strong> ' + (data && data.response_ms ? data.response_ms : 0) + ' ms' +
																'<br><strong>Last check:</strong> ' + timeText(data && data.last_checked) +
																(data.cache ? ('<br><strong>HTML cache:</strong> ' + (data.cache.files_total || 0) + ' files (fresh ' + (data.cache.files_fresh || 0) + ', stale ' + (data.cache.files_stale || 0) + ')') : '') +
																(data.cp_cross_rows !== undefined ? ('<br><strong>CP crosses saved:</strong> ' + data.cp_cross_rows + (data.local_crosses_on ? ' (local crosses ON)' : ' (local crosses OFF)')) : '') +
																(data.offline_ready ? '<br><strong>Offline mode:</strong> <span style="color:#15803d;">Cache / local crosses available</span>' : '<br><strong>Offline mode:</strong> <span style="color:#b91c1c;">Not ready — warm cache + sync crosses</span>') +
																((data.action_required && data.action_required.length) ? ('<br><strong style="color:#b45309;">Action required:</strong><br>' + data.action_required.map(function(x){ return '• ' + x; }).join('<br>')) : '');
														})
														.catch(function(){
															box.innerHTML = '<strong>Cross-reference API:</strong> status unavailable';
														});
												})();
												</script>
												<div id="epc_laximo_cp_status" style="margin-top:10px; padding:12px 14px; border:1px solid #d9e2ef; border-radius:8px; background:#f8fafc; color:#334155; font-size:13px;">
													<strong>Laximo OEM Catalog (CAT + DOC):</strong> checking...
												</div>
												<script>
												(function(){
													var box = document.getElementById('epc_laximo_cp_status');
													if(!box){ return; }
													function timeText(ts){
														if(!ts){ return 'never'; }
														var date = new Date(ts * 1000);
														return date.toLocaleString();
													}
													fetch('/api/laximo_proxy.php?action=status', {credentials:'same-origin', cache:'no-store'})
														.then(function(response){ return response.json(); })
														.then(function(data){
															var catOk = data && data.services && data.services.cat && data.services.cat.connected;
															var docOk = data && data.services && data.services.doc && data.services.doc.connected;
															box.style.borderColor = catOk ? '#bbf7d0' : '#fed7aa';
															box.style.background = catOk ? '#f0fdf4' : '#fff7ed';
															box.innerHTML =
																'<strong>Laximo OEM Catalog:</strong> ' +
																(catOk ? '<span style="color:#15803d;">CAT Connected</span>' : '<span style="color:#b45309;">CAT Not Connected</span>') +
																' | ' +
																(docOk ? '<span style="color:#15803d;">DOC Connected</span>' : '<span style="color:#b45309;">DOC Not Connected</span>') +
																'<br><strong>CAT Login:</strong> ' + (data.cat_login || 'not set') +
																' | <strong>DOC Login:</strong> ' + (data.doc_login || 'not set') +
																'<br><strong>Status:</strong> ' + (data.message || 'unknown') +
																'<br><strong>Saved catalogs:</strong> ' + (data.services && data.services.cat ? data.services.cat.catalogs_count : 0) + ' brands' +
																'<br><strong>API cache rows:</strong> ' + (data.cache_rows || 0) +
																'<br><strong>Last check:</strong> ' + timeText(data.last_checked) +
																(data.offline_ready ? '<br><strong>Offline mode:</strong> <span style="color:#15803d;">Saved data available</span>' : '<br><strong>Offline mode:</strong> <span style="color:#b91c1c;">Not ready — sync required</span>') +
																'<br><strong>Service docs:</strong> <a href="https://doc.laximo.ru/en/home" target="_blank">doc.laximo.ru</a>' +
																'<br><strong>Demo:</strong> <a href="https://wsdemo.laximo.ru/index.php?task=catalogs" target="_blank">CAT demo</a> | <a href="https://wsdemo.laximo.ru/index.php?task=aftermarket" target="_blank">DOC demo</a>' +
																'<br><small style="color:#64748b;">Config: laximo_cat_login, laximo_cat_key, laximo_doc_login, laximo_doc_key in DP_Config</small>';
														})
														.catch(function(){
															box.innerHTML = '<strong>Laximo OEM Catalog:</strong> status unavailable (API proxy not accessible)';
														});
												})();
												</script>
												<?php
											}
											?>
										</div>
									</div>
									<?php
								}
								?>
							</div>
						</div>
						<?php
					}
					?>

					<div class="epc-cfg-sticky-save">
						<div class="epc-cfg-sticky-save__hint">Changes write to <code>config.php</code>. Frontend-facing values apply on the next page load.</div>
						<button type="button" class="btn btn-primary" onclick="save_config();">
							<i class="fa fa-save"></i> <?php echo htmlspecialchars(translate_str_by_id(2114), ENT_QUOTES, 'UTF-8'); ?>
						</button>
					</div>
				</div>
			</div>
		</form>
	</div>
	<?php
}//else - если не было перехода после нажатия "Сохранить"
?>