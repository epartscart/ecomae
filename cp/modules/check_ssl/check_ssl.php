<?php

defined('_ASTEXE_') or die('No access');

require_once $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_cp_top_alerts.php';

// Для SSL
$ssl_state = 0;//Индикатор: SSL есть, redirect есть 11; SSL есть, redirect нет 12; SSL нет, redirect есть 21; SSL нет, redirect нет 22

$url = $DP_Config->domain_path;
$orignal_parse = parse_url($url, PHP_URL_HOST);
$context = stream_context_create(
    array(
        'ssl' => array(
            'capture_peer_cert' => true,
            'verify_peer'       => false, // Т.к. промежуточный сертификат может отсутствовать,
            'verify_peer_name'  => false  // отключение его проверки.
        )
    )
);


//На случай возникновения warning - устанавливаем обработчик
set_error_handler(function() {});
//Вызов функции, где может возникнуть warning
$read = stream_socket_client("ssl://".$orignal_parse.":443", $err_no, $err_str, 30, STREAM_CLIENT_CONNECT, $context);
//После вызова функции stream_socket_client, в которой мог возникнуть warning, возвращаем старый обработчик ошибок
restore_error_handler();

if($read)
{
	$cert = stream_context_get_params($read);
}


if (empty($err_no)) {
    // $certinfo = openssl_x509_parse($cert['options']['ssl']['peer_certificate']); Просмотр содержимого
    $ssl_state = 1;
}
else $ssl_state = 2;

if (epc_cp_https_redirect_is_configured($DP_Config)) {
    $ssl_state .= 1;
} else {
    $ssl_state .= 2;
}


switch($ssl_state)
{
    case 11:
        $ssl_status_text = translate_str_by_id(5585);
        $ssl_a_style = "";
        $ssl_sign_after = '';
        break;
    case 12:
        $ssl_status_text = translate_str_by_id(5586);
        $ssl_a_style = "background-color:#f3fa37;color:#000;";
        $ssl_sign_after = '<i class="pe-7s-info"></i>';
        break;
    case 21:
        $ssl_status_text = translate_str_by_id(5587);
        $ssl_a_style = "background-color:#f3fa37;color:#000;";
        $ssl_sign_after = '<i class="pe-7s-info"></i>';
        break;
    case 22:
        $ssl_status_text = translate_str_by_id(5588);
        $ssl_a_style = "background-color:#F00;color:#FFF;";
        $ssl_sign_after = '<i class="pe-7s-attention"></i>';
        break;
}
epc_cp_top_alerts_render_ssl_item();