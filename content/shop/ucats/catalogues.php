<?php
/*
 * UCats Catalogues — frontend listing of all UCats catalogs
 * Safe standalone + included version (PHP 5.6 compatible)
 */

// Ensure Docpart core environment is loaded
if (!defined('_ASTEXE_')) {
    define('_ASTEXE_', 1);
    require_once $_SERVER["DOCUMENT_ROOT"] . "/config.php";
    require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_helper.php";
    require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_content.php";
    require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_module.php";
    require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_template.php";
    require_once $_SERVER["DOCUMENT_ROOT"] . "/core/dp_core.php";
}

// Ensure $DP_Config exists
if (!isset($DP_Config)) {
    die("Configuration not loaded.");
}

// For main color class
$color_class = "navbar-inverse";

// Check whether any UCats categories are enabled
if (
    ($DP_Config->ucats_shiny != '' ||
    $DP_Config->ucats_disks != '' ||
    $DP_Config->ucats_accessories != '' ||
    $DP_Config->ucats_to != '' ||
    $DP_Config->ucats_oil != '' ||
    $DP_Config->ucats_akb != '' ||
    $DP_Config->ucats_caps != '' ||
    $DP_Config->ucats_bolty != '')
) {
    ?>
    <div class="row">
        <div class="col-xs-12 col-sm-12 col-md-12 col-lg-12">
            <h2 class="section-title"><?php echo translate_str_by_id(4584); ?></h2>
            <div class="row" style="margin-right:-11px; margin-left:-11px; margin-top:-9px; margin-bottom:-10px;">
                <?php
                if ($DP_Config->ucats_shiny != '') {
                    ?>
                    <div class="col-sm-6 col-md-4 col-lg-3 new-cat-block">
                        <a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/shiny" class="ucats-h-1 new-cat-block-tires">
                            <div class="new-cat-block-text <?php echo $color_class; ?>"><?php echo translate_str_by_id(4585); ?></div>
                        </a>
                    </div>
                    <?php
                }
                if ($DP_Config->ucats_disks != '') {
                    ?>
                    <div class="col-sm-6 col-md-4 col-lg-3 new-cat-block">
                        <a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/kolesnye-diski" class="ucats-h-1 new-cat-block-disks">
                            <div class="new-cat-block-text <?php echo $color_class; ?>"><?php echo translate_str_by_id(4586); ?></div>
                        </a>
                    </div>
                    <?php
                }
                if ($DP_Config->ucats_accessories != '') {
                    ?>
                    <div class="col-sm-6 col-md-4 col-lg-3 new-cat-block">
                        <a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/avtoaksessuary" class="ucats-h-1 new-cat-block-accessories">
                            <div class="new-cat-block-text <?php echo $color_class; ?>"><?php echo translate_str_by_id(4587); ?></div>
                        </a>
                    </div>
                    <?php
                }
                if ($DP_Config->ucats_to != '') {
                    ?>
                    <div class="col-sm-6 col-md-4 col-lg-3 new-cat-block">
                        <a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/katalog-texnicheskogo-obsluzhivaniya" class="ucats-h-1 new-cat-block-to">
                            <div class="new-cat-block-text <?php echo $color_class; ?>"><?php echo translate_str_by_id(4588); ?></div>
                        </a>
                    </div>
                    <?php
                }
                if ($DP_Config->ucats_oil != '') {
                    ?>
                    <div class="col-sm-6 col-md-4 col-lg-3 new-cat-block">
                        <a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/avtoximiya" class="ucats-h-1 new-cat-block-oil">
                            <div class="new-cat-block-text <?php echo $color_class; ?>"><?php echo translate_str_by_id(4589); ?></div>
                        </a>
                    </div>
                    <?php
                }
                if ($DP_Config->ucats_akb != '') {
                    ?>
                    <div class="col-sm-6 col-md-4 col-lg-3 new-cat-block">
                        <a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/akkumulyatory" class="ucats-h-1 new-cat-block-akb">
                            <div class="new-cat-block-text <?php echo $color_class; ?>"><?php echo translate_str_by_id(4590); ?></div>
                        </a>
                    </div>
                    <?php
                }
                if ($DP_Config->ucats_caps != '') {
                    ?>
                    <div class="col-sm-6 col-md-4 col-lg-3 new-cat-block">
                        <a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/kolpaki" class="ucats-h-1 new-cat-block-caps">
                            <div class="new-cat-block-text <?php echo $color_class; ?>"><?php echo translate_str_by_id(4591); ?></div>
                        </a>
                    </div>
                    <?php
                }
                if ($DP_Config->ucats_bolty != '') {
                    ?>
                    <div class="col-sm-6 col-md-4 col-lg-3 new-cat-block">
                        <a href="<?php echo $multilang_params['lang_href']; ?>/shop/katalogi-ucats/kolesnye-gajki-bolty-prostavki" class="ucats-h-1 new-cat-block-bolts">
                            <div class="new-cat-block-text <?php echo $color_class; ?>"><?php echo translate_str_by_id(4592); ?></div>
                        </a>
                    </div>
                    <?php
                }
                ?>
            </div>
        </div>
    </div>
    <?php
}
?>