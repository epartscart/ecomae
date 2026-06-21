<?php
/**
Скрипт страницы "О программе"
*/

defined('_ASTEXE_') or die('No access');
?>
<?php
//Инициализируем переменные
//Базовая версия
$base_version_query = $db_link->prepare("SELECT * FROM `version_control` ORDER BY `id` ASC LIMIT 1;");
$base_version_query->execute();
$base_version_record = $base_version_query->fetch();
if( $base_version_record != false )
{
	$base_version = $base_version_record["version"];
	$base_version_time = $base_version_record["time"];
}
//Текущая версия версия
$current_version_query = $db_link->prepare("SELECT * FROM `version_control` ORDER BY `id` DESC LIMIT 1;");
$current_version_query->execute();
$current_version_record = $current_version_query->fetch();
if( $current_version_record != false )
{
	$current_version = $current_version_record["version"];
	$current_version_time = $current_version_record["time"];
}
?>

<div class="col-lg-12">
	<div class="hpanel">
		<div class="panel-heading hbuilt">
			<?php echo translate_str_by_id(2492); ?>
		</div>
		<div class="panel-body">
			<div class="table-responsive">
				<table cellpadding="1" cellspacing="1" class="table table-bordered table-striped">
					<tbody>
					<tr>
						<td><b><?php echo translate_str_by_id(2493); ?></b></td>
						<td><?php echo $base_version; ?></td>
						<td><b><?php echo translate_str_by_id(2494); ?></b></td>
						<td><?php echo date("d.m.Y H:i:s", $base_version_time); ?></td>
					</tr>
					<tr>
						<td><b><?php echo translate_str_by_id(2495); ?></b></td>
						<td><?php echo $current_version; ?></td>
						<td><b><?php echo translate_str_by_id(2494); ?></b></td>
						<td><?php echo date("d.m.Y H:i:s", $current_version_time); ?></td>
					</tr>
					</tbody>
				</table>
			</div>

		</div>
	</div>
</div>