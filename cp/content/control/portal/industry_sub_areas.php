<?php
/**
 * CP — Industry Sub-Area Toggle Panel
 *
 * Lets tenants activate/deactivate specific sub-areas within their
 * consolidated industry group. E.g., a "Healthcare" tenant can toggle
 * on Pharmacy + Lab, toggle off Dental and Veterinary.
 *
 * This panel is accessible from CP → Industry Settings.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_industry_consolidation.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

epc_portal_db_ensure($db_link);

$settings = epc_portal_load_site_settings($db_link);
$siteKey = isset($settings['site_key']) ? (string) $settings['site_key'] : '';
$industryCode = isset($settings['industry_code']) ? (string) $settings['industry_code'] : 'general';

$groupKey = epc_industry_resolve_group($industryCode);
$group = epc_industry_get_group($industryCode);
$allAreas = $group['available_sub_areas'] ?? array();
$activeAreas = epc_industry_tenant_sub_areas($db_link, $siteKey, $industryCode);

// Handle save
if ($_SERVER['REQUEST_METHOD'] === 'POST' && isset($_POST['epc_sub_areas_save'])) {
    $newAreas = array();
    foreach ($allAreas as $key => $label) {
        $newAreas[$key] = !empty($_POST['sub_area_' . $key]);
    }
    epc_industry_save_tenant_sub_areas($db_link, $siteKey, $industryCode, $groupKey, $newAreas);
    $activeAreas = $newAreas;
    $savedMsg = 'Sub-area settings saved successfully.';
}

$activeCount = count(array_filter($activeAreas));
$totalCount = count($allAreas);

epc_cp_page_frame_open(array(
    'hero' => array(
        'badge' => 'Industry Configuration',
        'title' => 'Sub-Area Toggles',
        'sub' => 'Activate or deactivate specific capabilities within your <strong>' . htmlspecialchars($group['label'], ENT_QUOTES, 'UTF-8') . '</strong> industry group. Only active sub-areas appear in your storefront, CP modules, and ERP.',
        'html_sub' => true,
    ),
));
?>

<?php if (!empty($savedMsg)) { ?>
<div class="alert alert-success" style="margin-bottom:16px"><i class="fa fa-check-circle"></i> <?php echo htmlspecialchars($savedMsg, ENT_QUOTES, 'UTF-8'); ?></div>
<?php } ?>

<div class="row">
    <div class="col-lg-8">
        <div class="hpanel">
            <div class="panel-heading">
                <h4><i class="fa <?php echo htmlspecialchars($group['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i> <?php echo htmlspecialchars($group['label'], ENT_QUOTES, 'UTF-8'); ?> — Sub-Areas</h4>
                <p class="text-muted" style="margin:4px 0 0">
                    <strong><?php echo $activeCount; ?></strong> of <?php echo $totalCount; ?> sub-areas active.
                    Toggle switches below to customize which capabilities your business uses.
                </p>
            </div>
            <div class="panel-body">
                <form method="post" id="epc-sub-areas-form">
                    <input type="hidden" name="epc_sub_areas_save" value="1" />
                    <div class="epc-sub-areas-grid">
                        <?php foreach ($allAreas as $key => $label) {
                            $isActive = !empty($activeAreas[$key]);
                            $inputId = 'sub_area_' . $key;
                        ?>
                        <div class="epc-sub-area-item <?php echo $isActive ? 'epc-sub-area-item--active' : ''; ?>">
                            <label class="epc-sub-area-toggle" for="<?php echo $inputId; ?>">
                                <input type="checkbox"
                                       id="<?php echo $inputId; ?>"
                                       name="<?php echo $inputId; ?>"
                                       value="1"
                                       <?php echo $isActive ? 'checked' : ''; ?>
                                       class="epc-sub-area-checkbox" />
                                <span class="epc-sub-area-switch"></span>
                                <span class="epc-sub-area-label"><?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></span>
                            </label>
                        </div>
                        <?php } ?>
                    </div>

                    <div style="margin-top:20px;padding-top:16px;border-top:1px solid #e5e7eb">
                        <button type="submit" class="btn btn-primary"><i class="fa fa-save"></i> Save Sub-Area Settings</button>
                        <button type="button" class="btn btn-default" onclick="epcSubAreaToggleAll(true)"><i class="fa fa-check-square-o"></i> Enable All</button>
                        <button type="button" class="btn btn-default" onclick="epcSubAreaToggleAll(false)"><i class="fa fa-square-o"></i> Disable All</button>
                    </div>
                </form>
            </div>
        </div>
    </div>

    <div class="col-lg-4">
        <div class="hpanel">
            <div class="panel-heading"><h4>Group Info</h4></div>
            <div class="panel-body">
                <table class="table table-condensed">
                    <tr><td><strong>Industry Group</strong></td><td><?php echo htmlspecialchars($group['label'], ENT_QUOTES, 'UTF-8'); ?></td></tr>
                    <tr><td><strong>Template</strong></td><td><code><?php echo htmlspecialchars($group['template_key'], ENT_QUOTES, 'UTF-8'); ?></code></td></tr>
                    <tr><td><strong>Your Industry</strong></td><td><?php echo htmlspecialchars($industryCode, ENT_QUOTES, 'UTF-8'); ?></td></tr>
                    <tr><td><strong>ERP Base</strong></td><td><code><?php echo htmlspecialchars($group['erp_base'] ?? 'general', ENT_QUOTES, 'UTF-8'); ?></code></td></tr>
                    <tr><td><strong>Costing</strong></td><td><?php echo htmlspecialchars($group['costing_default'] ?? 'weighted_avg', ENT_QUOTES, 'UTF-8'); ?></td></tr>
                    <tr><td><strong>Active / Total</strong></td><td><?php echo $activeCount; ?> / <?php echo $totalCount; ?></td></tr>
                </table>
                <p class="text-muted" style="font-size:12px;margin-top:8px">
                    <?php echo htmlspecialchars($group['description'] ?? '', ENT_QUOTES, 'UTF-8'); ?>
                </p>
            </div>
        </div>

        <div class="hpanel" style="margin-top:16px">
            <div class="panel-heading"><h4>Impact</h4></div>
            <div class="panel-body">
                <ul style="margin:0;padding-left:18px;font-size:13px">
                    <li><strong>Storefront:</strong> Only active sub-area categories show on your website</li>
                    <li><strong>CP Modules:</strong> Only relevant CP tools load (fewer menu items, faster)</li>
                    <li><strong>ERP:</strong> Only applicable ERP modules/reports appear in sidebar</li>
                    <li><strong>Performance:</strong> Fewer active areas = less data to load per page</li>
                </ul>
            </div>
        </div>
    </div>
</div>

<style>
.epc-sub-areas-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
    gap: 10px;
}
.epc-sub-area-item {
    border: 1px solid #e5e7eb;
    border-radius: 8px;
    padding: 12px 14px;
    transition: all .2s;
    background: #fff;
}
.epc-sub-area-item--active {
    border-color: #3b82f6;
    background: #eff6ff;
}
.epc-sub-area-toggle {
    display: flex;
    align-items: center;
    gap: 10px;
    cursor: pointer;
    margin: 0;
    font-weight: 400;
}
.epc-sub-area-checkbox {
    display: none;
}
.epc-sub-area-switch {
    position: relative;
    width: 40px;
    height: 22px;
    background: #d1d5db;
    border-radius: 11px;
    transition: background .2s;
    flex-shrink: 0;
}
.epc-sub-area-switch::after {
    content: '';
    position: absolute;
    top: 2px;
    left: 2px;
    width: 18px;
    height: 18px;
    background: #fff;
    border-radius: 50%;
    transition: transform .2s;
    box-shadow: 0 1px 3px rgba(0,0,0,.15);
}
.epc-sub-area-checkbox:checked + .epc-sub-area-switch {
    background: #3b82f6;
}
.epc-sub-area-checkbox:checked + .epc-sub-area-switch::after {
    transform: translateX(18px);
}
.epc-sub-area-label {
    font-size: 13px;
    color: #374151;
}
</style>

<script>
function epcSubAreaToggleAll(state) {
    document.querySelectorAll('.epc-sub-area-checkbox').forEach(function(cb) {
        cb.checked = state;
        var item = cb.closest('.epc-sub-area-item');
        if (item) {
            item.classList.toggle('epc-sub-area-item--active', state);
        }
    });
}
document.querySelectorAll('.epc-sub-area-checkbox').forEach(function(cb) {
    cb.addEventListener('change', function() {
        var item = this.closest('.epc-sub-area-item');
        if (item) {
            item.classList.toggle('epc-sub-area-item--active', this.checked);
        }
    });
});
</script>

<?php epc_cp_page_frame_close(); ?>
