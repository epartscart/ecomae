<?php
// Подключение к БД
require_once($_SERVER["DOCUMENT_ROOT"] . "/config.php");
$DP_Config = new DP_Config; // Конфигурация CMS

// Создание соединения с БД
try {
    $db_link = new PDO('mysql:host=' . $DP_Config->host . ';dbname=' . $DP_Config->db, $DP_Config->user, $DP_Config->password);
} catch (PDOException $e) {
    $answer = array();
    $answer["result"] = false;
    exit(json_encode($answer));
}
$db_link->query("SET NAMES utf8;");

// Подключение файла для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"] . "/content/users/dp_user.php");

// Получение ID пользователя
$user_id = DP_User::getUserId();

// Получение POST данных
$id = $_POST['id'];
$order_id = $_POST['order_id'];
$sum = $_POST['pay_amount'];
$currency = $_POST['currency'];
$user_id = $_POST['user_id'];
$signature = $_POST['signature'];
$comment = $_POST['comment'];

// Лог для отладки
function writeLog($message) {
    $logFile = 'payment_log.txt';
    $message = date('Y-m-d H:i:s') . ': ' . $message . PHP_EOL;
    file_put_contents($logFile, $message, FILE_APPEND);
}

// Функция для проверки платежа
function validatePayment($id, $order_id, $sum, $db_link) {
    // Проверяем, что операция не активирована
    $account_data_query = $db_link->prepare('SELECT * FROM `shop_users_accounting` WHERE `id` = ? AND `active` = 0;');
    $account_data_query->execute([$order_id]);
    $account_data_record = $account_data_query->fetch();

    if ($account_data_record === false) {
        return false;
    }

    // Получаем сумму из базы данных
    $amount_query = $db_link->prepare('SELECT `amount` FROM `shop_users_accounting` WHERE `id` = ?;');
    $amount_query->execute([$order_id]);
    $amount_record = $amount_query->fetch();

    if ($amount_record === false) {
        return false;
    }

    $amount_from_db = $amount_record["amount"];

    // Сравниваем сумму из запроса и сумму из базы данных
    if (($amount_from_db * 100) != $sum) {
        writeLog("Ошибка: Сумма из запроса ($sum) не соответствует сумме из БД ($amount_from_db)");
        return false;
    }

    // Вызываем уведомление менеджеров магазинов
    $operation_id = $order_id;
    require_once($_SERVER["DOCUMENT_ROOT"] . "/content/shop/finance/pay_notify.php");

    // Вызываем протокол оплаты заказа, если номер заказа указан
    $operation_id = $order_id;
    require_once($_SERVER["DOCUMENT_ROOT"] . "/content/shop/finance/pay_for_order.php");

    // Оплата успешно обработана
    writeLog("Платеж успешно обработан для заказа $order_id");
    return true;
}

// Обработка платежа
if (validatePayment($id, $order_id, $sum, $db_link)) {
    echo "GOOD";
} else {
    // Ошибка при обработке платежа
    echo "BAD";
}
?>
