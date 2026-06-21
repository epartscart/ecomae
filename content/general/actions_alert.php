<?php
/**
 * Скрипт для вывода сообщений о результатах действий
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
//Вывод сообщений
if(!empty($_GET["success_message"]))
{
    ?>
    <div class="alert alert-success alert-dismissable" id="success_div">
        <button type="button" class="close" onclick="clearAlert('success_div');">&times;</button>
		<strong><i class="fa fa-check"></i> <?php echo translate_str_by_id(4036); ?>!</strong>
        <?php echo htmlentities($_GET["success_message"]); ?>
    </div>
    <?php
}
if(!empty($_GET["error_message"]))
{
    ?>
    <div class="alert alert-danger alert-dismissable" id="error_div">
        <button type="button" class="close" onclick="clearAlert('error_div');">&times;</button>
		<strong><i class="fa fa-times"></i> <?php echo translate_str_by_id(2122); ?>!</strong>
        <?php echo htmlentities($_GET["error_message"]); ?>
    </div>
    <?php
}
if(!empty($_GET["warning_message"]))
{
    ?>
    <div class="alert alert-warning alert-dismissable" id="warning_div">
        <button type="button" class="close" onclick="clearAlert('warning_div');">&times;</button>
		<strong><i class="fa fa-warning"></i> <?php echo translate_str_by_id(2385); ?></strong>
        <?php echo htmlentities($_GET["warning_message"]); ?>
    </div>
    <?php
}
if(!empty($_GET["info_message"]))
{
    ?>
    <div class="alert alert-info alert-dismissable" id="info_div">
        <button type="button" class="close" onclick="clearAlert('info_div');">&times;</button>
		<strong><i class="fa fa-info"></i> <?php echo translate_str_by_id(2307); ?>!</strong>
        <?php echo htmlentities($_GET["info_message"]); ?>
    </div>
    <?php
}
?>

<script>
    //Удаляем сообщение
    function clearAlert(alert_div_id)
    {
        var alert_div = document.getElementById(alert_div_id);
        alert_div.parentNode.removeChild(alert_div);
    }
</script>