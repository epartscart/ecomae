<?php
//Серверный скрипт для просмотра загруженных прайс-листов админом
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;

//Подключение к БД
try
{
	$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e) 
{
    $answer = array();
    $answer["result"] = 0;
    $answer["message"] = "No DB connect";
	header('Content-Type: application/json;charset=utf-8;');
    exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");


// -------------------------------------------------------------------------------
//Подключение мультиязычности
require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
$multilang_params = multilang_init();
// -------------------------------------------------------------------------------


// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------


//Проверка прав:
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
//Проверяем право менеджера
if( ! DP_User::isAdmin())
{
	$result["status"] = false;
	$result["message"] = "Forbidden";
	$result["code"] = 501;
	header('Content-Type: application/json;charset=utf-8;');
	exit(json_encode($result));//Вообще не является администратором бэкенда
}





$price_id = (int)$_POST["price_id"];


//Сначала получаем количество:
$price_records_query = $db_link->prepare("SELECT COUNT(*) FROM `shop_docpart_prices_data` WHERE `price_id` = ?;");
$price_records_query->execute( array($price_id) );
$price_records_count = $price_records_query->fetchColumn();

if( $price_records_count == 0 )
{
	?>
	<div class="text-center">
		<?php echo translate_str_by_id(3689); ?>
	</div>
	<?php
	exit;
}


$price_records_query = $db_link->prepare("SELECT * FROM `shop_docpart_prices_data` WHERE `price_id` = ? LIMIT 10;");
$price_records_query->execute( array($price_id) );

?>
<div>
<h3 style="font-weight:bold;"><?php echo translate_str_by_id(3690); ?></h3>
<p><?php echo translate_str_by_id(3691); ?> <font style="color:#000;font-weight:bold;"><?php echo translate_str_by_id(3692); ?></font>, <?php echo translate_str_by_id(3693); ?>:<br>
- <?php echo translate_str_by_id(3694); ?><br>
- <?php echo translate_str_by_id(3695); ?><br>
- <?php echo translate_str_by_id(3696); ?><br>
- <?php echo translate_str_by_id(3697); ?><br><br>

<?php echo translate_str_by_id(3693); ?> <font style="color:#000;font-weight:bold;"><?php echo translate_str_by_id(3698); ?></font> <?php echo translate_str_by_id(3699); ?>.</p>

<table style="border-spacing: 7px 5px;">

<tr>
	<th style="padding: 5px;"><?php echo translate_str_by_id(2070); ?></th>
	<th style="padding: 5px;"><?php echo translate_str_by_id(2071); ?></th>
	<th style="padding: 5px;"><?php echo translate_str_by_id(2102); ?></th>
	<th style="padding: 5px;"><?php echo translate_str_by_id(2751); ?></th>
	<th style="padding: 5px;"><?php echo translate_str_by_id(2752); ?></th>
</tr>

<?php
while( $record = $price_records_query->fetch() )
{
	?>
	<tr>
		<td style="padding: 5px;"><?php echo $record["manufacturer"]; ?></td>
		<td style="padding: 5px;"><?php echo $record["article"]; ?></td>
		<td style="padding: 5px;"><?php echo $record["name"]; ?></td>
		<td style="padding: 5px;"><?php echo $record["price"]; ?></td>
		<td style="padding: 5px;"><?php echo $record["exist"]; ?></td>
	</tr>
	<?php
}
?>
</table>
</div>