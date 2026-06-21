<?php
defined('_ASTEXE_') or die('No access');
// Скрипт вывода детальной информации по выбранному способу получения. Используется на странице "Мой заказ" - frontend
?>
<p class="lead"><?php echo translate_str_by_id(3507); ?> - <?=translate_str_by_id($obtain_mode["caption"]); ?></p>
<?php
$office_to_show = $how_get_json["office_id"];
require($_SERVER["DOCUMENT_ROOT"]."/content/shop/obtaining_modes/get_in_office/show_office_info.php");
?>