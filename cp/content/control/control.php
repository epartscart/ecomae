<?php
/**
 * Главная страница панели управления
*/
defined('_ASTEXE_') or die('No access');


//Определение функции проверки доступа к странице
require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/control/control_helper.php");

//Для работы с пользователями - для определения доступа к страницам
require_once($_SERVER["DOCUMENT_ROOT"]."/content/users/dp_user.php");

$epcSuperCpDash = $_SERVER["DOCUMENT_ROOT"] . "/" . $DP_Config->backend_dir . "/content/control/epc_super_cp_dashboard.php";
if (is_file($epcSuperCpDash)) {
	require_once $epcSuperCpDash;
}

$epcTenantCpDash = $_SERVER["DOCUMENT_ROOT"] . "/" . $DP_Config->backend_dir . "/content/control/epc_tenant_cp_dashboard.php";
if (is_file($epcTenantCpDash)) {
	require_once $epcTenantCpDash;
}

// Tenant home dashboard is the full page — do not continue into legacy
// statistics/control_items (those paths have fatals on PHP 8 and add latency).
if (!empty($GLOBALS['epc_tenant_cp_dashboard_shown'])) {
	return;
}

//Блок статистики
if( ! isset($_COOKIE["statistical_mp_hidden"]) || ((int)$_COOKIE["statistical_mp_hidden"] === 0)){
	//Проверяем доступ к статистике
	$adminProfile = DP_User::getAdminProfile();//Профиль администратора
	// getAdminProfile() returns false when there is no valid admin session;
	// guard against that instead of indexing into a bool (fatal on PHP 8).
	$adminGroupId = (is_array($adminProfile) && isset($adminProfile["groups"][0])) ? $adminProfile["groups"][0] : null;
	if ($adminGroupId !== null) {
		$SQL = "SELECT * FROM `content_access` WHERE `content_id` IN(SELECT `id` FROM `content` WHERE `alias` = 'statistika' AND `is_frontend` = 0) AND `group_id` = ?;";
		$query = $db_link->prepare($SQL);
		$query->execute(array($adminGroupId));
		$row = $query->fetch();
		if(!empty($row)){
			require_once($_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir."/content/shop/statistics/statistics_main_page.php");
		}
	}
}

//Массив для блоков и страниц по блокам
$tabs = array();

//Получаем перечнь групп задач панели управления:
$control_groups_query = $db_link->prepare("SELECT * FROM `control_groups` ORDER BY `order` ASC;");
$control_groups_query->execute();
while( $group = $control_groups_query->fetch() )
{
    $tabs[(string)$group["id"]] = array("caption"=>translate_str_by_id($group["caption"]), "items"=>array());
}


//Получаем перечень всех задач:
$control_panel_content_query = $db_link->prepare("SELECT * FROM `control_items` ORDER BY `order` ASC;");
$control_panel_content_query->execute();
while( $item = $control_panel_content_query->fetch() )
{
	$item["url"] = str_replace( array("<backend>"), $DP_Config->backend_dir, $item["url"]);
	
	$item['caption'] = translate_str_by_id($item['caption']);
	
	//Добавляем, только, если у пользователя есть доступ
	if( is_anable($item) || $item["show_anyway"] == 1 )
	{
		$group_key = (string)$item["items_group"];
		// Guard against orphaned control_items rows whose items_group no
		// longer matches any control_groups.id — without this, PHP 8
		// throws a fatal TypeError from array_push(null, ...) and the
		// whole /cp/control page 500s.
		if( !isset($tabs[$group_key]) || !is_array($tabs[$group_key]["items"]) )
		{
			continue;
		}
		array_push($tabs[$group_key]["items"], $item);
	}
}

//Выводим перечень задач на страницу:
foreach($tabs as $key => $tab)
{
	//В данном блоке нет доступных страниц
	if(count($tab["items"]) == 0)
	{
		continue;
	}
	
	if($DP_Template->name == "bootstrap_admin")
	{
		?>
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo $tab["caption"];?>
				</div>
				<div class="panel-body">
					<?php
					for($i=0; $i<count($tab["items"]); $i++)
					{
						//Функция подключена в скрипте шаблона панели управления
						print_backend_button($tab["items"][$i]);
					}//for()
					?>
				</div>
			</div>
		</div>
		<?php
	}
	else if($DP_Template->name == "startbootstrap")
	{
		?>
		<div class="panel panel-default">
			<div class="panel-heading">
				<?php echo $tab["caption"];?>
			</div>
			<div class="panel-body">
				<?php
				for($i=0; $i<count($tab["items"]); $i++)
				{
					//Изображение
					$img = "/".$DP_Config->backend_dir."/templates/".$DP_Template->name."/images/".$tab["items"][$i]["img"];
					if(!file_exists($_SERVER["DOCUMENT_ROOT"]."/".$img))
					{
						$img = "content/control/images/window.png";
					}
					?>
					<a class="panel_a" href="<?php echo $tab["items"][$i]["url"]; ?>">
						<div class="panel_a_img" style="background: url('<?php echo $img; ?>') 0 0 no-repeat;"></div>
						<div class="panel_a_caption"><?php echo $tab["items"][$i]["caption"]; ?></div>
					</a>
					<?php
				}//for()
				?>
			</div>
		</div>
		<?php
	}
}//foreach()
?>