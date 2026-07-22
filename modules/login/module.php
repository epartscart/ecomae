<?php
/**
 * Модуль входа на сайт
*/
defined('_ASTEXE_') or die('No access');

require_once("content/users/dp_user.php");

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getUserSession();
?>
<div id="open_login">
    <span><?php echo translate_str_by_id(4758); ?></span>
</div>
<div id="module_login_box">
    <div id="module_login" style="display: none">
        <?php
        if(DP_User::getUserId() == 0)
        {
            ?>
            <form method="POST" autocomplete="off" data-epc-no-autofill="1">
                <input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
				<input type="hidden" name="authentication" value="true"/>
                <div class="wrong_authentication" id="wrong_authentication">
                </div>
                <input class="login_input" type="text" name="login" value="" placeholder="<?php echo translate_str_by_id(4759); ?>" autocomplete="off" data-epc-secure-field="1" readonly/>
                <input class="login_input" type="password" name="password" value="" placeholder="<?php echo translate_str_by_id(1311); ?>" autocomplete="new-password" data-epc-secure-field="1" readonly/>
                <div id="remember_me_div">
                    <?php echo translate_str_by_id(4666); ?> <input type="checkbox" name="rememberme" value="rememberme"/>
                </div>
                <button type="submit" class="btn"><?php echo translate_str_by_id(4008); ?></button>
            </form>
            <script>
                document.getElementById("open_login").innerHTML = "<span><?php echo translate_str_by_id(4758); ?></span>";
				(function(){
					var form = document.querySelector('#module_login form[data-epc-no-autofill="1"]');
					if (!form) return;
					function unlock(el){ if (el) el.removeAttribute('readonly'); }
					function clearAutofill(){
						var fields = form.querySelectorAll('[data-epc-secure-field="1"]');
						for (var i = 0; i < fields.length; i++) { if (fields[i].value) fields[i].value = ''; }
					}
					clearAutofill();
					setTimeout(clearAutofill, 50);
					setTimeout(clearAutofill, 300);
					var secure = form.querySelectorAll('[data-epc-secure-field="1"]');
					for (var i = 0; i < secure.length; i++) {
						(function(el){
							el.addEventListener('focus', function(){ unlock(el); }, true);
							el.addEventListener('mousedown', function(){ unlock(el); }, true);
						})(secure[i]);
					}
				})();
            </script>
            
            <a href="<?php echo $multilang_params['lang_href']; ?>/users/registration" class="btn btn-success"><?php echo translate_str_by_id(3987); ?></a> 
            <a href="<?php echo $multilang_params['lang_href']; ?>/users/forgot_password" class="btn"><?php echo translate_str_by_id(4667); ?></a>
            
            <?php
        }
        else
        {
            ?>
                <div id="greeting">
                    <?php echo translate_str_by_id(4760); ?>
                </div>
                <div id="self_data_control">
                    <a href="<?php echo $multilang_params['lang_href']; ?>/users/profile" class="btn btn-success"><?php echo translate_str_by_id(4668); ?></a> 
                    <a href="<?php echo $multilang_params['lang_href']; ?>/users/editform" class="btn"><?php echo translate_str_by_id(4761); ?></a>
                </div>
                <form method="POST">
					<input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
                    <input type="hidden" name="logout" value="true"/>
                    <button type="submit" class="btn"><?php echo translate_str_by_id(3996); ?></button>
                </form>
                <script>
					<?php
					//Выводим имя пользователя
					$user_profile = DP_User::getUserProfile();
					$user_name_show = '';
					if( isset( $user_profile["name"] ) )
					{
						$user_name_show = $user_profile["name"];
					}
					if( isset($user_profile["surname"]) )
					{
						if( $user_name_show != '' )
						{
							$user_name_show = $user_name_show.' ';
						}
						$user_name_show = $user_name_show.$user_profile["surname"];
					}
					if( $user_name_show == '' )
					{
						$user_name_show = translate_str_by_id(4762);
					}
					?>
                    document.getElementById("open_login").innerHTML = "<span><?php echo $user_name_show; ?></span>";
                </script>
            <?php
        }
        ?>
    </div>
</div>


<script>
$("#open_login").click(function(){
    	if ( $("#module_login").css('display') == 'none' ) 
    	{
    	    $("#module_login").show(400);
    	}
    	else
    	{
    	    $("#module_login").hide(200);
    	}
	});
</script>