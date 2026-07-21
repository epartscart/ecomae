<?php
/**
 * Страничный скрипт для отображения заказов
 * 
 * Заказы отображаются:
 * - от тех офисов, для которых данный пользователь назначен менеджером;
 * - в соответствии с фильтром;
 * - упорядоченные по определенному полю;
 * - ограниченный диапазон
*/
defined('_ASTEXE_') or die('No access');

$epcOrdersGuard = $_SERVER['DOCUMENT_ROOT'] . '/content/general_pages/epc_tenant_data_guard.php';
if (is_file($epcOrdersGuard)) {
	require_once $epcOrdersGuard;
	if (function_exists('epc_tenant_data_guard_active') && epc_tenant_data_guard_active()) {
		echo epc_tenant_data_guard_banner('orders');
		echo '<div class="hpanel"><div class="panel-body"><p class="text-muted" style="margin:0">'
			. 'No orders are shown while this tenant shares the platform commerce database.'
			. '</p></div></div>';
		return;
	}
}

function str_replace_once($search, $replace, $text) 
{ 
   $pos = strpos($text, $search); 
   return $pos!==false ? substr_replace($text, $replace, $pos, strlen($search)) : $text; 
}

// формируем пагинацию
// $all 		= количество постов в категории (определяем количество постов в базе данных)
// $lim 		= количество постов, размещаемых на одной странице
// $prev 		= количество отображаемых ссылок до и после номера текущей страницы
// $curr_link 	= номер текущей страницы (получаем из URL)
// $curr_css 	= css-стиль для ссылки на "текущую (активную)" страницу
// $link 		= часть адреса, используемый для формирования линков на другие страницы
function pagination($all, $lim, $prev, $curr_link, $curr_css, $link)
{

	// print_r($all);
	// echo "</br>";
	// print_r($lim);
	// echo "</br>";
	// print_r($prev);
	// echo "</br>";
	// print_r($curr_link);
	// echo "</br>";

    $html = '';
	// осуществляем проверку, чтобы выводимые первая и последняя страницы
    // не вышли за границы нумерации
    $first = $curr_link - $prev;
    if ($first < 1) $first = 0;
		$last = $curr_link + $prev;
		
		$count_pages = (int)($all / $lim);//Количество страниц
		if($all%$lim)//Если остались еще элементы
		{
			$count_pages++;
		}

		$count_pages = $count_pages - 1;

    if ($last > $count_pages) $last = $count_pages;
 
    // начало вывода нумерации
    // выводим первую страницу
    $y = 0;
    if ($first > 0) $html .= "<li class='paginate_button'><a onclick='goToPage({$y})'>0</a></li>";
    // Если текущая страница далеко от 1-й (>10), то часть предыдущих страниц
    // скрываем троеточием
    // Если текущая страница имеет номер до 10, то выводим все номера
    // перед заданным диапазоном без скрытия
	// $prev
    $y = $first - 1;
    if ($first > $prev) {
        $html .= "<li class='paginate_button'><a onclick='goToPage({$y})'>...</a></li>";
    } else {
        for($i = 2;$i < $first;$i++){
            $html .=  "<li class='paginate_button'><a onclick='goToPage({$y})'>$i</a></li>";
        }
    }
    // отображаем заданный диапазон: текущая страница +-$prev
    for($i = $first;$i < $last + 1;$i++){
        // если выводится текущая страница, то ей назначается особый стиль css
        if($i == $curr_link) {
			$html .= "<li class='paginate_button ".$curr_css."'><a>". $i ."</a></li>";
        } else {
            $alink = "<li class='paginate_button'><a onclick='goToPage(";
            if($i != 0) $alink .= "{$i}";
            $alink .= ")'>$i</a></li>";
            $html .= $alink;
        }
    }
    $y = $last + 1;
    // часть страниц скрываем троеточием
    if ($last < $count_pages && $count_pages - $last > 2) $html .=  "<li class='paginate_button'><a onclick='goToPage({$y})'>...</a></li>";
    // выводим последнюю страницу
    $e = $count_pages;
    if ($last < $count_pages) $html .=  "<li class='paginate_button'><a onclick='goToPage({$e})'>$e</a></li>";
	
	return $html;
}


//Для работы с пользователями
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");
$manager_id = DP_User::getAdminId();//ID менежера, который отображает эту страницу

//Технические данные для работы с заказами
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/order_process/orders_background.php");
$epc_orders_process_dir = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/order_process';
?>


<?php
if(!empty($_POST["action"]))
{
    
}
else//Действий нет - выводим страницу
{
	$epc_orders_process_dir = $_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/shop/order_process';
	$epc_orders_page_error = '';
	try {
		$epc_orders_ws_helpers = $epc_orders_process_dir . '/epc_orders_workspace_helpers.php';
		if (!is_file($epc_orders_ws_helpers)) {
			throw new Exception('Helper file not found: ' . $epc_orders_ws_helpers);
		}
		require_once $epc_orders_ws_helpers;
	} catch (Exception $e) {
		$epc_orders_page_error = $e->getMessage();
	} catch (Throwable $e) {
		$epc_orders_page_error = $e->getMessage();
	}
	if ($epc_orders_page_error !== '') {
		echo '<div class="alert alert-danger"><strong>Orders list could not load:</strong> '
			. htmlspecialchars($epc_orders_page_error, ENT_QUOTES, 'UTF-8') . '</div>';
		return;
	}

	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
    ?>
    <?php
        require_once($_SERVER['DOCUMENT_ROOT'] . '/' . $DP_Config->backend_dir . '/content/control/actions_alert.php');//Вывод сообщений о результатах действий
    ?>
    <?php
    $epc_orders_kpi = epc_orders_ws_kpi($db_link, $offices_list, $manager_id);
    $epc_orders_selected_id = isset($_GET['order_id']) ? (int) $_GET['order_id'] : 0;
	$epc_orders_open_ids = epc_orders_ws_open_status_ids($db_link);
	$epc_orders_completed_ids = epc_orders_ws_completed_status_ids($db_link);
	$epc_orders_tab = epc_orders_ws_tab_from_cookie();
	$epc_orders_force_tab_defaults = !isset($_COOKIE['orders_tab']) || !isset($_COOKIE['orders_filter']);
	$epc_orders_office_ids = array_keys($offices_list);
	$epc_orders_completed_count = epc_orders_ws_count_by_statuses($db_link, $epc_orders_office_ids, $epc_orders_completed_ids);
	$epc_orders_open_count = epc_orders_ws_count_by_statuses($db_link, $epc_orders_office_ids, $epc_orders_open_ids);
    ?>
	<div class="col-lg-12 epc-scp-panel epc-scp-orders-page epc-orders-page">
		<div class="epc-orders-page__hero">
			<div>
				<h2><i class="fa fa-shopping-basket"></i> Orders · OMS</h2>
				<p>One-window order management: list on the left, full console on the right — items, fulfillment, payment, courier VAT, documents, and WhatsApp. Click KPI cards to filter; use keyboard shortcuts for speed.</p>
			</div>
			<div class="epc-orders-page__hero-actions">
				<a class="btn btn-primary btn-sm" href="/<?php echo htmlspecialchars($DP_Config->backend_dir, ENT_QUOTES, 'UTF-8'); ?>/shop/orders/oms-guide"><i class="fa fa-book"></i> OMS daily guide</a>
				<button type="button" class="btn btn-default btn-sm" onclick="sortOrders('id');" title="Order number sequence"><i class="fa fa-sort-numeric-desc"></i> By order #</button>
				<button type="button" class="btn btn-default btn-sm" onclick="sortOrders('last_modified');" title="Last activity / modification"><i class="fa fa-clock-o"></i> By last modified</button>
			</div>
		</div>
		<div class="epc-scp-kpi epc-scp-orders-kpi">
			<div class="epc-scp-kpi__card is-clickable" role="button" tabindex="0" onclick="ordersOpenTab();" onkeydown="if(event.key==='Enter')ordersOpenTab();" title="Show open orders">
				<div class="epc-scp-kpi__label">Open orders</div>
				<div class="epc-scp-kpi__val"><?php echo (int) $epc_orders_open_count; ?></div>
				<div class="epc-scp-kpi__hint">Click to filter · active pipeline</div>
			</div>
			<div class="epc-scp-kpi__card is-clickable" role="button" tabindex="0" onclick="ordersTodayTab();" onkeydown="if(event.key==='Enter')ordersTodayTab();" title="Orders created today">
				<div class="epc-scp-kpi__label">Today</div>
				<div class="epc-scp-kpi__val"><?php echo (int) $epc_orders_kpi['today']; ?></div>
				<div class="epc-scp-kpi__hint">Click to filter · new today</div>
			</div>
			<div class="epc-scp-kpi__card is-clickable" role="button" tabindex="0" onclick="ordersPendingShipTab();" onkeydown="if(event.key==='Enter')ordersPendingShipTab();" title="Paid orders not finished">
				<div class="epc-scp-kpi__label">Pending ship</div>
				<div class="epc-scp-kpi__val"><?php echo (int) $epc_orders_kpi['pending_ship']; ?></div>
				<div class="epc-scp-kpi__hint">Click to filter · paid, not finished</div>
			</div>
		</div>
		<p class="epc-oms-keys-hint"><i class="fa fa-keyboard-o"></i> Shortcuts: <kbd>j</kbd>/<kbd>k</kbd> or ↑/↓ next/prev order · <kbd>1</kbd>–<kbd>8</kbd> console tabs · <kbd>Ctrl</kbd>+<kbd>S</kbd> save line / all lines</p>
		<div class="epc-orders-tabs" role="tablist" aria-label="Orders tabs">
			<button type="button" class="epc-orders-tab<?php echo $epc_orders_tab === 'open' ? ' is-active' : ''; ?>" onclick="ordersOpenTab();">Open <span class="epc-tab-count"><?php echo (int) $epc_orders_open_count; ?></span></button>
			<button type="button" class="epc-orders-tab is-completed<?php echo $epc_orders_tab === 'completed' ? ' is-active' : ''; ?>" onclick="ordersCompletedTab();">Completed <span class="epc-tab-count"><?php echo (int) $epc_orders_completed_count; ?></span></button>
			<button type="button" class="epc-orders-tab<?php echo $epc_orders_tab === 'all' ? ' is-active' : ''; ?>" onclick="ordersAllTab();">All</button>
		</div>
		<div class="epc-orders-toolbar">
			<?php
			$epc_skip_pill_ids = array_flip(array_merge($epc_orders_completed_ids, array()));
			// Also skip canceled (for_inverse) via badge class lookup set
			$epc_inverse_ids = array();
			$epc_inv_q = $db_link->query("SELECT `id` FROM `shop_orders_statuses_ref` WHERE `for_inverse` = 1");
			if ($epc_inv_q) {
				while ($epc_inv_r = $epc_inv_q->fetch(PDO::FETCH_ASSOC)) {
					$epc_inverse_ids[] = (int) $epc_inv_r['id'];
				}
			}
			$epc_skip_pill_ids = array_flip(array_merge($epc_orders_completed_ids, $epc_inverse_ids));
			foreach ($orders_statuses as $pill_status_id => $pill_status_data) {
				if (isset($epc_skip_pill_ids[(int) $pill_status_id])) {
					continue; // Completed / Canceled live in their own tabs or out of Open toolbar
				}
				$pill_cls = epc_orders_ws_badge_class((int) $pill_status_id, $db_link);
				?>
			<a class="epc-scp-badge <?php echo epc_orders_ws_h($pill_cls); ?>" href="javascript:void(0);" onclick="epcFilterByStatus(<?php echo (int) $pill_status_id; ?>);"><?php echo epc_orders_ws_h(translate_str_by_id($pill_status_data['name'])); ?></a>
			<?php } ?>
			<span class="epc-orders-toolbar__hint"><i class="fa fa-columns"></i> Click a row to open OMS on the right · Ctrl+click → classic full card</span>
		</div>
	<div class="epc-scp-orders-filter epc-orders-filter-panel is-collapsed">
		<div class="hpanel">
			<div class="panel-heading hbuilt" onclick="if(window.epcToggleOrdersFilter){epcToggleOrdersFilter();}">
				<div class="panel-tools">
					<a class="showhide" href="javascript:void(0);" onclick="event.stopPropagation();if(window.epcToggleOrdersFilter){epcToggleOrdersFilter();}return false;"><i class="fa fa-chevron-down"></i></a>
				</div>
				<?php echo translate_str_by_id(3574); ?> <span class="epc-filter-toggle">Advanced · click to expand</span> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="event.stopPropagation();show_hint('<?php echo translate_str_by_id(5310); ?>');"><i class="fa fa-info"></i></button>
			</div>
			<div class="panel-body filter_panel">
				<?php
				$shop_orders_paid_type = array();
				$query = $db_link->prepare('SELECT * FROM `shop_orders_paid_type` WHERE `active` = 1 ORDER BY `order`;');
				$query->execute();
				while ($rov = $query->fetch()) {
					$shop_orders_paid_type[$rov['id']] = $rov['name'];
				}

				$time_from = '';
				$time_to = '';
				$order_id = '';
				$status = '0';
				$paid = -1;
				$customer = '';
				$customer_id = '';
				$viewed = -1;
				$paid_type = -1;
				$office = 0;
				$phone = '';
				$article = '';

				$orders_filter = null;
				if (isset($_COOKIE['orders_filter'])) {
					$orders_filter = $_COOKIE['orders_filter'];
				}
				if ($orders_filter != null) {
					$orders_filter = json_decode($orders_filter, true);
				}
				if (!is_array($orders_filter)) {
					$orders_filter = array(
						'time_from' => '', 'time_to' => '', 'order_id' => '', 'status' => 0, 'paid' => -1,
						'customer' => '', 'customer_id' => '', 'viewed' => -1, 'paid_type' => -1,
						'office' => 0, 'phone' => '', 'article' => '',
					);
				}
				$orders_filter = epc_orders_ws_normalize_filter_for_tab(
					$orders_filter,
					$epc_orders_tab,
					$epc_orders_open_ids,
					$epc_orders_completed_ids,
					$epc_orders_force_tab_defaults
				);
				$time_from = isset($orders_filter['time_from']) ? (string) $orders_filter['time_from'] : '';
				$time_to = isset($orders_filter['time_to']) ? (string) $orders_filter['time_to'] : '';
				$order_id = isset($orders_filter['order_id']) ? (string) $orders_filter['order_id'] : '';
				$status = isset($orders_filter['status']) ? $orders_filter['status'] : '0';
				$paid = isset($orders_filter['paid']) ? $orders_filter['paid'] : -1;
				$customer = isset($orders_filter['customer']) ? (string) $orders_filter['customer'] : '';
				$customer_id = isset($orders_filter['customer_id']) ? (string) $orders_filter['customer_id'] : '';
				$viewed = isset($orders_filter['viewed']) ? $orders_filter['viewed'] : -1;
				$paid_type = isset($orders_filter['paid_type']) ? $orders_filter['paid_type'] : -1;
				if (isset($orders_filter['office'])) {
					$office = $orders_filter['office'];
				}
				if (isset($orders_filter['phone'])) {
					$phone = (string) $orders_filter['phone'];
				}
				if (isset($orders_filter['article'])) {
					$article = trim((string) $orders_filter['article']);
				}

				$epc_filter_date_show = static function ($unix) {
					$unix = (int) $unix;
					return $unix > 0 ? date('d.m.Y H:i', $unix) : '';
				};
				$time_from_show = $epc_filter_date_show($time_from);
				$time_to_show = $epc_filter_date_show($time_to);

				$fields_for_customer_search = 'ID, E-mail, ' . translate_str_by_id(1312);
				$users_profile_fields_query = $db_link->prepare('SELECT `caption` FROM `reg_fields` WHERE `to_users_table` = 1;');
				$users_profile_fields_query->execute();
				while ($users_profile_field = $users_profile_fields_query->fetch()) {
					$fields_for_customer_search .= ', ' . $users_profile_field['caption'];
				}
				$phone_show = str_replace(array('+7', '+375', '+380'), '', (string) $phone);
				if ($phone_show !== '' && function_exists('urldecode')) {
					$phone_show = rawurldecode($phone_show);
				}
				?>
				<div class="epc-orders-filter-grid">
					<div class="epc-orders-filter-field<?php echo $time_from_show !== '' ? ' is-active' : ''; ?>">
						<label for="time_from_show"><?php echo translate_str_by_id(3237); ?></label>
						<input type="hidden" id="time_from" value="<?php echo htmlspecialchars((string) $time_from, ENT_QUOTES, 'UTF-8'); ?>" />
						<input type="text" id="time_from_show" class="form-control" value="<?php echo htmlspecialchars($time_from_show, ENT_QUOTES, 'UTF-8'); ?>" placeholder="dd.mm.yyyy hh:mm" autocomplete="off" />
					</div>
					<div class="epc-orders-filter-field<?php echo $time_to_show !== '' ? ' is-active' : ''; ?>">
						<label for="time_to_show"><?php echo translate_str_by_id(3238); ?></label>
						<input type="hidden" id="time_to" value="<?php echo htmlspecialchars((string) $time_to, ENT_QUOTES, 'UTF-8'); ?>" />
						<input type="text" id="time_to_show" class="form-control" value="<?php echo htmlspecialchars($time_to_show, ENT_QUOTES, 'UTF-8'); ?>" placeholder="dd.mm.yyyy hh:mm" autocomplete="off" />
					</div>
					<div class="epc-orders-filter-field<?php echo $order_id !== '' ? ' is-active' : ''; ?>">
						<label for="order_id"><?php echo translate_str_by_id(3244); ?></label>
						<input type="text" id="order_id" value="<?php echo htmlspecialchars((string) $order_id, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
					<div class="epc-orders-filter-field" id="paid_div">
						<label for="paid"><?php echo translate_str_by_id(3584); ?></label>
						<select multiple="multiple" id="paid">
							<option value="1"><?php echo translate_str_by_id(3514); ?></option>
							<option value="2"><?php echo translate_str_by_id(3515); ?></option>
							<option value="0"><?php echo translate_str_by_id(3513); ?></option>
						</select>
					</div>
					<div class="epc-orders-filter-field" id="paid_type_div">
						<label for="paid_type"><?php echo translate_str_by_id(4645); ?></label>
						<select multiple="multiple" id="paid_type">
							<option value="0"><?php echo translate_str_by_id(4207); ?></option>
							<?php foreach ($shop_orders_paid_type as $paid_type_id => $paid_type_data) { ?>
							<option value="<?php echo (int) $paid_type_id; ?>"><?php echo translate_str_by_id($paid_type_data); ?></option>
							<?php } ?>
						</select>
					</div>
					<div class="epc-orders-filter-field" id="status_div">
						<label for="status"><?php echo translate_str_by_id(2081); ?></label>
						<select multiple="multiple" id="status">
							<?php foreach ($orders_statuses as $status_id => $status_data) { ?>
							<option value="<?php echo (int) $status_id; ?>"><?php echo translate_str_by_id($status_data['name']); ?></option>
							<?php } ?>
						</select>
					</div>
					<div class="epc-orders-filter-field<?php echo $customer_id !== '' ? ' is-active' : ''; ?>">
						<label for="customer_id"><?php echo translate_str_by_id(3818); ?></label>
						<input type="text" id="customer_id" value="<?php echo htmlspecialchars((string) $customer_id, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
					<div class="epc-orders-filter-field<?php echo $customer !== '' ? ' is-active' : ''; ?>">
						<label for="customer">
							<?php echo translate_str_by_id(3245); ?>
							<button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo htmlspecialchars(translate_str_by_id(3579) . ': ' . $fields_for_customer_search . '. ' . translate_str_by_id(3580), ENT_QUOTES, 'UTF-8'); ?>');"><i class="fa fa-info"></i></button>
						</label>
						<input type="text" id="customer" value="<?php echo htmlspecialchars((string) $customer, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
					<div class="epc-orders-filter-field" id="viewed_div">
						<label for="viewed"><?php echo translate_str_by_id(3581); ?></label>
						<select multiple="multiple" id="viewed">
							<option value="1"><?php echo translate_str_by_id(3581); ?></option>
							<option value="0"><?php echo translate_str_by_id(3582); ?></option>
						</select>
					</div>
					<div class="epc-orders-filter-field" id="office_div">
						<label for="office"><?php echo translate_str_by_id(3506); ?></label>
						<select multiple="multiple" id="office">
							<?php
							foreach ($offices_list as $office_id => $office_caption) {
								echo '<option value="' . (int) $office_id . '">' . htmlspecialchars(translate_str_by_id($office_caption), ENT_QUOTES, 'UTF-8') . '</option>';
							}
							?>
						</select>
					</div>
					<div class="epc-orders-filter-field<?php echo $phone_show !== '' ? ' is-active' : ''; ?>">
						<label for="phone"><?php echo translate_str_by_id(1312); ?></label>
						<input type="text" id="phone" value="<?php echo htmlspecialchars($phone_show, ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
					<div class="epc-orders-filter-field<?php echo $article !== '' ? ' is-active' : ''; ?>">
						<label for="article"><?php echo translate_str_by_id(2071); ?></label>
						<input type="text" id="article" value="<?php echo htmlspecialchars(trim((string) $article), ENT_QUOTES, 'UTF-8'); ?>" class="form-control" />
					</div>
				</div>
			</div>
			<div class="panel-footer epc-scp-filter-bar">
				<button class="btn btn-success" type="button" onclick="filterOrders();"><i class="fa fa-filter"></i> <?php echo translate_str_by_id(2232); ?></button>
				<button class="btn btn-primary" type="button" onclick="unsetFilterOrders();"><i class="fa fa-square"></i> <?php echo translate_str_by_id(2555); ?></button>
				<button class="btn btn-info" type="button" onclick="ordersInProcess();"><i class="fa fa-toolbox"></i> <?php echo translate_str_by_id(5311); ?></button>
			</div>
		</div>
	</div>
	
	
	
	<div class="epc-scp-orders-workspace<?php echo $epc_orders_selected_id > 0 ? ' is-oms-active' : ''; ?>">
		<div class="epc-scp-orders-workspace__list">
		<div class="epc-scp-table-card">
			<div class="epc-scp-table-card__head epc-orders-workspace-head">
				<div>
					<h3><?php
						if ($epc_orders_tab === 'completed') {
							echo 'Completed orders';
						} elseif ($epc_orders_tab === 'all') {
							echo 'All orders';
						} else {
							echo 'Open orders';
						}
					?></h3>
					<p class="epc-oms-hint">Select a row — OMS console opens on the right</p>
				</div>
				<span>
					<a class="btn btn-primary btn-xs" href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/oms-guide"><i class="fa fa-book"></i> OMS daily guide</a>
					<a class="btn btn-success btn-xs" href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/whatsapp-guide"><i class="fa fa-whatsapp"></i> WhatsApp</a>
					<a class="btn btn-default btn-xs" href="/<?php echo $DP_Config->backend_dir; ?>/shop/orders/guide"><i class="fa fa-truck"></i> Fulfilment</a>
				</span>
			</div>
				<div class="table-responsive">
					<?php
					//Определяем текущую сортировку и обозначаем ее:
					$orders_sort = null;
					if( isset($_COOKIE["orders_sort"]) )
					{
						$orders_sort = $_COOKIE["orders_sort"];
					}
					$sort_field = "id";
					$sort_asc_desc = "desc";
					if($orders_sort != NULL)
					{
						$orders_sort = json_decode($orders_sort, true);
						$sort_field = $orders_sort["field"];
						$sort_asc_desc = $orders_sort["asc_desc"];
					}
					
					if( strtolower($sort_asc_desc) == "asc" )
					{
						$sort_asc_desc = "asc";
					}
					else
					{
						$sort_asc_desc = "desc";
					}
					
					if( array_search($sort_field, array('id', 'time', 'last_modified', 'price_sum', 'price_purchase', 'profit', 'paid', 'paid_type', 'status', 'obtain_caption', 'customer', 'office_id', 'checks_count', 'count_items')) === false )
					{
						$sort_field = "id";
					}
					
					
					
					
					
					//Формируем часть SQL-запроса для покупателя (отдельно, чтобы можно было использовать и для WHERE)
					//Формируем подзапрос для значений профиля пользователя (только для тех полей, которые выводятся в таблицу пользователей в менеджер пользователей колонками)
					$users_profile_SQL = "";
					$users_profile_fields_query = $db_link->prepare("SELECT `name` FROM `reg_fields` WHERE `to_users_table` = 1;");
					$users_profile_fields_query->execute();
					while( $users_profile_field = $users_profile_fields_query->fetch() )
					{
						if( $users_profile_SQL != "" )
						{
							$users_profile_SQL = $users_profile_SQL.",";
						}
						
						//Допустимы только буквы и знаки нижнего подчеркивания
						$field_name = str_replace( array(' ', '#', '-', "'", '"'), '', $users_profile_field["name"] );
						
						$users_profile_SQL = $users_profile_SQL." IF( IFNULL((SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = `shop_orders`.`user_id`), '') != '' , CONCAT(', ', (SELECT `data_value` FROM `users_profiles` WHERE `data_key` = '".$field_name."' AND `user_id` = `shop_orders`.`user_id`)),'') ";
					}
					if( $users_profile_SQL != "" )
					{
						$users_profile_SQL = ",".$users_profile_SQL;
					}
					//SQL-подзапрос компонует строку с данными пользователя
					$SQL_SELECT_CUSTOMER = " IF( `user_id` = 0, CONCAT('".translate_str_by_id(3233)." (ID 0), ', '".translate_str_by_id(1312).": ' , IF((`phone_not_auth`='' OR `phone_not_auth` IS NULL), '', CONCAT(', ".translate_str_by_id(1312).": ', `phone_not_auth` )), IF( `email_not_auth`=''  OR `email_not_auth` IS NULL, '', CONCAT(', E-mail: ', `email_not_auth` )))  , CONCAT( 'ID ', `user_id`, ', E-mail: ', (SELECT IF(`email`!='', `email`, '".translate_str_by_id(3253)."') FROM `users` WHERE `user_id` = `shop_orders`.`user_id` LiMIT 1 ), ', ".translate_str_by_id(1312).": ', (SELECT IF(`phone`!='', `phone`, '".translate_str_by_id(3253)."') FROM `users` WHERE `user_id` = `shop_orders`.`user_id` LiMIT 1 ) ".$users_profile_SQL." ) )";
					
					
					
					
					
					
					
					//Подстрока с условиями фильтрования заказов
					$WHERE_CONDITIONS = " WHERE ";
					$binding_values = array();
					//По офисам обслуживания - только те, с котроми работает данный менеджер
					$sub_WHERE_offices = "";
					foreach($offices_list as $office_id => $office_caption)
					{
						if(isset($orders_filter["office"]) && $orders_filter["office"] != 0 && !in_array($office_id, $orders_filter["office"]))
						{
							continue;//Если выбран в фильтре офис
						}
						if($sub_WHERE_offices != "")$sub_WHERE_offices .= " OR ";
						$sub_WHERE_offices .= "`office_id`=?";
						
						
						array_push( $binding_values, $office_id );
					}
					if($sub_WHERE_offices == "")
					{
						$sub_WHERE_offices = "0=1";
					}
					$WHERE_CONDITIONS .= "(".$sub_WHERE_offices.")";
					
					// Filter from cookie + OMS tab normalization (Open / Completed / All)
					if (!isset($orders_filter) || !is_array($orders_filter)) {
						$orders_filter = array();
						if (isset($_COOKIE['orders_filter'])) {
							$decoded = json_decode((string) $_COOKIE['orders_filter'], true);
							if (is_array($decoded)) {
								$orders_filter = $decoded;
							}
						}
						$orders_filter = epc_orders_ws_normalize_filter_for_tab(
							$orders_filter,
							isset($epc_orders_tab) ? $epc_orders_tab : 'open',
							isset($epc_orders_open_ids) ? $epc_orders_open_ids : epc_orders_ws_open_status_ids($db_link),
							isset($epc_orders_completed_ids) ? $epc_orders_completed_ids : epc_orders_ws_completed_status_ids($db_link),
							!empty($epc_orders_force_tab_defaults)
						);
					}
					if (is_array($orders_filter))
					{
						//1. Date from (inclusive)
						if (!empty($orders_filter['time_from'])) {
							$WHERE_CONDITIONS .= ' AND `time` >= ?';
							array_push($binding_values, (int) $orders_filter['time_from']);
						}

						//2. Date to (inclusive — end of selected minute/day)
						if (!empty($orders_filter['time_to'])) {
							$time_to_bound = (int) $orders_filter['time_to'];
							// If user picked midnight (date only), include the whole day.
							if ($time_to_bound > 0 && ((int) date('H', $time_to_bound) === 0) && ((int) date('i', $time_to_bound) === 0) && ((int) date('s', $time_to_bound) === 0)) {
								$time_to_bound += 86399;
							}
							$WHERE_CONDITIONS .= ' AND `time` <= ?';
							array_push($binding_values, $time_to_bound);
						}
						
						//3. Номер заказа
						if($orders_filter["order_id"] != "")
						{
							$WHERE_CONDITIONS .= " AND `id` = ?";
							
							array_push( $binding_values, $orders_filter["order_id"] );
						}
						
						
						//4. Статус заказа
						if ($orders_filter["status"] != 0)
						{
							$WHERE_CONDITIONS .= " AND `status` IN (".str_repeat("?,", count($orders_filter["status"])-1)."?)";
							$binding_values = array_merge($binding_values, $orders_filter["status"]);
						}
						
						
						//5. Оплата
						if ($orders_filter["paid"] != -1)
						{
							$WHERE_CONDITIONS .= " AND `paid` IN (".str_repeat("?,", count($orders_filter["paid"]) - 1)."?)";
							$binding_values = array_merge($binding_values, $orders_filter["paid"]);

						}
						
						
						//6. Покупатель
						if($orders_filter["customer"] != "" )
						{
							$WHERE_CONDITIONS .= " AND $SQL_SELECT_CUSTOMER LIKE ?";
							
							array_push( $binding_values, "%".htmlentities($orders_filter["customer"])."%");
						}
						
						if($orders_filter["customer_id"] != "" )
						{
							$WHERE_CONDITIONS .= " AND `user_id` = ?";
							
							array_push( $binding_values, $orders_filter["customer_id"]);
						}
						
						
						//7. Просмотрен
						if($orders_filter["viewed"] != -1 && count($orders_filter["viewed"]) == 1)
						{
							$orders_filter["viewed"] = $orders_filter["viewed"][0];
							$WHERE_CONDITIONS .= " AND IFNULL( (SELECT `viewed_flag` FROM `shop_orders_viewed` WHERE `order_id` = `shop_orders`.`id` AND `user_id` = ? LIMIT 1), 1 ) = ?";
							
							array_push( $binding_values, $manager_id);
							array_push( $binding_values, $orders_filter["viewed"][0]);
						}
						
						//8. Способ оплаты
						if ($orders_filter["paid_type"] != -1)
						{
							$WHERE_CONDITIONS .= " AND `paid_type` IN (".str_repeat("?,", count($orders_filter["paid_type"])-1)."?)";
							$binding_values = array_merge($binding_values, $orders_filter["paid_type"]);

						}
						
						//10. Телефон клиента
						if($orders_filter["phone"] != "")
						{
							$WHERE_CONDITIONS .= " AND $SQL_SELECT_CUSTOMER LIKE ?";
							
							array_push($binding_values, "%".htmlentities($orders_filter["phone"])."%");
						}
						
						//11. Артикул
						if(trim($orders_filter["article"]) != "")
						{
							$WHERE_CONDITIONS .= " AND `id` IN(SELECT DISTINCT `order_id` FROM `shop_orders_items` WHERE `t2_article` = ?)";
							
							array_push($binding_values, trim($orders_filter["article"]));
						}
					}
					
					
					//Подстрока с условиями фильтрования статусов позиций, которые не участвуют в ценовых расчетах
					$WHERE_statuses_not_count = "";
					$WHERE_statuses_not_count_without_and = "";
					for($i=0; $i<count($orders_items_statuses_not_count); $i++)
					{
						$WHERE_statuses_not_count .= " AND `status` != ".(int)$orders_items_statuses_not_count[$i];
						
						if($i > 0)$WHERE_statuses_not_count_without_and .= " AND ";
						$WHERE_statuses_not_count_without_and .= " `status` != ".(int)$orders_items_statuses_not_count[$i];
					}

					
					//ОБЕСПЕЧИВАЕМ ПОСТРАНИЧНЫЙ ВЫВОД:
					//---------------------------------------------------------------------------------------------->

					//Определяем, с какой страницы начать вывод:
					$s_page = 0;
					if( isset($_COOKIE['orders_need_page']) )
					{
						$s_page = (int) $_COOKIE['orders_need_page'];
					}


					//Определяем сколько пропустить записей для выборки
					$p = $DP_Config->list_page_limit;//Штук на страницу
					$start_elements_of_page = abs($s_page * $p);

					// print_r($SQL_SELECT_ITEMS_LIMIT);

					$elements_counter = 0;
					//----------------------------------------------------------------------------------------------|
				
					
					
					$SQL_SELECT_ORDERS = "SELECT SQL_CALC_FOUND_ROWS `shop_orders`.`id` AS `id`, ";
					$SQL_SELECT_ORDERS .= "`shop_orders`.`time` AS `time`, ";
					$SQL_SELECT_ORDERS .= "`shop_orders`.`paid` AS `paid`, ";
					$SQL_SELECT_ORDERS .= "`shop_orders`.`paid_type` AS `paid_type`, ";
					$SQL_SELECT_ORDERS .= "`shop_orders`.`status` AS `status`, ";
					$SQL_SELECT_ORDERS .= " (SELECT `caption` FROM `shop_obtaining_modes` WHERE `id` = `shop_orders`.`how_get`) AS `obtain_caption`, ";
					$SQL_SELECT_ORDERS .= "`shop_orders`.`status` AS `status`, ";
					$SQL_SELECT_ORDERS .= "`shop_orders`.`user_id` AS `user_id`, ";//Статистика
					
					$SQL_SELECT_ORDERS .= " $SQL_SELECT_CUSTOMER AS `customer`,";
					
					
					
					
					$SQL_SELECT_ORDERS .= "`shop_orders`.`office_id` AS `office_id`, ";

					//Количество позиций в заказе
					$SQL_SELECT_ORDERS .= "(SELECT COUNT(*) FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` ) AS `count_items`,";
					
					//Сумма заказа
					$sql_select_order_sum = " CAST( (SELECT SUM(`price`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` $WHERE_statuses_not_count ) AS DECIMAL(20,2)) ";
					$SQL_SELECT_ORDERS .= $sql_select_order_sum." AS `price_sum`,";
					
					$sql_select_order_purchase = "((CAST( IFNULL( (SELECT SUM(`price_purchase`*(`count_reserved`+`count_issued`)) FROM `shop_orders_items_details` WHERE `order_id`= `shop_orders`.`id` AND `order_item_id` IN (SELECT `id` FROM `shop_orders_items` WHERE $WHERE_statuses_not_count_without_and) ), 0 ) AS DECIMAL(20,2) ) ) + (CAST( IFNULL( (SELECT SUM(`t2_price_purchase`*`count_need`) FROM `shop_orders_items` WHERE `order_id`= `shop_orders`.`id` AND $WHERE_statuses_not_count_without_and),  0) AS DECIMAL(20,2) ) ))";
					
					
					
					
					$SQL_SELECT_ORDERS .= $sql_select_order_purchase." AS `price_purchase`,";//Сумма закупа
					
					//Прибыль
					$SQL_SELECT_ORDERS .= " $sql_select_order_sum - $sql_select_order_purchase  AS `profit`, ";
					
					
					//Флаг "Просмотрен"
					$SQL_SELECT_ORDERS .= " IFNULL( (SELECT `viewed_flag` FROM `shop_orders_viewed` WHERE `order_id` = `shop_orders`.`id` AND `user_id` = ? LIMIT 1), 1 ) AS `viewed_flag`, ";
					
					array_unshift( $binding_values, (int)$manager_id );
					
					
					//Количество непрочитанных сообщений
					$SQL_SELECT_ORDERS .= " (SELECT COUNT(*) FROM `shop_orders_messages` WHERE `order_id` = `shop_orders`.`id` AND `read` = 0 AND `is_customer` = 1) AS `count_not_viewed_msg`, ";
					
					if(isset($_GET["read"]) && (int)$_GET["read"] === 0)
					{
						$WHERE_CONDITIONS .= " AND `id` IN(SELECT DISTINCT `order_id` FROM `shop_orders_messages` WHERE `read` = 0 AND `is_customer` = 1) ";
					}
					
					
					//Количество чеков, привязанных к позициям заказа
					//$SQL_SELECT_ORDERS .= "(SELECT `id` FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `order_item_id` IN (SELECT `id` FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` ) ) AS `checks_count`";
					$SQL_SELECT_ORDERS .= "(SELECT COUNT(DISTINCT(`check_id`)) FROM `shop_kkt_checks_products` WHERE `id` IN (SELECT `check_product_id` FROM `shop_kkt_checks_products_to_orders_items_map` WHERE `order_item_id` IN (SELECT `id` FROM `shop_orders_items` WHERE `order_id` = `shop_orders`.`id` ) ) ) AS `checks_count`, ";

					// Last modified = latest log/message activity, else created time (for OMS daily queue)
					$SQL_SELECT_ORDERS .= "GREATEST(
						IFNULL((SELECT MAX(`time`) FROM `shop_orders_logs` WHERE `order_id` = `shop_orders`.`id`), 0),
						IFNULL((SELECT MAX(`time`) FROM `shop_orders_messages` WHERE `order_id` = `shop_orders`.`id`), 0),
						`shop_orders`.`time`
					) AS `last_modified`";
					
					$order_by_sql = ($sort_field === 'last_modified')
						? ("last_modified " . $sort_asc_desc)
						: ("`" . $sort_field . "` " . $sort_asc_desc);
					$SQL_SELECT_ORDERS .= " FROM `shop_orders` $WHERE_CONDITIONS ORDER BY $order_by_sql LIMIT $start_elements_of_page,$p";
					
					//echo $SQL_SELECT_ORDERS;
					
	
					$elements_query = $db_link->prepare($SQL_SELECT_ORDERS);
					$elements_query->execute($binding_values);
					//var_dump($elements_query->fetch());
					//var_dump($SQL_SELECT_ORDERS);
					//var_dump($binding_values);
					
					$elements_count_rows_query = $db_link->prepare('SELECT FOUND_ROWS();');
					$elements_count_rows_query->execute();
					$elements_count_rows = $elements_count_rows_query->fetchColumn();
					
					
					//Определяем количество страниц для вывода:
					$count_pages = (int)($elements_count_rows / $p);//Количество страниц
					if($elements_count_rows%$p)//Если остались еще элементы
					{
						$count_pages++;
					}
					
					// -------------------------------------------------------------------
					//Получаем суммарные показатели
					$SQL_SELECT_TOTAL_INDICATORS = "SELECT 
						COUNT(*) AS `orders_count`,
						SUM($sql_select_order_sum) AS `price_sum_total`,
						SUM($sql_select_order_sum - $sql_select_order_purchase) AS `profit_sum_total`,
						SUM($sql_select_order_purchase) AS `price_purchase_sum_total`
						FROM `shop_orders` $WHERE_CONDITIONS ";
					
					//Удаляем первый элемент массива связанных значений (это был manager_id для флага "Просмотрен")
					array_shift($binding_values);
					
					$total_indicators_query = $db_link->prepare($SQL_SELECT_TOTAL_INDICATORS);
					$total_indicators_query->execute($binding_values);
					$total_indicators = $total_indicators_query->fetch();
					// -------------------------------------------------------------------
					
					//Массив заказов
					$orders = array();
					$orders_id_list = array();
					while($record = $elements_query->fetch())
					{
						$orders[] = $record;
						$orders_id_list[] = $record["id"];
					}
					
					//Формируем список позиций заказов
					$orders_items = array();
					if (count($orders_id_list) > 0) {
						$orders_items_query = $db_link->prepare("SELECT * FROM `shop_orders_items` WHERE `order_id` IN(".implode(',', $orders_id_list).");");
						$orders_items_query->execute();
						while($record = $orders_items_query->fetch())
						{
							$orders_items[$record['order_id']][] = $record;
						}
					}
					
					//Массивы для JS с id элементов и с чекбоксами элементов
					$orders_boot = array(
						'elements_array' => array(),
						'elements_id_array' => array(),
						'tab' => $epc_orders_tab,
						'firstOrderId' => 0,
					);
									
					//Далее идет вывод таблицы с плагином footable. При этом постраничный вывод обеспечивает PHP. Поэтому, в таблицу ставятся параметры data-sort="false", data-page-size = всему количеству записей, <tfoot style="display:none;"> - заглушка, чтобы JS не затрагивал свой переключатель станиц
					?>
					<table id="orders_table" class="footable table table-hover toggle-arrow epc-scp-data-table" data-sort="false" data-page-size="<?php echo ($elements_count_rows * 2); ?>">
						<thead>
							<tr>
								<th><input type="checkbox" id="check_uncheck_all" name="check_uncheck_all" onchange="on_check_uncheck_all();" /></th>
								<th data-toggle="true"></th>
								<th><a href="javascript:void(0);" onclick="sortOrders('id');" id="id_sorter">Order #</a></th>
								<th><a href="javascript:void(0);" onclick="sortOrders('time');" id="time_sorter"><?php echo translate_str_by_id(3250); ?></a></th>
								<th data-hide="phone"><a href="javascript:void(0);" onclick="sortOrders('last_modified');" id="last_modified_sorter">Modified</a></th>
								<th><a href="javascript:void(0);" onclick="sortOrders('count_items');" id="count_items_sorter"><?php echo translate_str_by_id(4569); ?></a></th>
								<th><a href="javascript:void(0);" onclick="sortOrders('price_sum');" id="price_sum_sorter"><?php echo translate_str_by_id(3251); ?></a></th>
								<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrders('price_purchase');" id="price_purchase_sorter"><?php echo translate_str_by_id(5306); ?></a></th>
								<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrders('profit');" id="profit_sorter"><?php echo translate_str_by_id(3499); ?></a></th>
								<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrders('paid');" id="paid_sorter"><?php echo translate_str_by_id(3584); ?></a></th>
								<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrders('paid_type');" id="paid_type_sorter"><?php echo translate_str_by_id(4645); ?></a></th>
								<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrders('status');" id="status_sorter"><?php echo translate_str_by_id(2081); ?></a></th>
								<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrders('obtain_caption');" id="obtain_caption_sorter"><?php echo translate_str_by_id(3507); ?></a></th>
								<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrders('customer');" id="customer_sorter"><?php echo translate_str_by_id(4550); ?></a></th>
								<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrders('checks_count');" id="checks_count_sorter"><?php echo translate_str_by_id(3551); ?></a></th>
								<th data-hide="phone,tablet"><a href="javascript:void(0);" onclick="sortOrders('office_id');" id="office_id_sorter"><?php echo translate_str_by_id(3506); ?></a></th>
							</tr>
						</thead>
						<tbody>
						<?php
						foreach($orders as $element_record)
						{
							$customer_id =  $element_record['user_id'];//Статистика
							
							if($elements_counter >= $s_page*$p+$p)
							{
								break;
							}
							$elements_counter++;
							
							
							//Для Javascript
							$orders_boot['elements_array'][] = 'checked_' . $element_record['id'];
							$orders_boot['elements_id_array'][] = (int) $element_record['id'];
							if (empty($orders_boot['firstOrderId'])) {
								$orders_boot['firstOrderId'] = (int) $element_record['id'];
							}
							
							
							$order_id = $element_record["id"];
							$time = $element_record["time"];
							$count_items = $element_record["count_items"];
							$price_sum = $element_record["price_sum"];
							$price_purchase = $element_record["price_purchase"];
							$profit = $element_record["profit"];
							$paid = $element_record["paid"];
							$paid_type = $element_record["paid_type"];
							$status = $element_record["status"];
							$obtain_caption = $element_record["obtain_caption"];
							$customer = $element_record["customer"];
							$office_id = $element_record["office_id"];
							
							
							//Флаг "Заказ просмотрен"
							$viewed_class = "";
							$viewed_flag = $element_record["viewed_flag"];
							if( $viewed_flag == 0)
							{
								$viewed_class = " not_viewed";
							}
							
							
							
							//Чеки
							$order_checks_count = $element_record["checks_count"];
							if( $order_checks_count == 0 )
							{
								$order_checks_count = translate_str_by_id(2457);
							}
							else
							{
								$order_checks_count = "<span style=\"cursor:pointer;\" onclick=\"show_order_checks(".$order_id.");\">".$order_checks_count." <i class=\"fas fa-search\"></i></span>";
							}
							
							
							$a_item = "<a class=\"epc-scp-orders-link\" href=\"javascript:void(0);\" onclick=\"epcSelectOrder(".$order_id.", event);\">";
							$row_selected = ($epc_orders_selected_id === (int) $order_id) ? ' is-selected' : '';
							
							?>
							<tr class="epc-scp-orders-row<?php echo $viewed_class . $row_selected; ?>" data-order-id="<?php echo (int) $order_id; ?>" onclick="epcSelectOrder(<?php echo (int) $order_id; ?>, event);">
								<td><input type="checkbox" onchange="on_one_check_changed('checked_<?php echo $element_record["id"]; ?>');" id="checked_<?php echo $element_record["id"]; ?>" name="checked_<?php echo $element_record["id"]; ?>" /></td>
								<td>
								<?php
								// Индикатор непрочитанных сообщений в заказах
								if($element_record["count_not_viewed_msg"] > 0){
								?>
								<a style="white-space: nowrap;" class="dropdown-toggle label-menu-corner" href="<?=$DP_Config->domain_path.$DP_Config->backend_dir;?>/shop/orders/order?order_id=<?=$order_id;?>">
									<i style="font-size: 25px;" class="pe-7s-mail"></i>
									<span style="font-size: 9px;"><?=$element_record["count_not_viewed_msg"];?></span>
								</a>
								<?php
								}
								?>
								</td>
								<td><?php echo $a_item.$order_id; ?></a></td>
								<td><?php echo $a_item.date("d.m.Y", $time)."<br>".date("G:i", $time); ?></a></td>
								<td><?php
									$lm = (int) ($element_record['last_modified'] ?? $time);
									echo $a_item . date('d.m.Y', $lm) . '<br>' . date('G:i', $lm);
								?></a></td>
								<td><?php echo $a_item.$count_items; ?></a></td>
								<td><?php echo $a_item.number_format($price_sum, 2, '.', ''); ?></a></td>
								<td><?php echo $a_item.number_format($price_purchase, 2, '.', ''); ?></a></td>
								<td><?php echo $a_item.number_format($profit, 2, '.', ''); ?></a></td>
								<td>
									<?php
									if($paid == 1)
									{
										echo epc_orders_ws_paid_badge(1);
									}
									else if($paid == 0)
									{
										echo epc_orders_ws_paid_badge(0);
									}
									else
									{
										echo epc_orders_ws_paid_badge(2);
									}
									?>
								</td>
								<td><?=(!empty($shop_orders_paid_type[$paid_type]))?epc_orders_ws_h(translate_str_by_id($shop_orders_paid_type[$paid_type])):'';?></td>
								<td><?php echo epc_orders_ws_status_badge((int) $status, $orders_statuses, $db_link); ?></td>
								<td><?php echo $a_item.translate_str_by_id($obtain_caption); ?></a></td>
								<td><?php include $_SERVER['DOCUMENT_ROOT'].'/'.$DP_Config->backend_dir.'/content/users/statistics/modal.php';//Статистика?><?php echo $a_item.$customer; ?></a></td>
								<td><?php echo $order_checks_count; ?></td>
								<td><?php echo $a_item.translate_str_by_id($offices_list[$office_id]); ?></a></td>
							</tr>
							<?php
						}
						?>
						</tbody>
						<tfoot style="display:none;"><tr><td><ul class="pagination"></ul></td></tr></tfoot>
					</table>
					<div id="epc-orders-boot" style="display:none"><?php echo htmlspecialchars(json_encode($orders_boot, JSON_UNESCAPED_UNICODE), ENT_QUOTES, 'UTF-8'); ?></div>
				</div>
				
				<?php
				//START ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
				if( $count_pages > 1 )
				{
					// формируем пагинацию
					$pagination = pagination($elements_count_rows, $p, 3, $s_page, 'paginate_button active', '');
					if($pagination != '<a class="paginate_button active">1</a>'){
						$pagination = '<div class="pagination">'.$pagination.'</div>';
					}else{
						$pagination = '';
					}
					?>
					<div class="row">
						<div class="col-lg-12 text-center">
							<div class="dataTables_paginate paging_simple_numbers">
								<?php echo $pagination; ?>
							</div>
						</div>
					</div>
				<?php
				}
				//END ВЫВОД ПЕРЕКЛЮЧАТЕЛЕЙ СТРАНИЦ ТАБЛИЦЫ
				?>

		</div>
		</div>

		<div class="epc-scp-orders-workspace__detail">
			<div class="epc-scp-orders-detail" id="epc_orders_detail_pane">
				<?php
				if ($epc_orders_selected_id > 0) {
					$order_id = $epc_orders_selected_id;
					$epc_orders_detail_pane = $epc_orders_process_dir . '/epc_orders_detail_pane.php';
					if (is_file($epc_orders_detail_pane)) {
						include $epc_orders_detail_pane;
					}
				} else {
					?>
				<div class="epc-scp-orders-detail__empty">
					<i class="fa fa-columns"></i>
					<p><strong>OMS console</strong><br>Click an open order on the left to manage items, payment, documents, and status here.<br><span class="text-muted small">Ctrl+click opens the classic full order card</span></p>
				</div>
					<?php
				}
				?>
			</div>
		</div>
	</div>
	
	
	<div class="col-lg-12 epc-scp-orders-bulk">
		<div class="epc-scp-form-card">
			<h4 style="margin-top:0;"><?php echo translate_str_by_id(3587); ?> <small class="text-muted">— select rows with checkboxes</small></h4>
			<div class="epc-orders-bulk-grid">
				<div class="epc-orders-bulk-card">
					<h5><i class="fa fa-flag"></i> <?php echo translate_str_by_id(3558); ?></h5>
					<select id="setOrderStatusSelect" class="form-control">
						<?php foreach ($orders_statuses as $status_id => $status_data) { ?>
						<option value="<?php echo (int) $status_id; ?>"><?php echo htmlspecialchars(translate_str_by_id($status_data['name']), ENT_QUOTES, 'UTF-8'); ?></option>
						<?php } ?>
					</select>
					<button class="btn btn-success btn-sm" type="button" onclick="setOrdersStatus();"><i class="fa fa-check"></i> <?php echo translate_str_by_id(3588); ?></button>
				</div>
				<div class="epc-orders-bulk-card">
					<h5><i class="fa fa-eye"></i> <?php echo translate_str_by_id(3589); ?></h5>
					<select id="setOrderViewed" class="form-control">
						<option value="1"><?php echo translate_str_by_id(3581); ?></option>
						<option value="0"><?php echo translate_str_by_id(3582); ?></option>
					</select>
					<button class="btn btn-success btn-sm" type="button" onclick="setOrderViewed();"><i class="fa fa-check"></i> <?php echo translate_str_by_id(3588); ?></button>
				</div>
				<div class="epc-orders-bulk-card">
					<h5><i class="fa fa-trash"></i> <?php echo translate_str_by_id(3590); ?></h5>
					<p class="text-muted" style="font-size:12px;margin:0 0 10px;">Deletes selected orders permanently.</p>
					<button class="btn btn-danger btn-sm" type="button" onclick="deleteSelectedeOrders();"><i class="fa fa-trash-o"></i> <?php echo translate_str_by_id(3590); ?></button>
				</div>
			</div>
		</div>
	</div>
	
	
	
	
	<div class="col-lg-12">
		<div class="epc-scp-table-card">
			<h4><?php echo translate_str_by_id(3591); ?> <button class="btn btn-xs btn-info btn-circle" type="button" onclick="show_hint('<?php echo translate_str_by_id(3592); ?>.');"><i class="fa fa-info"></i></button></h4>
				<div class="row">
					<div class="col-lg-12 text-center">
						<div class="table-responsive">
							<table cellpadding="1" cellspacing="1" class="table">
								<thead>
									<tr>
										<th style="text-align:center;"><?php echo translate_str_by_id(3593); ?></th>
										<th style="text-align:center;"><?php echo translate_str_by_id(3594); ?></th>
										<th style="text-align:center;"><?php echo translate_str_by_id(3595); ?></th>
										<th style="text-align:center;"><?php echo translate_str_by_id(3596); ?></th>
									</tr>
								</thead>
								<tbody>
									<tr>
										<td style="text-align:center;"><?php echo $total_indicators["orders_count"]; ?></td>
										<td style="text-align:center;"><?php echo $total_indicators["price_sum_total"]; ?></td>
										<td style="text-align:center;"><?php echo $total_indicators["profit_sum_total"]; ?></td>
										<td style="text-align:center;"><?php echo $total_indicators["price_purchase_sum_total"]; ?></td>
									</tr>
								</tbody>
							</table>
						</div>
					</div>
				</div>
			</div>
		</div>
	</div>
	</div>
    
    
	

    <?php
    
}//~else//Действий нет - выводим страницу
?>
