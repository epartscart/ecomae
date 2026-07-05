<?php
/**
 * CP — Industry Consolidation Dashboard (Super CP)
 *
 * Shows how 1154 industries are grouped into ~28 super groups,
 * with statistics on server load reduction and template sharing.
 * Allows operators to view and manage the mapping.
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_industry_consolidation.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_industry_template_router.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

epc_portal_db_ensure($db_link);

$groups = epc_industry_groups();
$savings = epc_industry_consolidation_savings();

epc_cp_page_frame_open(array(
    'hero' => array(
        'badge' => 'Platform Optimization',
        'title' => 'Industry Consolidation Engine',
        'sub' => '1154 industries consolidated into ' . count($groups) . ' template groups — ' . $savings['reduction_pct'] . '% fewer templates to maintain.',
    ),
));
?>

<div class="row" style="margin-bottom:24px">
    <div class="col-lg-3 col-sm-6">
        <div class="hpanel" style="text-align:center;padding:20px">
            <h1 style="color:#3b82f6;margin:0"><?php echo $savings['total_industries']; ?></h1>
            <p class="text-muted" style="margin:4px 0 0">Total Industries</p>
        </div>
    </div>
    <div class="col-lg-3 col-sm-6">
        <div class="hpanel" style="text-align:center;padding:20px">
            <h1 style="color:#10b981;margin:0"><?php echo $savings['groups_count']; ?></h1>
            <p class="text-muted" style="margin:4px 0 0">Template Groups</p>
        </div>
    </div>
    <div class="col-lg-3 col-sm-6">
        <div class="hpanel" style="text-align:center;padding:20px">
            <h1 style="color:#f59e0b;margin:0"><?php echo $savings['reduction_pct']; ?>%</h1>
            <p class="text-muted" style="margin:4px 0 0">Template Reduction</p>
        </div>
    </div>
    <div class="col-lg-3 col-sm-6">
        <div class="hpanel" style="text-align:center;padding:20px">
            <h1 style="color:#8b5cf6;margin:0"><?php echo $savings['php_files_saved']; ?></h1>
            <p class="text-muted" style="margin:4px 0 0">PHP Files Saved</p>
        </div>
    </div>
</div>

<div class="row" style="margin-bottom:24px">
    <div class="col-lg-12">
        <div class="hpanel">
            <div class="panel-heading"><h4>Server Load Savings</h4></div>
            <div class="panel-body">
                <table class="table table-striped">
                    <thead>
                        <tr>
                            <th>Metric</th>
                            <th>Before (1 per industry)</th>
                            <th>After (consolidated)</th>
                            <th>Saving</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr>
                            <td>Frontend templates</td>
                            <td><?php echo $savings['templates_before']; ?></td>
                            <td><?php echo $savings['templates_after']; ?></td>
                            <td class="text-success"><strong><?php echo $savings['templates_before'] - $savings['templates_after']; ?> fewer</strong></td>
                        </tr>
                        <tr>
                            <td>PHP files (home + catalog + config)</td>
                            <td><?php echo $savings['templates_before'] * 3; ?></td>
                            <td><?php echo $savings['templates_after'] * 3; ?></td>
                            <td class="text-success"><strong><?php echo $savings['php_files_saved']; ?> fewer</strong></td>
                        </tr>
                        <tr>
                            <td>CSS theme files</td>
                            <td><?php echo $savings['templates_before']; ?></td>
                            <td><?php echo $savings['templates_after']; ?></td>
                            <td class="text-success"><strong><?php echo $savings['css_files_saved']; ?> fewer</strong></td>
                        </tr>
                        <tr>
                            <td>OPcache memory</td>
                            <td>~<?php echo round($savings['templates_before'] * 0.5); ?> MB</td>
                            <td>~<?php echo round($savings['templates_after'] * 0.5); ?> MB</td>
                            <td class="text-success"><strong><?php echo $savings['memory_reduction_est']; ?></strong></td>
                        </tr>
                    </tbody>
                </table>
            </div>
        </div>
    </div>
</div>

<div class="row">
    <div class="col-lg-12">
        <div class="hpanel">
            <div class="panel-heading"><h4>Consolidated Industry Groups (<?php echo count($groups); ?>)</h4></div>
            <div class="panel-body">
                <div class="epc-consolidation-grid">
                    <?php foreach ($groups as $key => $group) {
                        $areaCount = count($group['available_sub_areas'] ?? array());
                    ?>
                    <div class="epc-group-card">
                        <div class="epc-group-card__header" style="background:linear-gradient(135deg, <?php echo htmlspecialchars($group['color_scheme']['bg_from'] ?? '#1e3a5f', ENT_QUOTES, 'UTF-8'); ?>, <?php echo htmlspecialchars($group['color_scheme']['bg_to'] ?? '#1e40af', ENT_QUOTES, 'UTF-8'); ?>)">
                            <i class="fa <?php echo htmlspecialchars($group['icon'], ENT_QUOTES, 'UTF-8'); ?> fa-2x"></i>
                            <h5><?php echo htmlspecialchars($group['label'], ENT_QUOTES, 'UTF-8'); ?></h5>
                        </div>
                        <div class="epc-group-card__body">
                            <p class="text-muted" style="font-size:12px;margin:0 0 8px"><?php echo htmlspecialchars($group['description'], ENT_QUOTES, 'UTF-8'); ?></p>
                            <div class="epc-group-card__meta">
                                <span><i class="fa fa-puzzle-piece"></i> <?php echo $areaCount; ?> sub-areas</span>
                                <span><i class="fa fa-file-code-o"></i> <code><?php echo htmlspecialchars($group['template_key'], ENT_QUOTES, 'UTF-8'); ?></code></span>
                                <span><i class="fa fa-calculator"></i> <?php echo htmlspecialchars($group['costing_default'] ?? 'weighted_avg', ENT_QUOTES, 'UTF-8'); ?></span>
                            </div>
                            <div class="epc-group-card__areas">
                                <?php
                                $areas = $group['available_sub_areas'] ?? array();
                                $shown = 0;
                                foreach ($areas as $aKey => $aLabel) {
                                    if ($shown >= 5) {
                                        echo '<span class="badge" style="background:#e5e7eb;color:#6b7280">+' . (count($areas) - 5) . ' more</span>';
                                        break;
                                    }
                                    $isDefault = in_array($aKey, $group['default_sub_areas'] ?? array(), true);
                                    $style = $isDefault ? 'background:#dbeafe;color:#1d4ed8' : 'background:#f3f4f6;color:#374151';
                                    echo '<span class="badge" style="' . $style . '">' . htmlspecialchars($aLabel, ENT_QUOTES, 'UTF-8') . '</span> ';
                                    $shown++;
                                }
                                ?>
                            </div>
                        </div>
                    </div>
                    <?php } ?>
                </div>
            </div>
        </div>
    </div>
</div>

<style>
.epc-consolidation-grid {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
    gap: 16px;
}
.epc-group-card {
    border: 1px solid #e5e7eb;
    border-radius: 12px;
    overflow: hidden;
    transition: transform .2s, box-shadow .2s;
}
.epc-group-card:hover {
    transform: translateY(-2px);
    box-shadow: 0 8px 24px rgba(0,0,0,.1);
}
.epc-group-card__header {
    padding: 16px;
    color: #fff;
    display: flex;
    align-items: center;
    gap: 12px;
}
.epc-group-card__header h5 {
    margin: 0;
    color: #fff;
    font-size: 15px;
}
.epc-group-card__body {
    padding: 14px 16px;
}
.epc-group-card__meta {
    display: flex;
    gap: 12px;
    font-size: 11px;
    color: #6b7280;
    margin-bottom: 10px;
}
.epc-group-card__meta span {
    display: flex;
    align-items: center;
    gap: 4px;
}
.epc-group-card__areas {
    display: flex;
    flex-wrap: wrap;
    gap: 4px;
}
.epc-group-card__areas .badge {
    font-size: 11px;
    font-weight: 500;
    padding: 3px 8px;
    border-radius: 4px;
}
</style>

<?php epc_cp_page_frame_close(); ?>
