<?php
/**
 * Скрипт содержит опеределения:
 * - класс Записи категории товара
*/
// --------------------------------- Start PHP - класс ---------------------------------
//Класс категория товара
class DP_CatalogueCategory
{
    public $id;//ID категори - ID узла в дереве webix
    public $alias;//Псевдоним категори
    public $url;//URL категории
    public $count;//Количество вложенных элементов
    public $level;//Уровень вложенности, начиная с 1
    public $value;//Название категории
    public $value_lang_str_id;//Мультиязычность - id строки с названием категории
    public $parent;//ID категории - ID узла в дереве webix, который является родителем этого узла
    public $title_tag;//Тег title
    public $description_tag;//Мета-тег description
    public $keywords_tag;//Мета-тег keywords
    public $robots_tag;//Мета-тег robots
    
    public $import_format;//Формат импорта
    public $export_format;//Формат экспорта
    
    public $image;//Имя файла изображения категории, который находится в каталоге: /content/files/images/catalogue_images/...
    
    public $image_url;//Переменная для хранения изображения при работе в браузере
    
    public $data = array();//Массив с вложенными объектами категорий (в таблице БД это поле отсутствует)
    
    public $properties = array();//Массив для описания свойств категории
    
	public $order;//Порядок отображения данной категории
	
	public $published_flag;//Флаг - выводить для покупателя
	
	
	//Поля используемые только для товара. Т.е. когда данный класс используется для описания товара, а не категории
	public $is_product;//Флаг - Является товаром
	public $product_id;//ID товара
	
	
	//Поля, используемые в редакторе дерева категорий
	public $img_blob;//BLOB для изображения категории, когда категория создана на основе шаблона
	public $img_blob_name;//Имя файла изображения, когда категория создана на основе шаблона
	public $by_template;//ID шаблона, если категория создана на основе шаблона
	
	
	//Мультиязычность. Хардкодный массив с именами полей, которые переводятся на разные языки
	public $translated_items = array('value', 'title_tag', 'description_tag', 'keywords_tag');
	
    public function __construct()
    {
        $this->id = 0;
        $this->alias = "";
        $this->url = "";
        $this->count = 0;
        $this->level = 0;
        //$this->value = "";
        $this->parent = 0;
        //$this->title_tag = "";
        //$this->description_tag = "";
        //$this->keywords_tag = "";
        $this->robots_tag = "";
        $this->import_format = "";
        $this->export_format = "";
        $this->image = "";
		
        $this->image_url = "";
		$this->order = 0;
		$this->published_flag = 1;
		
		
		$this->is_product = false;
		$this->product_id = 0;
		
		
		$this->img_blob = '';
		$this->img_blob_name = '';
		$this->by_template = 0;
		
		
		//Мультиязычные поля
		for( $i=0 ; $i < count( $this->translated_items ) ; $i++ )
		{
			$str = "";
			$str_id = 0;
			
			//Для новой категории, когда она добавляется в редакторпе дерева - указываем имя по умолчанию
			if( $this->translated_items[$i] == 'value' )
			{
				$str_id = 2806;//ID строки "Новая категория"
				$str = translate_str_by_id($str_id);
			}
			
			
			$this->{$this->translated_items[$i]} = $str;//Само поле - пустая строка
			$this->{$this->translated_items[$i]."_lang_str_id"} = $str_id;//ID строки
		}
		
    }
}//~class DP_CatalogueCategory
// ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ End PHP - класс ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
?>