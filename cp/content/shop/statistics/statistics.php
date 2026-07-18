<?php
/*
Страничный скрипт Статистики интернет-магазина
*/
defined('_ASTEXE_') or die('No access');

$statsMain = $_SERVER["DOCUMENT_ROOT"] . "/" . $DP_Config->backend_dir . "/content/shop/statistics/statistics_main_page.php";
$ratingPage = $_SERVER["DOCUMENT_ROOT"] . "/" . $DP_Config->backend_dir . "/content/shop/statistics/stat_article_queries_rating/page.php";
$chartPage = $_SERVER["DOCUMENT_ROOT"] . "/" . $DP_Config->backend_dir . "/content/shop/statistics/stat_article_queries_time_chart/page.php";

// Older Docpart builds shipped these report modules; some platform docroots
// no longer include them. Guard so /cp/shop/statistika does not fatal (HTTP 500).
if (!is_file($statsMain) && !is_file($ratingPage) && !is_file($chartPage)) {
	?>
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">Statistics</div>
			<div class="panel-body">
				<p class="text-muted" style="margin:0 0 12px">
					The classic article-query statistics modules are not installed on this host.
				</p>
				<p style="margin:0">
					Use
					<a href="/<?php echo htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/control/portal/epc_web_tracker">Website tracker</a>
					for traffic analytics, or
					<a href="/<?php echo htmlspecialchars((string) $DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/control/portal/epc_boc_command_center">Command Center</a>
					for fleet KPIs.
				</p>
			</div>
		</div>
	</div>
	<?php
	// Do not return — CP pages are eval()'d inside the template shell.
} else {

//Блок статистики
if (is_file($statsMain)) {
	require_once $statsMain;
}

$statistics_type = "article_queries_rating";//По учолчанию - выбранный отчет - Рейтинг запросов по артикулу
if( isset($_COOKIE["statistics_type"]) )
{
	$statistics_type = $_COOKIE["statistics_type"];
}
?>



<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2117); ?>
		</div>
		<div class="panel-body">
			<div class="form-group">
				<label for="" class="col-lg-6 control-label">
					<?php echo translate_str_by_id(3845); ?>
				</label>
				<div class="col-lg-6">
					<select id="statistics_type_select" onchange="onchange_statistics_type_select();" class="form-control">
						<option value="article_queries_rating"><?php echo translate_str_by_id(3839); ?></option>
						<option value="article_queries_time_chart"><?php echo translate_str_by_id(3843); ?></option>
					</select>
				</div>
			</div>
		</div>
	</div>
</div>
<script>
document.getElementById("statistics_type_select").value = '<?php echo $statistics_type; ?>';
function onchange_statistics_type_select()
{
	//Устанавливаем cookie (на полгода)
	var date = new Date(new Date().getTime() + 15552000 * 1000);
	document.cookie = "statistics_type="+document.getElementById("statistics_type_select").value+"; path=/; expires=" + date.toUTCString();
	
	//Обновляем страницу
	location='/<?php echo $DP_Config->backend_dir; ?>/shop/statistika';
}
</script>



<?php
//В зависимости от выставленного типа отчета - подключаем соответствующий скрипт
if($statistics_type == "article_queries_rating")
{
	if (is_file($ratingPage)) {
		require_once $ratingPage;
	} else {
		echo '<div class="col-lg-12"><div class="alert alert-warning">Article queries rating report module is not installed.</div></div>';
	}
}
else if($statistics_type == "article_queries_time_chart")
{
	if (is_file($chartPage)) {
		require_once $chartPage;
	} else {
		echo '<div class="col-lg-12"><div class="alert alert-warning">Article queries time chart module is not installed.</div></div>';
	}
}

} // modules present
?>
