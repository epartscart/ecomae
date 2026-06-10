<?php
/**
 * Страничный скрипт для управления одним пакетом
*/
defined('_ASTEXE_') or die('No access');

if($_POST["pack_action"])
{
    
}
else//Действий нет - выводим страницу
{
	//Для работы с пользователем
	require_once( $_SERVER['DOCUMENT_ROOT']."/content/users/dp_user.php" );
	$user_session = DP_User::getAdminSession();
	
    ?>
    
    <?php
        require_once("content/control/actions_alert.php");//Вывод сообщений о результатах действий
    ?>
    
    
    
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2113); ?>
			</div>
			<div class="panel-body">
				<a class="panel_a" href="javascript:void(0);" onclick="deletePack();">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/pack_delete.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2224); ?></div>
				</a>
				
				
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir;?>/packs/packs_manager">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/packs.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2699); ?></div>
				</a>
			
			
				<a class="panel_a" href="/<?php echo $DP_Config->backend_dir?>">
					<div class="panel_a_img" style="background: url('/<?php echo $DP_Config->backend_dir; ?>/templates/<?php echo $DP_Template->name; ?>/images/power_off.png') 0 0 no-repeat;"></div>
					<div class="panel_a_caption"><?php echo translate_str_by_id(2116); ?></div>
				</a>
			</div>
		</div>
	</div>
	
	
    

    

    
    
    <div id="message_div"></div>
    
    
	
	
	<div class="col-lg-12">
		<div class="hpanel">
			<div class="panel-heading hbuilt">
				<?php echo translate_str_by_id(2700); ?>
			</div>
			<div class="panel-body">
				<?php
					//Получаем данные по пакету
					$pack_query = $db_link->prepare("SELECT * FROM `packs` WHERE `id` = ?;");
					$pack_query->execute( array($_GET["pack_id"]) );
					$pack_record = $pack_query->fetch();
				?>
				
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2701); ?>
					</label>
					<div class="col-lg-6">
						<?php echo $pack_record["id"]; ?>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2277); ?>
					</label>
					<div class="col-lg-6">
						<?php echo $pack_record["caption"]; ?>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2702); ?>
					</label>
					<div class="col-lg-6">
						<?php echo $pack_record["name"]; ?>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2703); ?>
					</label>
					<div class="col-lg-6">
						<?php echo $pack_record["author"]; ?>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2704); ?>
					</label>
					<div class="col-lg-6">
						<?php echo $pack_record["version"]; ?>
					</div>
				</div>
				<div class="hr-line-dashed col-lg-12"></div>
				<div class="form-group">
					<label for="" class="col-lg-6 control-label">
						<?php echo translate_str_by_id(2705); ?>
					</label>
					<div class="col-lg-6">
						<?php echo date("d.m.Y H:i:s", $pack_record["time_setup"]); ?>
					</div>
				</div>
				
			</div>
		</div>
	</div>
	
	


    
    
    <?php
    $pack_json_ob = json_decode($pack_record["pack_json"], true);
    $files = $pack_json_ob["files"];
    if(count($files) > 0)
    {
        ?>
        
		
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(2706); ?>
				</div>
				<div class="panel-body">
					 <?php
					for($f = 0; $f < count($files); $f++)
					{
						if($f > 0)
						{
							?>
							<div class="hr-line-dashed col-lg-12"></div>
							<?php
						}
						?>
						<div class="col-lg-12">
							<?php echo $files[$f]["server_path"].$files[$f]["file_name"] ?>
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
    
    
    
    <?php
    $templates = $pack_json_ob["templates"];
    if(count($templates) > 0)
    {
        ?>
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(2707); ?>
				</div>
				<div class="panel-body">
					<div class="table-responsive">
						<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
							<thead>
								<th>ID</th>
								<th><?php echo translate_str_by_id(2277); ?></th>
							</thead>
							<tbody>
								<?php
								for($t = 0; $t < count($templates); $t++)
								{
									?>
									<tr> 
										<td><?php echo $templates[$t]["id"];?></td>
										<td><?php echo $templates[$t]["caption"];?></td>
									</tr>
									<?php
								}
								?>
							</tbody>
						</table>
					</div>
				</div>
			</div>
		</div>
        <?php
    }
    ?>
    
    
    
    
    <?php
    $modules_prototypes = $pack_json_ob["modules_prototypes"];
    if(count($modules_prototypes) > 0)
    {
        ?>
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(2708); ?>
				</div>
				<div class="panel-body">
					<div class="table-responsive">
						<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
							<thead>
								<th>ID</th>
								<th><?php echo translate_str_by_id(2277); ?></th>
							</thead>
							<tbody>
								<?php
								for($m = 0; $m < count($modules_prototypes); $m++)
								{
									?>
									<tr>
										<td><?php echo $modules_prototypes[$m]["id"];?></td>
										<td><?php echo $modules_prototypes[$m]["caption"];?></td>
									</tr>
									<?php
								}
								?>
							</tbody>
						</table>
					</div>
				</div>
			</div>
		</div>
        <?php
    }
    ?>
    
    
    
    
    <?php
    $plugins = $pack_json_ob["plugins"];
    if(count($plugins) > 0)
    {
        ?>
		<div class="col-lg-12">
			<div class="hpanel">
				<div class="panel-heading hbuilt">
					<?php echo translate_str_by_id(2709); ?>
				</div>
				<div class="panel-body">
					<div class="table-responsive">
						<table cellpadding="1" cellspacing="1" class="table table-condensed table-striped">
							<thead>
								<th>ID</th>
								<th><?php echo translate_str_by_id(2277); ?></th>
							</thead>
							<tbody>
							<?php
							for($p = 0; $p < count($plugins); $p++)
							{
								?>
								<tr>
									<td><?php echo $plugins[$p]["id"];?></td>
									<td><?php echo $plugins[$p]["caption"];?></td>
								</tr>
								<?php
							}
							?>
							</tbody>
						</table>
					</div>
				</div>
			</div>
		</div>
        <?php
    }
    ?>
    
    
    
    
    
    <script>
    //Функция удаления пакета
    function deletePack()
    {
        if(!confirm("<?php echo translate_str_by_id(2710); ?>"))
        {
            return;
        }
        
        
        jQuery.ajax({
                type: "POST",
                async: true, //Запрос асинхронный
                url: "/<?php echo $DP_Config->backend_dir; ?>/content/packs_control/ajax_delete_pack.php",
                dataType: "json",//Тип возвращаемого значения
                data: "pack_id=<?php echo $_GET["pack_id"]; ?>&csrf_guard_key=<?php echo $user_session["csrf_guard_key"]; ?>",
                success: function(answer) {
                    on_after_detele_answer(answer);
                }
            });
    }
    //Обработка ответа об удалении
    function on_after_detele_answer(answer)
    {
        //Удалено успешно
        if(answer.result_code == 0)
        {
            //Переадресация на страницу пакетов
            alert("<?php echo translate_str_by_id(2711); ?>");
            location="<?php echo $DP_Config->domain_path.$DP_Config->backend_dir;?>/packs/packs_manager";
        }
        else//Ошибка на данном шаге:
        {
            document.getElementById("message_div").innerHTML = "<h4 class=\"alert_error\">"+answer.message+"</h4>";
        }
    }
    </script>
    <?php
}
?>