<?php
defined('_ASTEXE_') or die('No access');

//Верхняя панель модуля Возвраты
?>
<div class="col-lg-12">
    <div class="hpanel">
        <div class="panel-heading hbuilt">
            <?php echo translate_str_by_id(2113); ?>
        </div>
        <div class="panel-body">
            <a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/returns-manager">
                <div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/special_search.png') 0 0 no-repeat;"></div>
                <div class="panel_a_caption"><?php echo translate_str_by_id(3815); ?></div>
            </a>
            <a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>/shop/returns-manager?page=reasons_statuses&action=select">
                <div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/warning.png') 0 0 no-repeat;"></div>
                <div class="panel_a_caption"><?php echo translate_str_by_id(3799); ?></div>
            </a>
            <a class="panel_a" href="/<?php echo $DP_Config->backend_dir; ?>">
                <div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir;?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
                <div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
            </a>
        </div>
    </div>
</div>
<?php
$epcReturnsRouter = $_SERVER["DOCUMENT_ROOT"] . "/" . $DP_Config->backend_dir . "/content/shop/returns/router.php";
if (is_file($epcReturnsRouter)) {
	require_once $epcReturnsRouter;
} else {
	echo '<div class="col-lg-12"><div class="alert alert-warning">Returns router module is not installed on this host.</div></div>';
}
// Do not return — CP pages are eval()'d inside the template shell.
?>