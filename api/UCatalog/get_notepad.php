<?php
// Список автомобилей гаража
defined('_UCatalog_') or die('No access');



//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    exit("No DB connect");
}
$db_link->query("SET NAMES utf8;");



//Для работы с пользователем
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$user_id = DP_User::getUserId();


ob_start();
?>

<div class="row" style="margin: 0;">
	<div class="col-lg-12" style="border-bottom: 1px solid #ddd; padding: 5px 15px; position: relative; font-weight: bold;">
		<i onClick="$('#UCatalog_modal_garage').modal('hide');" style="position: absolute; top: 8px; right: 8px; font-size: 18px; cursor: pointer;" class="fa fa-times-circle-o" aria-hidden="true"></i>
		<span><?php echo translate_str_by_id(2099); ?></span>
	</div>
</div>
<div style="padding: 15px;">
	<div class="row">
		<div class="col-lg-12">
			<div id="UCatalog_add_bloknot_content">
			<?php
			$query = $db_link->prepare('SELECT *, (SELECT `caption` FROM `shop_docpart_cars` WHERE `id` = `shop_docpart_garage`.`mark_id`) AS `mark` FROM `shop_docpart_garage` WHERE `user_id` = ?;');
			$query->execute( array($user_id) );
			echo '<select id="UCatalog_garage_auto" class="form-control">';
			echo '<option value="0">'.translate_str_by_id(2100).'</option>';
			while($car = $query->fetch())
			{
				echo '<option value="'.$car['id'].'">'. $car["caption"] .'</option>';
			}
			echo '</select>';
			?>
			</div>
			
			<div id="UCatalog_add_bloknot_btn" style="margin-top:15px; text-align:right;">
				<div id="UCatalog_add_bloknot_msg"></div>
				<span class="btn_primary" onclick="UCatalog_add_notepad('<?php echo $request_object['manufacturer']; ?>', '<?php echo $request_object['article']; ?>', '<?php echo $request_object['name']; ?>');"><?php echo translate_str_by_id(2101); ?></span>
			</div>
		</div>

	</div>
</div>

<?php
$html = ob_get_contents();
ob_end_clean();

$answer["status"] = true;
$answer["html"] = $html;
$answer["tag"] = 'UCatalog_modal_garage_body';
?>