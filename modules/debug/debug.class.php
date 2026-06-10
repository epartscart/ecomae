<?php

/*****************************************
 * Редакция: 2020.06.09
*****************************************
*/

class Debug {
	
	private $errors;
	public $date;	
	
	//------------------------------------------------------------------------
	public function __construct($params = array(), $soap_version = NULL) {
		$this->errors = array();
		$this->date = date("dmY_Hi");

		$this->start_log();
	}
	
	
	//------------------------------------------------------------------------
	//Метод начала нового лога, т.е. создаем файл с нуля
	public function start_log()
	{
		//Открываем файл в режиме "w"
		$log = fopen( $_SERVER["DOCUMENT_ROOT"]."/modules/debug/tmp/".$this->date.".php", "w" );
		
		//Запрет просмотра вне веб-интерфейса
		/* fwrite($log, '<?php defined(\'_ASTEXE_\') or die(\'No access\'); ?>'); */
			
		//Стиль для тега pre - чтобы отображать содержимое без прокрутки
		fwrite($log, "
		<style>
		pre
		{
			white-space: pre-wrap;
			white-space: -moz-pre-wrap;
			white-space: -pre-wrap;
			white-space: -o-pre-wrap;
			word-wrap: break-word;
			max-height: 600px;
		}
		</style>
		");
				
		fwrite($log, "<div class=\"col-lg-12\">
		 <div>".translate_str_by_key('1711376682_1_5f735d1486aa51eb9a61df1cd635a0fb')."</div>");
		
		fwrite($log, date("<p>".translate_str_by_key('4222')." d.m.Y H:i:s</p>", time()) );
		fwrite($log, "</div>");
		
		fclose($log);
	}

	
	//------------------------------------------------------------------------
	//Метод записи шапки логов
	public function logger_collapse_start($title = '')
	{
		//Открываем файл в режиме "a"
		$log = fopen( $_SERVER["DOCUMENT_ROOT"]."/modules/debug/tmp/".$this->date.".php", "a" );

		//Записываем лог запроса
		fwrite($log, "<div class=\"col-lg-12\">");
		fwrite($log, "<div class=\"hpanel panel-collapse\">");
		fwrite($log, "<div class=\"panel-heading hbuilt\" style=\"background-color:#cecece;color:#000;\">");
		fwrite($log, $title);
		fwrite($log, "<div class=\"panel-tools\"><a class=\"showhide\"><i class=\"fa fa-chevron-down\" style=\"color:#000;\"></i></a></div>");
		fwrite($log, "</div>");
		fwrite($log, "<div class=\"panel-body\" style=\"display:none;\">");

		//Закрываем файл
		fclose($log);
	}

		//------------------------------------------------------------------------
	//Метод записи шапки логов
	public function logger_collapse_end()
	{
		//Открываем файл в режиме "a"
		$log = fopen( $_SERVER["DOCUMENT_ROOT"]."/modules/debug/tmp/".$this->date.".php", "a" );

		//Записываем лог запроса
		fwrite($log, "</div>");
		fwrite($log, "</div>");
		fwrite($log, "</div>");

		//Закрываем файл
		fclose($log);
	}

	//------------------------------------------------------------------------
	//Метод записи логов
	public function logger($title = '', $obj = '', $array = false)
	{
		//Открываем файл в режиме "a"
		$log = fopen( $_SERVER["DOCUMENT_ROOT"]."/modules/debug/tmp/".$this->date.".php", "a" );

		$text = $obj;
		
		if($array) {
			ob_start();
			echo "<pre>";
			print_r($obj);
			echo "</pre>";
			$text = ob_get_contents();
			ob_end_clean();
		}

		//Записываем лог запроса
		fwrite($log, "<div class=\"col-lg-12\">");
		fwrite($log, "<p>".$title.":</p><pre>".$text."</pre>");
		fwrite($log, "</div>");

		//Закрываем файл
		fclose($log);
	}
	
	//------------------------------------------------------------------------	
	private function reset_errors() {
		$this->errors = array();
	}
	

}

?>