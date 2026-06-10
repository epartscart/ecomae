<?php
/**
 * Скрипт для вывода сообщений о результатах действий
 
 Возможные значения классов для блоков:
 
 success
 danger
 purple
 info
 primary
 mint
 pink
 dark
 
 css в astself.css
 
*/
defined('_ASTEXE_') or die('No access');
?>

<?php
//Вывод сообщений
if(!empty($_GET["success_message"]))
{
    ?>
	<div class="col-lg-12" id="success_div">
		<div class="alert alert-success fade in">
			<button class="close" onclick="clearAlert('success_div');" data-dismiss="alert"><span>×</span></button>
			<strong><?php echo translate_str_by_id(2383); ?></strong> <?php echo htmlentities($_GET["success_message"]); ?>
		</div>
	</div>
    <?php
}
if(!empty($_GET["error_message"]))
{
    ?>
	<div class="col-lg-12" id="danger_div">
		<div class="alert alert-danger fade in">
			<button class="close" onclick="clearAlert('danger_div');" data-dismiss="alert"><span>×</span></button>
			<strong><?php echo translate_str_by_id(2384); ?></strong> <?php echo htmlentities($_GET["error_message"]); ?>
		</div>
	</div>
    <?php
}
if(!empty($_GET["warning_message"]))
{
    ?>
	<div class="col-lg-12" id="purple_div">
		<div class="alert alert-purple fade in">
			<button class="close" onclick="clearAlert('purple_div');" data-dismiss="alert"><span>×</span></button>
			<strong><?php echo translate_str_by_id(2385); ?></strong> <?php echo htmlentities($_GET["warning_message"]); ?>
		</div>
	</div>
    <?php
}
if(!empty($_GET["info_message"]))
{
    ?>
	<div class="col-lg-12" id="info_div">
		<div class="alert alert-info fade in">
			<button class="close" onclick="clearAlert('info_div');" data-dismiss="alert"><span>×</span></button>
			<strong><?php echo translate_str_by_id(2386); ?></strong> <?php echo htmlentities($_GET["info_message"]); ?>
		</div>
	</div>
    <?php
}
?>

<?php
// clearAlert() lives in desktop.php footer — keep this include HTML-only.