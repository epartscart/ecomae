<?php
/**
 * Модуль формы выхода из бэкэнда
*/
defined('_ASTEXE_') or die('No access');

//Для работы с пользователем
require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
$user_session = DP_User::getAdminSession();
?>

<div id="logout_form_container">
    <?php
    //Получаем данные пользователя
    $admin_profile = DP_User::getAdminProfile();
    ?>
    
    <div><?php echo translate_str_by_id(3995); ?>, <?php echo $admin_profile["name"] ?>!</div>
    <form id="logout_form" method="POST" name="logout_form">
        <input type="hidden" name="csrf_guard_key" value="<?php echo $user_session["csrf_guard_key"]; ?>" />
		<input type="hidden" name="logout" value="logout" />
        <a href="javascript:void(0);" onclick="document.forms['logout_form'].submit();"><?php echo translate_str_by_id(3996); ?></a>
    </form>
</div>