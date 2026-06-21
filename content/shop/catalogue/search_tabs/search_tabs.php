<?php
//Скрипт вывода компонента tab на главной странице
defined('_ASTEXE_') or die('No access');

$search_tabs = array();
$search_tabs_query = $db_link->prepare('SELECT SQL_CALC_FOUND_ROWS * FROM `shop_docpart_search_tabs` WHERE `enabled` = :enabled ORDER BY `order`;');
$search_tabs_query->bindValue(':enabled', 1);
$search_tabs_query->execute();
while( $search_tab = $search_tabs_query->fetch() )
{
	$search_tabs[] = $search_tab;
}

if(count($search_tabs) > 0){
?>
<div class="row">
	<div class="col-md-12 main_page">
		<!-- Nav tabs -->
		<ul class="nav nav-tabs">
			<?php
			$first = true;
			foreach( $search_tabs as $search_tab)
			{
				$active = "";
				if($first)
				{
					$active = "active";
					$first = false;
				}
				?>
				<li class="<?php echo $active; ?>"><a href="#tab_<?php echo $search_tab["id"]; ?>" data-toggle="tab"><?php echo translate_str_by_id($search_tab["caption"]); ?></a></li>
				<?php
			}
			?>
		</ul>
		 
		<!-- Tab panes -->
		<div class="tab-content navbar-inverse">
			<?php
			$first = true;
			foreach( $search_tabs as $search_tab)
			{
				$active = "";
				if($first)
				{
					$active = "active";
					$first = false;
				}
				?>
				<div class="tab-pane <?php echo $active; ?>" id="tab_<?php echo $search_tab["id"]; ?>">
					<?php
					require_once($_SERVER["DOCUMENT_ROOT"]."/content/shop/catalogue/search_tabs/tabs_content/".$search_tab["name"]."/tab_content.php");
					?>
				</div>
				<?php
			}
			?>
		</div>
	</div>
</div>
<?php
}
?>