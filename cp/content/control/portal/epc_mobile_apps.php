<?php
/**
 * Mobile apps hub — Android/iOS/PWA config (Super CP defaults + tenant overrides).
 */
defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_integrations_helpers.php';
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/users/dp_user.php';

if (!DP_User::isAdmin()) {
	echo '<div class="alert alert-warning">Admin login required.</div>';
	return;
}

global $db_link;
$pdo = ($db_link instanceof PDO) ? $db_link : null;
$isSuper = function_exists('epc_portal_is_super_cp_host') && epc_portal_is_super_cp_host();
$mobile = epc_integrations_mobile_config($pdo);
$defaults = epc_integrations_platform_mobile_defaults();
$backend = epc_int_backend();
$ajaxUrl = '/' . $backend . '/content/control/portal/ajax_integrations.php';
$host = function_exists('epc_portal_host') ? epc_portal_host() : (string) ($_SERVER['HTTP_HOST'] ?? '');
$storefrontUrl = 'https://' . preg_replace('/^www\./', 'www.', strtolower($host)) . '/en/';

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_page_frame.php';
epc_cp_page_frame_open(array(
	'class' => 'epc-mobile-apps',
	'hero' => array(
		'badge' => $isSuper ? 'Super CP' : 'Tenant CP',
		'title' => 'Mobile apps (Android / iOS / PWA)',
		'sub' => 'Capacitor shell + installable PWA. Configure store links, bundle ID, and deep links per tenant.',
		'actions' => array(
			array('label' => 'Integrations hub', 'icon' => 'fa-plug', 'url' => '/' . $backend . '/control/portal/epc_integrations_hub'),
		),
	),
));
?>

<div id="epc-mobile-flash" class="alert" style="display:none"></div>

<form id="epc-mobile-form" class="well" data-ajax-url="<?php echo epc_int_h($ajaxUrl); ?>">
	<input type="hidden" name="action" value="save_mobile">
	<div class="checkbox">
		<label><input type="checkbox" name="enabled" value="1" <?php echo !empty($mobile['enabled']) ? 'checked' : ''; ?>> Mobile app enabled for this tenant</label>
	</div>
	<div class="row">
		<div class="col-md-6">
			<div class="form-group">
				<label>App display name</label>
				<input type="text" class="form-control" name="app_name" value="<?php echo epc_int_h($mobile['app_name']); ?>" placeholder="eParts Cart">
			</div>
		</div>
		<div class="col-md-6">
			<div class="form-group">
				<label>Bundle / package ID</label>
				<input type="text" class="form-control" name="bundle_id" value="<?php echo epc_int_h($mobile['bundle_id']); ?>" placeholder="com.epartscart.app">
			</div>
		</div>
	</div>
	<div class="row">
		<div class="col-md-4">
			<div class="form-group">
				<label>Storefront / API base URL</label>
				<input type="url" class="form-control" name="api_base_url" value="<?php echo epc_int_h($mobile['api_base_url'] ?: $storefrontUrl); ?>">
			</div>
		</div>
		<div class="col-md-4">
			<div class="form-group">
				<label>Deep link scheme</label>
				<input type="text" class="form-control" name="deep_link_scheme" value="<?php echo epc_int_h($mobile['deep_link_scheme'] ?: $defaults['default_deep_link_scheme']); ?>" placeholder="epartscart://">
			</div>
		</div>
		<div class="col-md-4">
			<div class="form-group">
				<label>Universal link domain</label>
				<input type="text" class="form-control" name="deep_link_domain" value="<?php echo epc_int_h($mobile['deep_link_domain']); ?>" placeholder="www.epartscart.com">
			</div>
		</div>
	</div>
	<div class="row">
		<div class="col-md-6">
			<div class="form-group">
				<label>Google Play store URL</label>
				<input type="url" class="form-control" name="play_store_url" value="<?php echo epc_int_h($mobile['play_store_url']); ?>">
			</div>
		</div>
		<div class="col-md-6">
			<div class="form-group">
				<label>Apple App Store URL</label>
				<input type="url" class="form-control" name="app_store_url" value="<?php echo epc_int_h($mobile['app_store_url']); ?>">
			</div>
		</div>
	</div>
	<?php if ($isSuper) { ?>
	<div class="row">
		<div class="col-md-6">
			<div class="form-group">
				<label>Firebase project ID (push template)</label>
				<input type="text" class="form-control" name="firebase_project_id" value="<?php echo epc_int_h($mobile['firebase_project_id']); ?>">
			</div>
		</div>
		<div class="col-md-6">
			<div class="checkbox" style="margin-top:28px">
				<label><input type="checkbox" name="push_enabled" value="1" <?php echo !empty($mobile['push_enabled']) ? 'checked' : ''; ?>> Push notifications (FCM/APNs) enabled</label>
			</div>
		</div>
	</div>
	<?php } ?>
	<div class="checkbox">
		<label><input type="checkbox" name="pwa_enabled" value="1" <?php echo !empty($mobile['pwa_enabled']) ? 'checked' : ''; ?>> PWA manifest + service worker on storefront</label>
	</div>
	<button type="submit" class="btn btn-success"><i class="fa fa-save"></i> Save mobile settings</button>
</form>

<div class="panel panel-default">
	<div class="panel-heading"><strong><i class="fa fa-book"></i> Publish guide</strong></div>
	<div class="panel-body">
		<p><strong>Android:</strong> <code>cd mobile/epartscart-app && npm install && npx cap sync && npx cap open android</code> — Build APK in Android Studio.</p>
		<p><strong>iOS:</strong> Requires macOS + Xcode. Run <code>npx cap add ios</code> once, then <code>npx cap sync ios && npx cap open ios</code>.</p>
		<p><strong>Connect to tenant:</strong> Set <em>API base URL</em> above to this storefront (e.g. <?php echo epc_int_h($storefrontUrl); ?>). Capacitor loads the live site in WebView.</p>
		<p class="text-muted small">See <code>docs/guides/MOBILE_APPS.md</code> and <code>docs/EPARTSCART-MOBILE-PHASE1.md</code>.</p>
	</div>
</div>

<?php epc_cp_page_frame_close(); ?>
