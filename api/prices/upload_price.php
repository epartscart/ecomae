<?php
	require_once($_SERVER["DOCUMENT_ROOT"].'/config.php');	
	$DP_Config = new DP_Config();	
	
	
	//Подключение к БД
	try
	{
		$db_link = new PDO('mysql:host='.$DP_Config->host.';dbname='.$DP_Config->db, $DP_Config->user, $DP_Config->password);
	}
	catch (PDOException $e) 
	{
		$answer = array();
		$answer["status"] = false;
		$answer["data"] = "NO DB CONNECT";
		exit(json_encode($answer));
	}
	$db_link->query("SET NAMES utf8;");
	
	// -------------------------------------------------------------------------------
	//Подключение мультиязычности
	require_once($_SERVER["DOCUMENT_ROOT"]."/lang/dp_lang.php");
	$multilang_params = multilang_init();
	// -------------------------------------------------------------------------------
	
	
	if ($_POST["tech_key"] != $DP_Config->tech_key)
	{
		$awnser = [
			'status' => false,
			'data' => translate_str_by_id(2056);//STR Не правильно указан ключ
		];
		exit(json_encode($awnser));
	}
	
	
	
	function clear_dir($dir, $clear_only) 
	{
		foreach(glob($dir . '/*') as $file) 
		{
			if(is_dir($file))
			{
				clear_dir($file, false);
			}
			else
			{
				$file_name = explode("/", $file);
				$file_name = $file_name[ count($file_name) - 1 ];
				if( $file_name != "index.html" )
				{
					unlink($file);
				}
			}
		}
		if(!$clear_only)
		{
			rmdir($dir);
		}
	}
	
	function import_csv_to_db($clean_before, $price_id)
	{	
		global $DP_Config;
		$url = $DP_Config->domain_path.$DP_Config->backend_dir.'/content/shop/prices_upload/ajax_5_import_csv_to_db.php?price_id='.$price_id.'&initiator=js&clean_before='.$clean_before."&key=".$DP_Config->tech_key;
		
		 if( $curl = curl_init() ) {
			curl_setopt($curl, CURLOPT_URL, $url);
			curl_setopt($curl, CURLOPT_RETURNTRANSFER,true);
			$result = curl_exec($curl);
			curl_close($curl);
			
			$awnser = json_decode($result);
			if ($awnser->result == 1)
			{
				$url = $DP_Config->domain_path.$DP_Config->backend_dir.'/content/shop/prices_upload/ajax_6_complete_session.php?price_id='.$price_id."&key=".$DP_Config->tech_key;
				$curl = curl_init();
				curl_setopt($curl, CURLOPT_URL, $url);
				curl_setopt($curl, CURLOPT_RETURNTRANSFER,true);
				$result = curl_exec($curl);
				curl_close($curl);
				
				$awnser = json_decode($result);
				
				if ($awnser->result != 1)
				{
					$awnser = [
						'status' => false,
						'data' => translate_str_by_id(2057)//STR Файл был загружен, но возникла ошибка при обновлении времени последнего изменения.
					];
					exit(json_encode($awnser));
				}
			}
			else
			{
				$awnser = [
					'status' => false,
					'data' => translate_str_by_id(2058)//STR Ошибка загрузки прайса в базу данных.
				];
				exit(json_encode($awnser));
			}
		  }
	
	}
	///////////////////////////////////////////////////////////
	
	$SQL_select = "SELECT * FROM `shop_docpart_prices` WHERE `id` =  ?;";
	$query = $db_link->prepare($SQL_select);
	$query->execute( array( $_POST["id"] ) );
	
	$result = $query->fetch();
	
	if ( $result )
	{
		//Проверяем наличие временного каталога для загрузки. ПРИ НЕОБХОДИМОСТИ 0 СОЗДАЕМ
		$treelax_tmp_dir = $_SERVER["DOCUMENT_ROOT"]."/".$DP_Config->backend_dir.$DP_Config->tmp_dir_prices_upload;//Путь к каталогу для загрузки файлов прайс-листов
		if(!is_dir($treelax_tmp_dir))
		{
			if(!mkdir($treelax_tmp_dir))
			{
				$awnser = [
					'status' => false,
					'data' => translate_str_by_id(2059)//STR Не удалось создать временный каталог.
				];
				exit(json_encode($awnser));
			}
		}
		else//Каталог есть - предварительно очищаем его
		{
			clear_dir($treelax_tmp_dir, true);//Функция очистки каталога (true - очистить, а сам каталог оставить)
		}
		$price_id = (int)$_POST["id"];
		$origName = basename((string)($_FILES["document"]["name"] ?? 'price.txt'));
		if ($origName === '' || $origName === '.') {
			$origName = 'price.txt';
		}
		$name = $treelax_tmp_dir.'/'.$origName;
		move_uploaded_file($_FILES["document"]["tmp_name"], $name);
		import_csv_to_db(true, $price_id);
	}
	else
	{
		$awnser = [
			'status' => false,
			'data' => translate_str_by_id(2060)//STR Прайс лист с данным id не был обнаружен.
		];
		exit(json_encode($awnser));
	}
	
	
	$awnser = [
			'status' => true,
			'data' => translate_str_by_id(2061)//STR Файл успешно загружен.
		];
	exit(json_encode($awnser));
?>