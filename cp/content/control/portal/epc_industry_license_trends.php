<?php
/**
 * CP — Industry License Trends (Super CP)
 *
 * Monitors DED (Dubai Department of Economic Development) business activity
 * registrations and maps them to ecomae consolidation groups for targeting.
 *
 * Features:
 *   - Live view of all 17 DED divisions with activity counts
 *   - Sub-group drill-down showing individual activities
 *   - ecomae group coverage map (which groups serve which DED activities)
 *   - Search/filter to find specific activities
 *   - Worldwide registry comparison (UAE, UK, USA, EU, etc.)
 *   - Targeting recommendations for uncovered niches
 *
 * Reference: https://app.invest.dubai.ae/search-business-activities
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal_db.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_industry_consolidation.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_ded_activity_mapping.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';

epc_portal_db_ensure($db_link);

$divisions = epc_ded_divisions();
$totalActivities = epc_ded_total_activities();
$registries = epc_worldwide_business_registries();
$coverage = epc_ded_coverage_audit();
$groups = epc_industry_groups();
$groupCount = count($groups);

$totalDivisions = count($divisions);
$totalSubGroups = 0;
foreach ($divisions as $div) {
    $totalSubGroups += count($div['sub_groups']);
}
$coveragePercent = $groupCount > 0 ? round(((($groupCount - count($coverage)) / $groupCount) * 100), 1) : 0;

$searchQuery = isset($_GET['q']) ? trim((string) $_GET['q']) : '';

epc_cp_page_frame_open(array(
    'hero' => array(
        'badge' => 'DED Intelligence',
        'title' => 'Industry License Trends',
        'sub' => 'Monitor DED business activity registrations, identify new industries, and target gaps. All ' . $totalDivisions . ' DED divisions (' . number_format($totalActivities) . ' activities) mapped to ' . $groupCount . ' ecomae groups.',
        'html_sub' => false,
        'actions' => array(
            array('label' => 'DED Portal', 'url' => 'https://app.invest.dubai.ae/search-business-activities', 'icon' => 'fa-external-link'),
            array('label' => 'Consolidation Dashboard', 'url' => '/cp/control/portal/industry_consolidation', 'icon' => 'fa-sitemap'),
        ),
    ),
));
?>

<!-- KPI Cards -->
<div class="row" style="margin-bottom:24px">
    <div class="col-lg-3 col-sm-6">
        <div class="hpanel" style="text-align:center;padding:20px;border-left:4px solid #3b82f6">
            <h1 style="color:#3b82f6;margin:0"><?php echo $totalDivisions; ?></h1>
            <p class="text-muted" style="margin:4px 0 0">DED Main Divisions</p>
        </div>
    </div>
    <div class="col-lg-3 col-sm-6">
        <div class="hpanel" style="text-align:center;padding:20px;border-left:4px solid #10b981">
            <h1 style="color:#10b981;margin:0"><?php echo number_format($totalActivities); ?></h1>
            <p class="text-muted" style="margin:4px 0 0">Total Activities</p>
        </div>
    </div>
    <div class="col-lg-3 col-sm-6">
        <div class="hpanel" style="text-align:center;padding:20px;border-left:4px solid #f59e0b">
            <h1 style="color:#f59e0b;margin:0"><?php echo $totalSubGroups; ?></h1>
            <p class="text-muted" style="margin:4px 0 0">Sub-Groups</p>
        </div>
    </div>
    <div class="col-lg-3 col-sm-6">
        <div class="hpanel" style="text-align:center;padding:20px;border-left:4px solid <?php echo count($coverage) === 0 ? '#10b981' : '#ef4444'; ?>">
            <h1 style="color:<?php echo count($coverage) === 0 ? '#10b981' : '#ef4444'; ?>;margin:0"><?php echo $coveragePercent; ?>%</h1>
            <p class="text-muted" style="margin:4px 0 0">Group Coverage</p>
        </div>
    </div>
</div>

<!-- Search -->
<div class="row" style="margin-bottom:20px">
    <div class="col-lg-12">
        <form method="get" action="" class="form-inline">
            <input type="hidden" name="a" value="control">
            <input type="hidden" name="sa" value="portal">
            <input type="hidden" name="pg" value="epc_industry_license_trends">
            <div class="input-group" style="width:100%;max-width:500px">
                <input type="text" name="q" class="form-control" placeholder="Search activities... (e.g. cheese manufacturing, crude oil, jewellery trading)" value="<?php echo htmlspecialchars($searchQuery, ENT_QUOTES, 'UTF-8'); ?>">
                <span class="input-group-btn">
                    <button class="btn btn-primary" type="submit"><i class="fa fa-search"></i> Search</button>
                </span>
            </div>
            <?php if ($searchQuery !== '') { ?>
            <a href="/cp/control/portal/epc_industry_license_trends" class="btn btn-default" style="margin-left:8px"><i class="fa fa-times"></i> Clear</a>
            <?php } ?>
        </form>
    </div>
</div>

<?php
// Search results
if ($searchQuery !== '') {
    $resolvedGroup = epc_industry_resolve_group($searchQuery);
    $groupInfo = epc_industry_get_group($searchQuery);
    ?>
    <div class="row" style="margin-bottom:24px">
        <div class="col-lg-12">
            <div class="hpanel">
                <div class="panel-heading">
                    <h4><i class="fa fa-search"></i> Resolution Result: "<?php echo htmlspecialchars($searchQuery, ENT_QUOTES, 'UTF-8'); ?>"</h4>
                </div>
                <div class="panel-body">
                    <table class="table table-bordered">
                        <tr>
                            <th style="width:200px">Search Term</th>
                            <td><code><?php echo htmlspecialchars($searchQuery, ENT_QUOTES, 'UTF-8'); ?></code></td>
                        </tr>
                        <tr>
                            <th>Resolved Group</th>
                            <td>
                                <span class="label label-primary"><?php echo htmlspecialchars($resolvedGroup, ENT_QUOTES, 'UTF-8'); ?></span>
                                <?php if ($groupInfo) { ?>
                                — <?php echo htmlspecialchars($groupInfo['label'], ENT_QUOTES, 'UTF-8'); ?>
                                <?php } ?>
                            </td>
                        </tr>
                        <?php if ($groupInfo) { ?>
                        <tr>
                            <th>ERP Base</th>
                            <td><code><?php echo htmlspecialchars($groupInfo['erp_base'] ?? 'generic', ENT_QUOTES, 'UTF-8'); ?></code></td>
                        </tr>
                        <tr>
                            <th>Template</th>
                            <td><code><?php echo htmlspecialchars($groupInfo['template'] ?? 'generic_commerce', ENT_QUOTES, 'UTF-8'); ?></code></td>
                        </tr>
                        <tr>
                            <th>Sub-Areas</th>
                            <td>
                                <?php
                                $subAreas = $groupInfo['available_sub_areas'] ?? array();
                                $defaults = $groupInfo['default_sub_areas'] ?? array();
                                foreach ($subAreas as $key => $label) {
                                    $isDefault = in_array($key, $defaults);
                                    $cls = $isDefault ? 'label-success' : 'label-default';
                                    echo '<span class="label ' . $cls . '" style="margin-right:4px;margin-bottom:4px;display:inline-block">' . htmlspecialchars($label, ENT_QUOTES, 'UTF-8') . ($isDefault ? ' ✓' : '') . '</span> ';
                                }
                                ?>
                            </td>
                        </tr>
                        <?php } ?>
                    </table>
                    <?php
                    // Show which DED divisions this maps to
                    $matchedDivisions = array();
                    foreach ($divisions as $dKey => $div) {
                        foreach ($div['ecomae_groups'] as $g) {
                            if ($g === $resolvedGroup) {
                                $matchedDivisions[$dKey] = $div['label'];
                                break;
                            }
                        }
                    }
                    if (!empty($matchedDivisions)) { ?>
                    <p style="margin-top:12px"><strong>DED Divisions serving this group:</strong></p>
                    <ul>
                        <?php foreach ($matchedDivisions as $dKey => $dLabel) { ?>
                        <li><i class="fa <?php echo htmlspecialchars($divisions[$dKey]['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i> <?php echo htmlspecialchars($dLabel, ENT_QUOTES, 'UTF-8'); ?> (<?php echo $divisions[$dKey]['activities']; ?> activities)</li>
                        <?php } ?>
                    </ul>
                    <?php } ?>
                </div>
            </div>
        </div>
    </div>
    <?php
}
?>

<!-- DED Divisions Grid -->
<div class="row" style="margin-bottom:24px">
    <div class="col-lg-12">
        <div class="hpanel">
            <div class="panel-heading">
                <h4><i class="fa fa-building"></i> DED Business Activity Divisions</h4>
                <small class="text-muted">Official Dubai DED classification — <?php echo $totalDivisions; ?> divisions with <?php echo number_format($totalActivities); ?> total activities</small>
            </div>
            <div class="panel-body" style="padding:0">
                <table class="table table-striped table-hover" style="margin:0">
                    <thead>
                        <tr>
                            <th style="width:40px">#</th>
                            <th>Division</th>
                            <th style="width:100px;text-align:center">Activities</th>
                            <th style="width:100px;text-align:center">Sub-Groups</th>
                            <th>ecomae Groups</th>
                        </tr>
                    </thead>
                    <tbody>
                        <?php $idx = 1; foreach ($divisions as $dKey => $div) { ?>
                        <tr>
                            <td><?php echo $idx++; ?></td>
                            <td>
                                <i class="fa <?php echo htmlspecialchars($div['icon'], ENT_QUOTES, 'UTF-8'); ?>" style="width:20px"></i>
                                <strong><?php echo htmlspecialchars($div['label'], ENT_QUOTES, 'UTF-8'); ?></strong>
                            </td>
                            <td style="text-align:center">
                                <span class="badge" style="background:#3b82f6"><?php echo $div['activities']; ?></span>
                            </td>
                            <td style="text-align:center"><?php echo count($div['sub_groups']); ?></td>
                            <td>
                                <?php foreach ($div['ecomae_groups'] as $grp) {
                                    $grpLabel = isset($groups[$grp]) ? $groups[$grp]['label'] : $grp;
                                    echo '<span class="label label-info" style="margin-right:3px;margin-bottom:2px;display:inline-block;font-size:10px">' . htmlspecialchars($grpLabel, ENT_QUOTES, 'UTF-8') . '</span>';
                                } ?>
                            </td>
                        </tr>
                        <?php } ?>
                    </tbody>
                    <tfoot>
                        <tr style="font-weight:bold;background:#f8fafc">
                            <td></td>
                            <td>TOTAL</td>
                            <td style="text-align:center"><span class="badge" style="background:#10b981"><?php echo number_format($totalActivities); ?></span></td>
                            <td style="text-align:center"><?php echo $totalSubGroups; ?></td>
                            <td><?php echo $groupCount; ?> groups</td>
                        </tr>
                    </tfoot>
                </table>
            </div>
        </div>
    </div>
</div>

<!-- Sub-Group Details (Expandable) -->
<div class="row" style="margin-bottom:24px">
    <div class="col-lg-12">
        <div class="hpanel">
            <div class="panel-heading">
                <h4><i class="fa fa-list-ul"></i> Sub-Group Drill-Down</h4>
                <small class="text-muted">Click a division to see individual sub-groups with activity counts</small>
            </div>
            <div class="panel-body">
                <div class="panel-group" id="dedDivisions">
                    <?php $i = 0; foreach ($divisions as $dKey => $div) { $i++; ?>
                    <div class="panel panel-default" style="margin-bottom:4px">
                        <div class="panel-heading" style="padding:8px 12px;cursor:pointer" data-toggle="collapse" data-target="#div_<?php echo $i; ?>">
                            <i class="fa <?php echo htmlspecialchars($div['icon'], ENT_QUOTES, 'UTF-8'); ?>" style="width:20px"></i>
                            <strong><?php echo htmlspecialchars($div['label'], ENT_QUOTES, 'UTF-8'); ?></strong>
                            <span class="badge pull-right"><?php echo $div['activities']; ?> activities</span>
                        </div>
                        <div id="div_<?php echo $i; ?>" class="panel-collapse collapse">
                            <div class="panel-body" style="padding:8px 12px">
                                <table class="table table-condensed" style="margin:0">
                                    <?php foreach ($div['sub_groups'] as $sgLabel => $sgCount) { ?>
                                    <tr>
                                        <td><?php echo htmlspecialchars($sgLabel, ENT_QUOTES, 'UTF-8'); ?></td>
                                        <td style="width:80px;text-align:right"><span class="text-muted"><?php echo $sgCount; ?> activities</span></td>
                                        <td style="width:180px">
                                            <?php
                                            $sgResolved = epc_industry_resolve_group($sgLabel);
                                            echo '<span class="label label-default" style="font-size:9px">' . htmlspecialchars($sgResolved, ENT_QUOTES, 'UTF-8') . '</span>';
                                            ?>
                                        </td>
                                    </tr>
                                    <?php } ?>
                                </table>
                            </div>
                        </div>
                    </div>
                    <?php } ?>
                </div>
            </div>
        </div>
    </div>
</div>

<!-- Coverage Audit -->
<div class="row" style="margin-bottom:24px">
    <div class="col-lg-6">
        <div class="hpanel">
            <div class="panel-heading">
                <h4><i class="fa fa-check-circle"></i> Coverage Audit</h4>
            </div>
            <div class="panel-body">
                <?php if (empty($coverage)) { ?>
                <div class="alert alert-success" style="margin:0">
                    <i class="fa fa-check"></i> <strong>100% Coverage</strong> — All <?php echo $groupCount; ?> ecomae groups are mapped to at least one DED division.
                </div>
                <?php } else { ?>
                <div class="alert alert-warning">
                    <i class="fa fa-exclamation-triangle"></i> <strong><?php echo count($coverage); ?> uncovered groups:</strong>
                    <?php foreach ($coverage as $ug) { ?>
                    <span class="label label-danger" style="margin-left:4px"><?php echo htmlspecialchars($ug, ENT_QUOTES, 'UTF-8'); ?></span>
                    <?php } ?>
                </div>
                <?php } ?>
            </div>
        </div>
    </div>
    <div class="col-lg-6">
        <div class="hpanel">
            <div class="panel-heading">
                <h4><i class="fa fa-globe"></i> Worldwide Registries</h4>
            </div>
            <div class="panel-body" style="padding:0">
                <table class="table table-condensed" style="margin:0">
                    <thead><tr><th>Country</th><th>Registry</th><th>Authority</th></tr></thead>
                    <tbody>
                        <?php foreach ($registries as $countryName => $reg) { ?>
                        <tr>
                            <td><strong><?php echo htmlspecialchars($countryName, ENT_QUOTES, 'UTF-8'); ?></strong></td>
                            <td><?php echo htmlspecialchars($reg['name'], ENT_QUOTES, 'UTF-8'); ?></td>
                            <td>
                                <?php if (!empty($reg['url'])) { ?>
                                <a href="<?php echo htmlspecialchars($reg['url'], ENT_QUOTES, 'UTF-8'); ?>" target="_blank" rel="noopener" class="btn btn-xs btn-default"><i class="fa fa-external-link"></i></a>
                                <?php } ?>
                            </td>
                        </tr>
                        <?php } ?>
                    </tbody>
                </table>
            </div>
        </div>
    </div>
</div>

<!-- Targeting Recommendations -->
<div class="row" style="margin-bottom:24px">
    <div class="col-lg-12">
        <div class="hpanel">
            <div class="panel-heading">
                <h4><i class="fa fa-bullseye"></i> Targeting Recommendations</h4>
                <small class="text-muted">High-activity DED divisions with the most business registrations — prime targets for customer acquisition</small>
            </div>
            <div class="panel-body" style="padding:0">
                <?php
                // Sort divisions by activity count descending
                $sorted = $divisions;
                uasort($sorted, function ($a, $b) {
                    return $b['activities'] - $a['activities'];
                });
                $top5 = array_slice($sorted, 0, 5, true);
                ?>
                <table class="table table-striped" style="margin:0">
                    <thead>
                        <tr>
                            <th>Rank</th>
                            <th>Division</th>
                            <th style="text-align:center">Activities</th>
                            <th>% of Total</th>
                            <th>Target Groups</th>
                            <th>Opportunity</th>
                        </tr>
                    </thead>
                    <tbody>
                        <?php $rank = 1; foreach ($top5 as $dKey => $div) {
                            $pct = $totalActivities > 0 ? round(($div['activities'] / $totalActivities) * 100, 1) : 0;
                            ?>
                        <tr>
                            <td><span class="badge" style="background:#f59e0b"><?php echo $rank++; ?></span></td>
                            <td><i class="fa <?php echo htmlspecialchars($div['icon'], ENT_QUOTES, 'UTF-8'); ?>"></i> <strong><?php echo htmlspecialchars($div['label'], ENT_QUOTES, 'UTF-8'); ?></strong></td>
                            <td style="text-align:center"><?php echo number_format($div['activities']); ?></td>
                            <td>
                                <div class="progress" style="margin:0;height:18px;min-width:80px">
                                    <div class="progress-bar progress-bar-info" style="width:<?php echo min($pct * 2, 100); ?>%"><?php echo $pct; ?>%</div>
                                </div>
                            </td>
                            <td>
                                <?php foreach (array_slice($div['ecomae_groups'], 0, 3) as $grp) {
                                    $grpLabel = isset($groups[$grp]) ? $groups[$grp]['label'] : $grp;
                                    echo '<span class="label label-info" style="font-size:9px;margin-right:2px">' . htmlspecialchars($grpLabel, ENT_QUOTES, 'UTF-8') . '</span>';
                                } ?>
                            </td>
                            <td>
                                <span class="text-success"><i class="fa fa-arrow-up"></i> High demand — <?php echo count($div['sub_groups']); ?> sub-sectors</span>
                            </td>
                        </tr>
                        <?php } ?>
                    </tbody>
                </table>
            </div>
        </div>
    </div>
</div>

<!-- Quick Activity Resolution Tool -->
<div class="row" style="margin-bottom:24px">
    <div class="col-lg-12">
        <div class="hpanel">
            <div class="panel-heading">
                <h4><i class="fa fa-magic"></i> Quick Activity Resolution</h4>
                <small class="text-muted">Test how any DED activity name resolves to an ecomae consolidation group</small>
            </div>
            <div class="panel-body">
                <div class="row">
                    <?php
                    $testCases = array(
                        'Cheese Manufacturing',
                        'Crude Oil Extraction',
                        'Casting of Steel and Iron',
                        'Growing of Cereals & Crops',
                        'Jewellery & Precious Metals Trading',
                        'Medical Equipment Trading',
                        'Electronics & IT Equipment Trading',
                        'Building Construction',
                        'Hotels & Resorts',
                        'General Trading',
                        'Fishing & Aquaculture',
                        'Cosmetics & Perfumes Trading',
                    );
                    foreach ($testCases as $tc) {
                        $resolved = epc_industry_resolve_group($tc);
                        $gInfo = isset($groups[$resolved]) ? $groups[$resolved] : null;
                        $color = $gInfo ? '#10b981' : '#ef4444';
                        ?>
                    <div class="col-lg-4 col-sm-6" style="margin-bottom:8px">
                        <div style="padding:8px 12px;background:#f8fafc;border-radius:6px;border-left:3px solid <?php echo $color; ?>">
                            <div style="font-size:12px;color:#64748b"><?php echo htmlspecialchars($tc, ENT_QUOTES, 'UTF-8'); ?></div>
                            <div style="font-size:13px;font-weight:600;color:#0f172a">→ <?php echo htmlspecialchars($gInfo ? $gInfo['label'] : $resolved, ENT_QUOTES, 'UTF-8'); ?></div>
                        </div>
                    </div>
                    <?php } ?>
                </div>
            </div>
        </div>
    </div>
</div>

<script>
(function() {
    // Enable Bootstrap collapse for sub-group drill-down
    if (typeof jQuery !== 'undefined') {
        jQuery('[data-toggle="collapse"]').on('click', function() {
            var target = jQuery(this).data('target');
            jQuery(target).collapse('toggle');
        });
    }
})();
</script>

<?php
epc_cp_page_frame_close();
