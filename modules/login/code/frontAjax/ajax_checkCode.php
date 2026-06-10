<?php
ini_set('display_errors', 0);

    require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");

    //Соединение с основной БД
    $DP_Config = new DP_Config;//Конфигурация CMS
    $epcPortalFile = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_portal.php';
    if (is_file($epcPortalFile)) {
        require_once $epcPortalFile;
        if (function_exists('epc_portal_apply_config')) {
            epc_portal_apply_config($DP_Config);
        }
    }

    //Подключение к БД
    try
    {
        $db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
    }
    catch (PDOException $e)
    {
        exit("No DB connect");
    }
    $db_link->query("SET NAMES utf8;");
	
	
	// -------------------------------------------------------------------------------
	//Подключение мультиязычности
	require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
	$multilang_params = multilang_init();
	// -------------------------------------------------------------------------------
	
	
    require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
    $user_session = DP_User::getUserSession();

    $_POST = json_decode(file_get_contents("php://input"), true);

    // -------------------------------------------------------------------------------
    //Защита от CSRF-атак
    require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
    // -------------------------------------------------------------------------------

    $_POST = json_decode(file_get_contents("php://input"), true);

    $session_data = json_decode($user_session["data"], true);

    $faCode = $user_session["2fa_code"];
    $faAttempts = $user_session["2fa_attempts"];

    if ($faAttempts < 1)
        exit(json_encode(["status" => 501, "message" => translate_str_by_id(4003)]));
    else if ($faCode == $_POST["code"])
        if ($session_data["expireFaCode"] < time())
            exit(json_encode(["status" => 501, "message" => translate_str_by_id(5642)]));
        else
            exit(json_encode(["status" => 200]));

    else
    {
        $faAttempts = $faAttempts - 1;
        $sqlUpdateAttempt = $db_link->prepare("UPDATE `sessions` SET `2fa_attempts` = ? WHERE `session` = ?;");
        $sqlUpdateAttempt->execute([$faAttempts, $user_session["session"]]);

        exit(json_encode(["status" => 501, "message" => translate_str_by_id(5643).": " . $faAttempts . "."]));
    }

    exit(json_encode(["status" => 200]));
?>