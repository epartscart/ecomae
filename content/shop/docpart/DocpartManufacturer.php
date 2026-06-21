<?php
/**
 * Определение класса бренда (для протокола - шаг 1)
 * 
*/
class DocpartManufacturer
{
    //ПОЛЯ ПРОДУКТА
    public $manufacturer;//Производитель, выдаваемый поставщиком
    public $manufacturer_id;//ID производителя у поставщика
	public $manufacturer_show;//Производитель для показа (берется из словаря синонимов)
	public $name;//Наименование товара
	public $storage_id;//ID поставщика
	public $office_id;//ID офиса обслуживания
   
	
	//Технические параметры
    public $synonyms_single_query;//Посылать только один запрос для синонима
	/* Есть такие поставщики, которые по запросу артикула показывают производителей, которые являются синонимами, например KNECHT и MAHLE
	При этом, если сделать запрос по каждому из них - результат будет одинаковый. Во избежание таких ситуаций используется флаг synonyms_single_query, который не даст сделать запрос по каждому синониму */
	
	
	public $params;//Ассоциативный массив - для записи любых значений, которые могут потребоваться для конкретного поставщика
	
	
	public $valid;//Флаг корректности данных
	
	
    public function __construct($manufacturer,
        $manufacturer_id,
        $name,
		$office_id,
        $storage_id,
		$synonyms_single_query,
		$params = NULL
    )
    {
		$manufacturer = trim($manufacturer);
		
		//Инициализация полей
        $this->manufacturer = $manufacturer;
        $this->manufacturer_id = $manufacturer_id;
        $this->manufacturer_show = mb_strtoupper($manufacturer, 'UTF-8');
        $this->name = str_replace(array("\n", "\t", "\r", "\\"), '', trim((string)$name));
		$this->storage_id = $storage_id;
        $this->office_id = $office_id;
		$this->synonyms_single_query = $synonyms_single_query;
		$this->params = $params;
		
		// Валидация данных
		if(mb_strlen($manufacturer, 'UTF-8') > 1){
			$this->valid = true;
		}else{
			$this->valid = false;
		}
    }
}
?>