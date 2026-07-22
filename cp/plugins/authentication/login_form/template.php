<!DOCTYPE html>
<html>
<head>
	<base href="/<?php echo $DP_Config->backend_dir; ?>/templates/bootstrap_admin/">
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta http-equiv="X-UA-Compatible" content="IE=edge">

    <!-- Page title -->
    <docpart type="head" name="head" />

    <!-- Vendor styles -->
<?php
// Login: prefer CDN vendor CSS (cacheable, small) — local epc-static.php is DYNAMIC and slower.
?>
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/4.7.0/css/font-awesome.min.css" />
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap/3.4.1/css/bootstrap.min.css" />
    <!-- App styles (minimal for login — skip pe-icon / full Homer suite) -->
    <link rel="stylesheet" href="/epc-static.php?f=cp/templates/bootstrap_admin/styles/style.css">
<?php
require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_professional_shell.php';
epc_cp_shell_enqueue_assets(true);
echo epc_cp_shell_inline_style_block();
$epcLogin = epc_cp_login_context();
?>

</head>
<body class="blank epc-cp epc-cp-login epc-cp-login-hero epc-cp-shell <?php echo htmlspecialchars($epcLogin['body_class'], ENT_QUOTES, 'UTF-8'); ?> <?php echo htmlspecialchars(epc_cp_shell_body_classes(), ENT_QUOTES, 'UTF-8'); ?>">


<?php
extract(epc_brand_cp_context());
$loginHeading = $epcLogin['heading'];
$loginSub = $epcLogin['sub'];
$loginBadge = $epcLogin['badge'];
$loginTagline = $epcLogin['tagline'];
$epcLoginCentered = in_array($epcLogin['type'], array('tenant', 'super', 'client_erp', 'demo_erp_only', 'platform_erp'), true);
?>



<!-- Simple splash screen-->
<div class="splash"> <div class="color-line"></div><div class="splash-title"><h1><?php echo htmlspecialchars($loginHeading, ENT_QUOTES, 'UTF-8'); ?></h1><p><?php echo translate_str_by_id(4001); ?>... </p><div class="spinner"> <div class="rect1"></div> <div class="rect2"></div> <div class="rect3"></div> <div class="rect4"></div> <div class="rect5"></div> </div> </div> </div>
<script>(function(){function epcHideCpSplash(){var s=document.querySelector('.splash');if(s){s.style.display='none';}}if(document.readyState==='loading'){document.addEventListener('DOMContentLoaded',epcHideCpSplash);}else{epcHideCpSplash();}window.addEventListener('load',epcHideCpSplash);setTimeout(epcHideCpSplash,3500);})();</script>
<!--[if lt IE 7]>
<p class="alert alert-danger">You are using an <strong>outdated</strong> browser. Please <a href="http://browsehappy.com/">upgrade your browser</a> to improve your experience.</p>
<![endif]-->

<div class="color-line"></div>

<div id="epcCpParticles" class="epc-cp-particles"></div>

<div class="epc-cp-login-shell">
	<div class="epc-cp-login-brand">
		<div class="epc-cp-login-brand-inner">
			<span class="epc-cp-login-brand-badge"><?php echo htmlspecialchars($loginBadge, ENT_QUOTES, 'UTF-8'); ?></span>
			<h1><?php echo htmlspecialchars($loginHeading, ENT_QUOTES, 'UTF-8'); ?></h1>
			<p class="epc-cp-login-brand-sub"><?php echo htmlspecialchars($loginSub, ENT_QUOTES, 'UTF-8'); ?></p>
			<?php if ($loginTagline !== '') { ?>
			<p class="epc-cp-login-brand-tagline"><?php echo htmlspecialchars($loginTagline, ENT_QUOTES, 'UTF-8'); ?></p>
			<?php } ?>
			<div class="epc-cp-login-visual">
				<?php echo epc_cp_login_hero_markup(); ?>
			</div>
			<ul class="epc-cp-login-features">
				<?php foreach ($epcLogin['features'] as $icon => $label) { ?>
				<li><i class="fa <?php echo htmlspecialchars($icon, ENT_QUOTES, 'UTF-8'); ?>"></i> <?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></li>
				<?php } ?>
			</ul>
		</div>
	</div>
	<div class="epc-cp-login-panel">
<?php if ($epcLoginCentered) { ?>
		<div class="epc-cp-login-card-head">
			<?php if ($epcLogin['type'] === 'super') { ?>
			<div class="epc-cp-login-card-head__logo"><?php echo epc_ecomae_static_logo('compact', array('show_tagline' => false)); ?></div>
			<?php } ?>
			<span class="epc-cp-login-card-head__badge"><?php echo htmlspecialchars($loginBadge, ENT_QUOTES, 'UTF-8'); ?></span>
			<h1><?php echo htmlspecialchars($loginHeading, ENT_QUOTES, 'UTF-8'); ?></h1>
			<p><?php echo htmlspecialchars($loginSub, ENT_QUOTES, 'UTF-8'); ?></p>
		</div>
<?php } ?>
<div class="login-container">
    <div class="row">
        <div class="col-md-12">
            <div class="text-center m-b-md">
                <h3><?php echo htmlspecialchars($loginHeading, ENT_QUOTES, 'UTF-8'); ?></h3>
                <small><?php echo htmlspecialchars($loginSub, ENT_QUOTES, 'UTF-8'); ?></small>
            </div>
            <div class="hpanel">
                <div class="panel-body">
						<div class="wrong_authentication" id="wrong_authentication"></div>
<?php
$epcAuthSocialFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_auth_social.php';
if (is_file($epcAuthSocialFile)) {
	require_once $epcAuthSocialFile;
	$epcAuthUi = epc_auth_login_context_for_ui();
	echo epc_cp_login_modern_auth_html($epcAuthUi);
}
?>
                        <form id="login_form" method="POST" autocomplete="off" data-epc-no-autofill="1">
							<input type="hidden" name="authentication" value="authentication"/>
							<input type="text" name="epc_autofill_trap_user" value="" tabindex="-1" aria-hidden="true" autocomplete="username" style="position:absolute;left:-9999px;width:1px;height:1px;opacity:0;" />
							<input type="password" name="epc_autofill_trap_pass" value="" tabindex="-1" aria-hidden="true" autocomplete="current-password" style="position:absolute;left:-9999px;width:1px;height:1px;opacity:0;" />
                            
							
							<div class="form-group">
								<label class="control-label" for="auth_contact_select"><?php echo translate_str_by_id(4016); ?></label>
								<select class="form-control" name="auth_contact_select" id="auth_contact_select" onchange="on_auth_contact_select_changed();" autocomplete="off">
									<option value="email">E-mail</option>
									<option value="phone"><?php echo translate_str_by_id(1312); ?></option>
								</select>
							</div>
							<div class="form-group">
								<label for="auth_contact_input" class="control-label" id="auth_contact_label"></label>
								<input type="text" placeholder="" title="" value="" name="auth_contact" id="auth_contact_input" class="form-control" autocomplete="off" autocapitalize="off" autocorrect="off" spellcheck="false" data-epc-secure-field="1" readonly />
                            </div>
							<script>
							//Обработка выбора контакта
							function on_auth_contact_select_changed()
							{
								if( document.getElementById("auth_contact_select").value == "email" )
								{
									document.getElementById("auth_contact_label").innerHTML = "E-mail";
									document.getElementById("auth_contact_input").setAttribute("placeholder", "<?php echo translate_str_by_id(4017); ?>");
								}
								else
								{
									document.getElementById("auth_contact_label").innerHTML = "<?php echo translate_str_by_id(1312); ?>";
									document.getElementById("auth_contact_input").setAttribute("placeholder", "<?php echo translate_str_by_id(4018); ?>");
								}
							}
							on_auth_contact_select_changed();
							</script>
							
							
                            <div class="form-group">
                                <label class="control-label" for="password"><?php echo translate_str_by_id(1311); ?></label>
                                <input type="password" title="Please enter your password" placeholder="<?php echo translate_str_by_id(4019); ?>" required="" value="" name="password" id="password" class="form-control" autocomplete="new-password" data-epc-secure-field="1" readonly>
                            </div>
                            <button type="submit" class="btn btn-success btn-block"><?php echo translate_str_by_id(4008); ?></button>
                        </form>
						<script>
						(function(){
							var form = document.getElementById('login_form');
							if (!form) return;
							function unlock(el){ if (el) el.removeAttribute('readonly'); }
							function clearAutofill(){
								var fields = form.querySelectorAll('[data-epc-secure-field="1"]');
								for (var i = 0; i < fields.length; i++) {
									if (fields[i].value) fields[i].value = '';
								}
								var traps = form.querySelectorAll('input[name^="epc_autofill_trap_"]');
								for (var t = 0; t < traps.length; t++) traps[t].value = '';
							}
							clearAutofill();
							setTimeout(clearAutofill, 50);
							setTimeout(clearAutofill, 300);
							setTimeout(clearAutofill, 1000);
							var secure = form.querySelectorAll('[data-epc-secure-field="1"]');
							for (var i = 0; i < secure.length; i++) {
								(function(el){
									el.addEventListener('focus', function(){ unlock(el); }, true);
									el.addEventListener('mousedown', function(){ unlock(el); }, true);
									el.addEventListener('touchstart', function(){ unlock(el); }, true);
								})(secure[i]);
							}
						})();
						</script>
<?php if ($epcLoginCentered && !empty($epcLogin['features'])) { ?>
						<ul class="epc-cp-login-features epc-cp-login-features--card">
							<?php foreach ($epcLogin['features'] as $icon => $label) { ?>
							<li><i class="fa <?php echo htmlspecialchars($icon, ENT_QUOTES, 'UTF-8'); ?>"></i> <?php echo htmlspecialchars($label, ENT_QUOTES, 'UTF-8'); ?></li>
							<?php } ?>
						</ul>
<?php } ?>
                </div>
            </div>
        </div>
    </div>
    <div class="row login-footer">
        <div class="col-md-12 text-center">
            <?php echo $product_description; ?><br/><?php echo $brand_copyright; ?>
			<?php if (!empty($hosted_by_html)) { ?>
			<div class="epc-cp-login-hosted"><?php echo $hosted_by_html; ?></div>
			<?php } ?>
        </div>
    </div>
</div>
	</div>
</div>


<!-- Vendor scripts (login only — no metisMenu / Homer / iCheck) -->
<script src="https://cdnjs.cloudflare.com/ajax/libs/jquery/3.6.0/jquery.min.js"></script>
<script src="https://cdnjs.cloudflare.com/ajax/libs/twitter-bootstrap/3.4.1/js/bootstrap.min.js" defer></script>

<?php
//Маска ввода телефона
require_once($_SERVER["DOCUMENT_ROOT"]."/lib/inputmask/phone_mask.php");
?>

<script>
(function(){
    var container = document.getElementById('epcCpParticles');
    if (!container) return;
    if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        container.style.display = 'none';
        return;
    }
    // Keep login light for tenants (was 180 DOM nodes + continuous animations).
    var colors = [
        'rgba(14, 165, 233, .55)',
        'rgba(56, 189, 248, .45)',
        'rgba(255, 255, 255, .25)'
    ];
    var totalParticles = 28;
    var frag = document.createDocumentFragment();
    for (var i = 0; i < totalParticles; i++) {
        var dot = document.createElement('span');
        var size = 1.5 + Math.random() * 2.5;
        var speed = 8 + Math.random() * 10;
        var color = colors[Math.floor(Math.random() * colors.length)];
        dot.className = 'epc-cp-particle';
        dot.style.cssText = 'width:' + size + 'px;height:' + size + 'px;'
            + 'left:' + (Math.random() * 100) + '%;top:-' + (size + 4) + 'px;'
            + 'background:' + color + ';'
            + 'animation:epcCpFloat ' + speed + 's linear ' + (-(Math.random() * speed)) + 's infinite;'
            + 'border-radius:50%;opacity:' + (0.35 + Math.random() * 0.45) + ';';
        frag.appendChild(dot);
    }
    container.appendChild(frag);
})();
</script>

</body>
</html>
