<?php
/*
Страничный скрипт Статистики интернет-магазина
*/
defined('_ASTEXE_') or die('No access');

//Блок статистики
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/statistics/statistics_main_page.php");

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
	require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/statistics/stat_article_queries_rating/page.php");
}
else if($statistics_type == "article_queries_time_chart")
{
	require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/statistics/stat_article_queries_time_chart/page.php");
}
?>