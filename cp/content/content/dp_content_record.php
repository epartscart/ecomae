<?php
/**
 * Скрипт содержит опеределения:
 * - класс Записи материала
*/
// --------------------------------- Start PHP - класс ---------------------------------
//Класс Материал
/*
Материал может быть представлен в таблице БД, а также в узле дерева webix. Технические особенности таковы, что в БД и в Webix не получается сделать набор данных с одинаковыми именами как для webix, так и для БД
Поэтому, класс DP_ContentRecord может дублировать одну и ту же сущность в двух полях с разными именами, одно из которых используется в БД, а другое в Webix
*/
class DP_ContentRecord extends DP_Content
{
    public $id;//ID материала - ID узла в дереве webix
    public $count;//Количество вложенных элементов
    public $url;//URL материала
    public $level;//Уровень вложенности, начиная с 1
    public $alias;//Alias материала
    public $value;//Название материала
    public $parent;//ID материала - ID узла в дереве webix, который является родителем этого узла
    public $description;//Текстовое описание
    public $main_flag;//Флаг - Главный материал
    public $title_tag;//Тег title
    public $description_tag;//Мета-тег description
    public $keywords_tag;//Мета-тег keywords
    public $author_tag;//Мета-тег author
    public $robots_tag;//Мета-тег robots
    public $modules_array = array();//Подключенные модули к данной странице
    public $system_flag;//Является системным - означает, что материал обеспечивает работы системы (Ядро+Оболочка) и его нельзя удалить или отредактировать. Кроме этого, это поле не редактируется через интерфейс, т.е. только выставляется вручную в БД
    public $published_flag;//Флаг - Материал опубликован
    public $open;//Флаг - узел открыт при отображении дерева материалов - поле в дереве webix
    public $css_js;//Подключаемые теги css и js
    
    public $groups_access = array();//Массив c ID групп пользователей, допущенных к этому материалу
    public $data = array();//Массив с вложенными объектами материалов (в таблице БД это поле отсутствует)
    
	
	
	
	//Мультиязычность. Массив с именами полей, которые переводятся на языки
	public $translated_items = array('value', 'description', 'title_tag', 'description_tag', 'keywords_tag', 'author_tag');
	
	
	
    public function __construct()
    {
        $this->id = 0;
        $this->count = 0;
        $this->url = "";
        $this->level = 0;
        $this->alias = "";
        //$this->value = "";
        $this->parent = 0;
        //$this->description = "";
        $this->main_flag = 0;
        //$this->title_tag = "";
        //$this->description_tag = "";
        //$this->keywords_tag = "";
        //$this->author_tag = "";
        $this->robots_tag = "";
        $this->system_flag = 0;
        $this->published_flag = 1;
        $this->open = true;
        $this->css_js = "";
		
		
		
		
		//Мультиязычные поля
		for( $i=0 ; $i < count( $this->translated_items ) ; $i++ )
		{
			$str = "";
			$str_id = 0;
			
			//Для нового материала, когда он добавляется в редакторпе дерева - указываем имя по умолчанию
			if( $this->translated_items[$i] == 'value' )
			{
				$str_id = 2220;//ID строки "Новый материал"
				$str = translate_str_by_id($str_id);
			}
			
			
			$this->{$this->translated_items[$i]} = $str;//Само поле - пустая строка
			$this->{$this->translated_items[$i]."_lang_str_id"} = $str_id;//ID строки
		}
    }
}//~class DP_ContentRecord
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ End PHP - класс ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
?>