<?php
require_once($_SERVER["DOCUMENT_ROOT"]."/config.php");
$DP_Config = new DP_Config;
try
{
    $db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
}
catch (PDOException $e)
{
    $result["status"] = false;
    $result["message"] = "DB connect error";
    $result["code"] = 502;
    exit(json_encode($result));
}
$db_link->query("SET NAMES utf8;");

// -------------------------------------------------------------------------------
//Защита от CSRF-атак
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/stop_csrf.php");
// -------------------------------------------------------------------------------

$answer = [
    "status" => true,
];

$items_query = " ?";
$bind_val_items = [explode(",", $_POST["items_id"])[0]];

for ($i = 1; $i < count(explode(",", $_POST["items_id"])); $i++)
{
    $items_query .= ", ?";
    $bind_val_items[] = explode(",", $_POST["items_id"])[$i];
}

$reason_query = $db_link->prepare("SELECT COUNT(*) as `count` FROM `shop_orders_returns_items` WHERE `item_id` IN (".$items_query.");");
$reason_query->execute($bind_val_items);

$result = $reason_query->fetch();

$answer["count_confirm"] = (int)$result["count"];

$reason_query_items = $db_link->prepare("SELECT COUNT(*) as `count` FROM `shop_orders_items` WHERE `status` IN (SELECT `id` FROM `shop_orders_items_statuses_ref` WHERE `check_for_return` = 1) AND `id` IN (".$items_query.");");
$reason_query_items->execute($bind_val_items);

$result_items = $reason_query_items->fetch();
$answer["count_complete"] = (int)$result_items["count"];
$answer["all_complete"]  = $answer["count_complete"] == count(explode(",", $_POST["items_id"]));
exit(json_encode($answer));
?>
