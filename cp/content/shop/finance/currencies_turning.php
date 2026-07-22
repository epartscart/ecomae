<?php
/**
 * Страничный скрипт для нстройки курсов валют
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
if( !empty($_POST["save_action"]) )
{
	// -------------------------------------------------------------------------------
	//Защита от CSRF-атак
	require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
	// -------------------------------------------------------------------------------
	
	if( $_POST["save_action"] == "general" )
	{
		$s_page = (int)$_POST["s_page"];
		
		$no_error = true;//Флаг - сохранение выполнено без ошибок
	
		//Получаем все валюты, кроме главной (т.е. курс главной валюты мы не настраиваем, он равен 1)
		$currencies_query = $db_link->prepare("SELECT * FROM `shop_currencies`");
		$currencies_query->execute();
		while( $currency = $currencies_query->fetch() )
		{
			//Этой валюты не было на странице
			if( empty($_POST["rate_".$currency["iso_code"]]) )
			{
				continue;
			}
			
			//Сохраняем курсы всех валют
			if( $db_link->prepare("UPDATE `shop_currencies` SET `rate` = ? WHERE `iso_code` = ?;")->execute( array($_POST["rate_".$currency["iso_code"]], $currency["iso_code"]) ) != true )
			{
				$no_error = false;
			}
		}
		
		
		if($no_error)
		{
			$success_message = translate_str_by_id(3276);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/nastrojka-kursov-valyut?success_message=<?php echo $success_message; ?>&s_page=<?php echo $s_page; ?>";
			</script>
			<?php
			exit;
		}
		else
		{
			$error_message = translate_str_by_id(3277);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/nastrojka-kursov-valyut?error_message=<?php echo $error_message; ?>&s_page=<?php echo $s_page; ?>";
			</script>
			<?php
			exit;
		}
	}
	else if( $_POST["save_action"] == "available_currencies" )
	{
		$currencies = json_decode($_POST["currencies_list"], true);
		$available = (int)$_POST["available"];
		$s_page = (int)$_POST["s_page"];
		
		if( array_search($DP_Config->shop_currency, $currencies) !== false )
		{
			$error_message = translate_str_by_id(3278);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/nastrojka-kursov-valyut?error_message=<?php echo $error_message; ?>";
			</script>
			<?php
			exit;
		}
		
		
		$currencies_for_sql = "";
		$binding_values = array();
		$currencies_list = json_decode($_POST["currencies_list"], true);
		for( $i=0; $i<count($currencies_list); $i++)
		{
			if( $i > 0 )
			{
				$currencies_for_sql = $currencies_for_sql.",";
			}
			$currencies_for_sql = $currencies_for_sql."?";
			array_push($binding_values, $currencies_list[$i]);
		}
		
		
		array_unshift($binding_values, $available);
		

		if( $db_link->prepare("UPDATE `shop_currencies` SET `available` = ? WHERE `iso_code` IN (".$currencies_for_sql.");")->execute($binding_values) != true )
		{
			$error_message = translate_str_by_id(3279);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/nastrojka-kursov-valyut?error_message=<?php echo $error_message; ?>&s_page=<?php echo $s_page; ?>";
			</script>
			<?php
			exit;
		}
		else
		{
			$success_message = translate_str_by_id(2157);
			?>
			<script>
				location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir; ?>/shop/finance/nastrojka-kursov-valyut?success_message=<?php echo $success_message; ?>&s_page=<?php echo $s_page; ?>";
			</script>
			<?php
			exit;
		}
	}
}
else
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
	?>
	
	<?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
	
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" href="javascript:void(0);" onclick="document.forms['rates_save_form'].submit();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/save.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3280); ?></div>
				</a>
				
				
				
				<a class="panel_a" href="javascript:void(0);" onclick="set_available_currencies(true, false);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/on.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2644); ?></div>
				</a>
				
				
				
				<a class="panel_a" href="javascript:void(0);" onclick="set_available_currencies(false, false);">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(3281); ?></div>
				</a>
				
				
				
				<a class="panel_a" href="javascript:void(0);" onclick="epcLiveRatesPreview();">
					<div class="panel_a_img" style="background:#0ea5e9;border-radius:8px;width:48px;height:48px;margin:0 auto 6px;display:flex;align-items:center;justify-content:center;color:#fff;font-size:20px;line-height:1;">↻</div>
					<div class="panel_a_caption">Fetch live rates</div>
				</a>
				
				
				
				<a class="panel_a" href="javascript:void(0);" onclick="epcLiveRatesApply(false);">
					<div class="panel_a_img" style="background:#059669;border-radius:8px;width:48px;height:48px;margin:0 auto 6px;display:flex;align-items:center;justify-content:center;color:#fff;font-size:18px;line-height:1;">✓</div>
					<div class="panel_a_caption">Apply live rates</div>
				</a>
				
				
				
				

				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Live FX vs main currency
			</div>
			<div class="panel-body">
				<p style="margin:0 0 10px;color:#64748b;font-size:13px;">
					Fetches mid-market rates against your main shop currency
					(<strong><?php
						$__mainIso = (string)$DP_Config->shop_currency;
						$__mainName = $__mainIso;
						try {
							$__mq = $db_link->prepare('SELECT `iso_name` FROM `shop_currencies` WHERE `iso_code` = ? LIMIT 1');
							$__mq->execute(array($__mainIso));
							$__mn = $__mq->fetchColumn();
							if ($__mn) { $__mainName = $__mn . ' (' . $__mainIso . ')'; }
						} catch (Throwable $e) {}
						echo htmlspecialchars($__mainName, ENT_QUOTES, 'UTF-8');
					?></strong>).
					Source priority: ExchangeRate-API (open.er-api.com) → ExchangeRate-API v4 → FloatRates.
					Shop rate = how many main-currency units equal <em>1</em> unit of the listed currency (same model as this page).
				</p>
				<div id="epc_live_rates_status" class="alert alert-info" style="display:none;margin-bottom:12px;"></div>
				<div class="table-responsive" id="epc_live_rates_wrap" style="display:none;">
					<table class="table table-condensed table-striped" id="epc_live_rates_table">
						<thead>
							<tr>
								<th>ISO</th>
								<th>Code</th>
								<th>Current rate</th>
								<th>Live rate</th>
								<th>Diff %</th>
								<th>Main</th>
							</tr>
						</thead>
						<tbody></tbody>
					</table>
					<p style="margin:8px 0 0;font-size:12px;color:#64748b;">
						<button type="button" class="btn btn-sm btn-primary" onclick="epcLiveRatesFillForm();">Fill form fields from live rates</button>
						<button type="button" class="btn btn-sm btn-success" onclick="epcLiveRatesApply(true);">Save live rates to database</button>
						<span id="epc_live_rates_meta" style="margin-left:10px;"></span>
					</p>
				</div>
			</div>
		</div>
	</div>


	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				Daily auto update (night)
			</div>
			<div class="panel-body">
				<p style="margin:0 0 12px;color:#64748b;font-size:13px;">
					Automatically fetch and apply live FX rates once per night.
					Uses the same providers as above. Safe to hit every minute — it only applies when the nightly window is due.
				</p>
				<div id="epc_fx_sched_status" class="alert alert-info" style="display:none;margin-bottom:12px;"></div>
				<div class="row">
					<div class="col-sm-3">
						<label style="font-weight:600;">Enabled</label>
						<div>
							<label class="checkbox-inline" style="padding-left:0;">
								<input type="checkbox" id="epc_fx_sched_enabled" value="1" checked>
								Daily auto update
							</label>
						</div>
					</div>
					<div class="col-sm-3">
						<label for="epc_fx_sched_timezone" style="font-weight:600;">Timezone</label>
						<input type="text" class="form-control input-sm" id="epc_fx_sched_timezone" value="Asia/Dubai">
					</div>
					<div class="col-sm-2">
						<label for="epc_fx_sched_hour" style="font-weight:600;">Night hour</label>
						<select class="form-control input-sm" id="epc_fx_sched_hour">
							<?php for ($__h = 0; $__h <= 23; $__h++): ?>
								<option value="<?php echo $__h; ?>"<?php echo $__h === 2 ? ' selected' : ''; ?>><?php echo sprintf('%02d:00', $__h); ?></option>
							<?php endfor; ?>
						</select>
					</div>
					<div class="col-sm-4">
						<label style="font-weight:600;">Actions</label>
						<div>
							<button type="button" class="btn btn-sm btn-primary" onclick="epcFxSchedSave();">Save schedule</button>
							<button type="button" class="btn btn-sm btn-success" onclick="epcFxSchedRunNow();">Run now</button>
						</div>
					</div>
				</div>
				<div style="margin-top:14px;font-size:13px;color:#334155;line-height:1.55;">
					<div><strong>Local now:</strong> <span id="epc_fx_sched_local_now">—</span></div>
					<div><strong>Due now:</strong> <span id="epc_fx_sched_due">—</span></div>
					<div><strong>Next window:</strong> <span id="epc_fx_sched_next">—</span></div>
					<div><strong>Last run:</strong> <span id="epc_fx_sched_last">—</span></div>
					<div style="margin-top:8px;color:#64748b;font-size:12px;">
						Cron URL (token-gated; every minute or <code>0 2 * * *</code>):
						<code id="epc_fx_sched_cron_url" style="display:inline-block;margin-top:4px;word-break:break-all;">https://www.epartscart.com/epc-currency-live-rates-cron.php?token=epartscart-deploy-2026</code>
						<br>Also hooked into the existing every-minute pyprices <code>cron_crutch.php</code> tick.
					</div>
				</div>
			</div>
		</div>
	</div>
	
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(3282); ?>
			</div>
			<div class="panel-body">
				<div class="table-responsive">
					<form method="POST" name="rates_save_form">
						<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
						<input type="hidden" value="general" name="save_action" />
						<input type="hidden" value="<?php echo (int)$_GET["s_page"]; ?>" name="s_page" />
						<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
							<thead> 
								<tr> 
									<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();"/></th>
									<th>ID</th>
									<th><?php echo translate_str_by_id(2533); ?> ISO</th>
									<th><?php echo translate_str_by_id(2277); ?> ISO</th>
									<th><?php echo translate_str_by_id(3283); ?></th>
									<th><?php echo translate_str_by_id(3284); ?></th>
									<th><?php echo translate_str_by_id(3285); ?></th>
								</tr>
							</thead>
							<tbody>
							<?php
							
							//Массивы для JS с id элементов и с чекбоксами элементов
							$for_js = "var elements_array = new Array();\n";//Выведем массив для JS с чекбоксами элементов
							$for_js = $for_js."var elements_id_array = new Array();\n";//Выведем массив для JS с ID элементов
							

							$elements_query = $db_link->prepare("SELECT SQL_CALC_FOUND_ROWS * FROM `shop_currencies` ORDER BY `order`;");
							$elements_query->execute();
							
							$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
							$elements_count_rows_query->execute();
							$elements_count_rows = $elements_count_rows_query->fetchColumn();
							
							//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
							//---------------------------------------------------------------------------------------------->
							//Определяем количество страниц для вывода:
							$p = $DP_Config->list_page_limit;//Штук на страницу
							$count_pages = (int)($elements_count_rows / $p);//Количество страниц
							if($elements_count_rows%$p)//Если остались еще элементы
							{
								$count_pages++;
							}
							//Определяем, с какой страницы начать вывод:
							$s_page = 0;
							if(!empty($_GET['s_page']))
							{
								$s_page = $_GET['s_page'];
							}
							//----------------------------------------------------------------------------------------------|
							
							
							for($i=0, $d=0; $i<$elements_count_rows && $d<$p; $i++, $d++)
							{
								$element_record = $elements_query->fetch();
								
								//Пропускаем нужное количество блоков в соответствии с номером требуемой страницы
								if($i < $s_page*$p)
								{
									$d--;
									continue;
								}
								
								//Для Javascript
								$for_js = $for_js."elements_array[elements_array.length] = \"checked_".$element_record["iso_code"]."\";\n";//Добавляем элемент для JS
								$for_js = $for_js."elements_id_array[elements_id_array.length] = ".$element_record["iso_code"].";\n";//Добавляем элемент для JS
								
							?>
								<tr>
									<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["iso_code"]; ?>');" id="checked_<?php echo $element_record["iso_code"]; ?>" name="checked_<?php echo $element_record["iso_code"]; ?>"/></td>
									<td><?php echo $element_record["id"]; ?></td>
									<td><?php echo $element_record["iso_code"]; ?></td>
									<td><?php echo $element_record["iso_name"]; ?></td>
									<td>
										<input class="form-control" type="text" name="rate_<?php echo $element_record["iso_code"]; ?>" value="<?php echo $element_record["rate"]; ?>" style="width:100px;" />
									</td>
									<td>
										<?php
										if($DP_Config->shop_currency == $element_record["iso_code"])
										{
											?>
											<img title="<?php echo translate_str_by_id(3286); ?>" src="/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/images/star.png" style="width:18px;border-radius:50%;" />
											<?php
										}
										?>
									</td>
									
									<td>
									<?php 
										if($element_record["available"] == 1) 
										{
											?>
											<img class="a_col_img" src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/on.png" onclick="set_available_currencies(false, [<?php echo $element_record["iso_code"]; ?>] );" style="cursor:pointer;" />
											<?php
										}
										else
										{
											?>
											<img class="a_col_img" src="/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/off.png" onclick="set_available_currencies(true, [<?php echo $element_record["iso_code"]; ?>] );" style="cursor:pointer;" />
											<?php
										}
									?>
								</td>
									
								</tr>
							<?php
							}//for
							?>
							</tbody>
						</table>
					</form>
				</div>
				
				
				<?php
				//START ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
				if( $count_pages > 1 )
				{
					?>
					<div class="row">
						<div class="col-lg-12 text-center">
							<div class="dataTables_paginate paging_simple_numbers">
								<ul class="pagination">
								<?php
								for($i=0; $i < $count_pages; $i++)
								{
									//Класс первой страницы
									$previous = "";
									if($i == 0) $previous = "previous";
									
									//Класс последней страницы
									$next = "";
									if($i == $count_pages-1) $next = "next";
									
									if($i == $s_page)//Текущая страница
									{
										?>
										<li class="paginate_button active <?php echo $previous; ?> <?php echo $next; ?>"><a href="javascript:void(0);"><?php echo $i; ?></a></li>
										<?php
									}
									else
									{
										?>
										<li class="paginate_button <?php echo $previous; ?> <?php echo $next; ?>"><a href="<?php echo "/".$DP_Config->backend_dir."/shop/finance/nastrojka-kursov-valyut?s_page=$i"; ?>"><?php echo $i; ?></a></li>
										<?php
									}
								}
								?>
								</ul>
							</div>
						</div>
					</div>
				<?php
				}
				//END ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
				?>
				
				
			</div>
		</div>
	</div>
	
    
	
	
	
	
	
	
	
	
	
	
	
	<form name="available_currency_form" method="POST">
		<input type="hidden" name="save_action" value="available_currencies" />
		<input type="hidden" id="currencies_list" name="currencies_list" value="" />
		<input type="hidden" id="available" name="available" value="" />
		<input type="hidden" name="s_page" value="<?php echo (int)$_GET["s_page"]; ?>" />
		<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
	</form>
	<script>
	// ---------------------------------------------------------
	function set_available_currencies(available, currencies)
	{
		if(available == true)
		{
			document.getElementById("available").value = "1";
		}
		else
		{
			document.getElementById("available").value = "0";
		}
		
		
		if(currencies == false)
		{
			currencies = getCheckedElements();
			
			if(currencies.length == 0)
			{
				alert("<?php echo translate_str_by_id(3287); ?>");
				return;
			}
		}
		
		
		
		document.getElementById("currencies_list").value = JSON.stringify(currencies);
		
		
		document.forms["available_currency_form"].submit();
	}
	// ---------------------------------------------------------

	var EPC_LIVE_RATES_AJAX = "/<?php echo $DP_Config->backend_dir; ?>/content/shop/finance/ajax_currency_live_rates.php";
	var EPC_LIVE_CSRF = "<?php echo htmlspecialchars((string)$user_session['csrf_guard_key'], ENT_QUOTES, 'UTF-8'); ?>";
	var epcLiveRatesLast = null;

	function epcLiveRatesSetStatus(kind, text) {
		var el = document.getElementById("epc_live_rates_status");
		if (!el) return;
		el.style.display = "block";
		el.className = "alert alert-" + (kind || "info");
		el.textContent = text || "";
	}

	function epcLiveRatesPreview() {
		epcLiveRatesSetStatus("info", "Fetching live FX rates…");
		var wrap = document.getElementById("epc_live_rates_wrap");
		if (wrap) wrap.style.display = "none";
		fetch(EPC_LIVE_RATES_AJAX + "?action=preview", { credentials: "same-origin" })
			.then(function (r) { return r.json(); })
			.then(function (j) {
				if (!j || !j.ok) {
					epcLiveRatesSetStatus("danger", (j && j.error) ? j.error : "Failed to fetch live rates");
					return;
				}
				epcLiveRatesLast = j;
				epcLiveRatesRender(j);
				epcLiveRatesSetStatus("success", "Live rates loaded vs " + (j.base_alpha || "main") + " · " + (j.provider || ""));
			})
			.catch(function () {
				epcLiveRatesSetStatus("danger", "Network error fetching live rates");
			});
	}

	function epcLiveRatesRender(j) {
		var wrap = document.getElementById("epc_live_rates_wrap");
		var tbody = document.querySelector("#epc_live_rates_table tbody");
		var meta = document.getElementById("epc_live_rates_meta");
		if (!wrap || !tbody) return;
		var html = "";
		(j.rows || []).forEach(function (row) {
			var live = row.has_live ? String(row.live_rate) : "—";
			var diff = (row.diff_pct === null || row.diff_pct === undefined) ? "—" : (row.diff_pct + "%");
			var diffColor = "";
			if (typeof row.diff_pct === "number") {
				if (Math.abs(row.diff_pct) >= 1) diffColor = "color:#b45309;font-weight:600;";
				if (Math.abs(row.diff_pct) >= 5) diffColor = "color:#b91c1c;font-weight:700;";
			}
			html += "<tr>"
				+ "<td>" + String(row.iso_code || "") + "</td>"
				+ "<td><strong>" + String(row.iso_name || "") + "</strong></td>"
				+ "<td>" + String(row.current_rate) + "</td>"
				+ "<td>" + live + "</td>"
				+ "<td style=\"" + diffColor + "\">" + diff + "</td>"
				+ "<td>" + (row.is_main ? "★" : "") + "</td>"
				+ "</tr>";
		});
		tbody.innerHTML = html || "<tr><td colspan=\"6\">No currencies</td></tr>";
		wrap.style.display = "block";
		if (meta) {
			meta.textContent = "As of: " + (j.as_of || "—") + " · fetched " + new Date((j.fetched_at || 0) * 1000).toLocaleString();
		}
	}

	function epcLiveRatesFillForm() {
		if (!epcLiveRatesLast || !epcLiveRatesLast.rows) {
			alert("Fetch live rates first.");
			return;
		}
		var filled = 0;
		epcLiveRatesLast.rows.forEach(function (row) {
			if (!row.has_live && !row.is_main) return;
			var name = "rate_" + row.iso_code;
			var input = document.querySelector('input[name="' + name + '"]');
			if (!input) return;
			input.value = row.is_main ? "1" : String(row.live_rate);
			input.style.background = "#ecfdf5";
			filled++;
		});
		epcLiveRatesSetStatus("success", "Filled " + filled + " rate fields from live data. Click Save rates to store them, or use Save live rates to database.");
	}

	function epcLiveRatesApply(reloadAfter) {
		if (!confirm("Apply live FX rates to the database for all currencies (main currency stays 1)?")) {
			return;
		}
		epcLiveRatesSetStatus("info", "Saving live rates…");
		var body = new FormData();
		body.append("action", "apply");
		body.append("csrf_guard_key", EPC_LIVE_CSRF);
		fetch(EPC_LIVE_RATES_AJAX, { method: "POST", credentials: "same-origin", body: body })
			.then(function (r) { return r.json(); })
			.then(function (j) {
				if (!j || !j.ok) {
					epcLiveRatesSetStatus("danger", (j && (j.message || j.error)) ? (j.message || j.error) : "Apply failed");
					return;
				}
				epcLiveRatesSetStatus("success", "Updated " + j.updated + " currencies from " + (j.provider || "live FX") + ". Skipped " + j.skipped + ".");
				if (reloadAfter) {
					setTimeout(function () { location.reload(); }, 900);
				} else {
					epcLiveRatesPreview();
				}
			})
			.catch(function () {
				epcLiveRatesSetStatus("danger", "Network error applying live rates");
			});
	}

	function epcFxSchedSetStatus(kind, text) {
		var el = document.getElementById("epc_fx_sched_status");
		if (!el) return;
		el.style.display = "block";
		el.className = "alert alert-" + (kind || "info");
		el.textContent = text || "";
	}

	function epcFxSchedRender(s) {
		if (!s) return;
		var en = document.getElementById("epc_fx_sched_enabled");
		var tz = document.getElementById("epc_fx_sched_timezone");
		var hour = document.getElementById("epc_fx_sched_hour");
		if (en) en.checked = !!s.enabled;
		if (tz) tz.value = s.timezone || "Asia/Dubai";
		if (hour) hour.value = String(s.hour != null ? s.hour : 2);
		var ln = document.getElementById("epc_fx_sched_local_now");
		var due = document.getElementById("epc_fx_sched_due");
		var next = document.getElementById("epc_fx_sched_next");
		var last = document.getElementById("epc_fx_sched_last");
		if (ln) ln.textContent = (s.local_now || "—") + " (" + (s.timezone || "") + ")";
		if (due) due.textContent = s.due ? "Yes — will run on next cron tick" : "No";
		if (next) next.textContent = s.next_window || "—";
		if (last) {
			var when = s.last_run_at ? new Date(s.last_run_at * 1000).toLocaleString() : "never";
			var st = s.last_status || "—";
			var msg = s.last_message || "";
			var prov = s.last_provider ? (" · " + s.last_provider) : "";
			last.textContent = when + " · " + st + prov + (msg ? (" · " + msg) : "");
		}
	}

	function epcFxSchedLoad() {
		fetch(EPC_LIVE_RATES_AJAX + "?action=schedule_get", { credentials: "same-origin" })
			.then(function (r) { return r.json(); })
			.then(function (j) {
				if (!j || !j.ok) {
					epcFxSchedSetStatus("danger", (j && j.error) ? j.error : "Could not load schedule");
					return;
				}
				epcFxSchedRender(j.schedule);
			})
			.catch(function () {
				epcFxSchedSetStatus("danger", "Network error loading schedule");
			});
	}

	function epcFxSchedSave() {
		epcFxSchedSetStatus("info", "Saving schedule…");
		var body = new FormData();
		body.append("action", "schedule_save");
		body.append("csrf_guard_key", EPC_LIVE_CSRF);
		body.append("enabled", document.getElementById("epc_fx_sched_enabled").checked ? "1" : "0");
		body.append("timezone", document.getElementById("epc_fx_sched_timezone").value || "Asia/Dubai");
		body.append("hour", document.getElementById("epc_fx_sched_hour").value || "2");
		fetch(EPC_LIVE_RATES_AJAX, { method: "POST", credentials: "same-origin", body: body })
			.then(function (r) { return r.json(); })
			.then(function (j) {
				if (!j || !j.ok) {
					epcFxSchedSetStatus("danger", (j && (j.message || j.error)) ? (j.message || j.error) : "Save failed");
					return;
				}
				epcFxSchedRender(j.schedule);
				epcFxSchedSetStatus("success", "Daily auto update schedule saved.");
			})
			.catch(function () {
				epcFxSchedSetStatus("danger", "Network error saving schedule");
			});
	}

	function epcFxSchedRunNow() {
		if (!confirm("Fetch and apply live FX rates now?")) return;
		epcFxSchedSetStatus("info", "Running live FX update…");
		var body = new FormData();
		body.append("action", "schedule_run_now");
		body.append("csrf_guard_key", EPC_LIVE_CSRF);
		fetch(EPC_LIVE_RATES_AJAX, { method: "POST", credentials: "same-origin", body: body })
			.then(function (r) { return r.json(); })
			.then(function (j) {
				if (!j || !j.ok) {
					epcFxSchedSetStatus("danger", (j && (j.error || j.message)) ? (j.error || j.message) : "Run failed");
					if (j && j.schedule) epcFxSchedRender(j.schedule);
					return;
				}
				if (j.schedule) epcFxSchedRender(j.schedule);
				epcFxSchedSetStatus("success", "Updated " + (j.updated || 0) + " currencies from " + (j.provider || "live FX") + ".");
			})
			.catch(function () {
				epcFxSchedSetStatus("danger", "Network error running update");
			});
	}

	// Load schedule panel on page open
	if (document.getElementById("epc_fx_sched_enabled")) {
		epcFxSchedLoad();
	}
	</script>
	
	
	
	
	
	
	
	
	
	
	
	
	
	
	

	
	
	<script>
    <?php
    echo $for_js;//Выводим массив с чекбоксами для элементов
    ?>
    //Обработка переключения Выделить все/Снять все
    function on_check_uncheck_all()
    {
        var state = document.getElementById("check_uncheck_all").checked;
        
        for(var i=0; i<elements_array.length;i++)
        {
            document.getElementById(elements_array[i]).checked = state;
        }
    }//~function on_check_uncheck_all()
    
    
    
    //Обработка переключения одного чекбокса
    function on_one_check_changed(id)
    {
        //Если хотя бы один чекбокс снят - снимаем общий чекбокс
        for(var i=0; i<elements_array.length;i++)
        {
            if(document.getElementById(elements_array[i]).checked == false)
            {
                document.getElementById("check_uncheck_all").checked = false;
                break;
            }
        }
    }//~function on_one_check_changed(id)
    
    
    
    //Получение массива id отмеченых элементов
    function getCheckedElements()
    {
        var checked_ids = new Array();
        //По массиву чекбоксов
        for(var i=0; i<elements_array.length;i++)
        {
            if(document.getElementById(elements_array[i]).checked == true)
            {
                checked_ids.push(elements_id_array[i]);
            }
        }
        
        return checked_ids;
    }
    </script>
	
	<?php
}
?>