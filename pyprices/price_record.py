#Определение класса товарной позиции из прайс-листа
"""
Валидацию значений колонок пока не делаем. Возможно она совсем не нужна. Так даже будет быстрее обрабатываться.
Т.е. в БД могут быть записаны некорректные значения из файла. А потом все-равно при проценке они не будут показываться. Но, при желании, админ сможет посмотреть, что загрузилось в БД
"""

# --------------------------------------------------------------
# --------------------------------------------------------------

from price_parser import Price
import re


class PriceRecord:

    #Для инициализации передается словарь (dict) со значениеми, непосредственно полученными из файла прайс-листа
    """
    Лучше передавать именно словарь record, т.к. его проще будет сформировать, исходя из массива возможных колонок и их фактических настроек для данного прайс-листа. Если номер колонки указан в настройках задания, то, читаем содержимое и записываем в словарь
    """
    
    #Все поля, которые могут быть прочитаны из файла
    price_fields = ['manufacturer', 'article', 'name', 'exist', 'price', 'time_to_exe', 'storage', 'min_order']
    
    # --------------------------------------------------------------
    
    #Конструктор
    def __init__(self, record):
        
        #Значения позиции по-умолчанию
        self.manufacturer = ""
        self.article = ""
        self.article_show = ""
        self.name = ""
        self.exist = 0
        self.price = 0
        self.time_to_exe = 0
        self.storage = ""
        self.min_order = 0
        
        
        #Исходная инициализация значений полей (прямо из файла). Если в record нет какого-то поля, то, его значение останется по-умолчанию
        for key, value in record.items():
            #Инициализируем значение
            setattr(self, key, value)
        
        
        #Значения из файла могут быть некорректными, например, цена с руб и т.д. Поэтому тут нужно привести значения к технически корректному виду, например 2.500,54 руб к 2500.54
        #Все колонки, какие были в файле - получены. Далее делаем валидацию своих полей (на техническую корректность)
        
        #БЕЗ ОБРАБОТОК
        #Производитель
        #self.manufacturer = ""
        #Наименование
        #self.name = ""
        #Склад
        #self.storage = ""
        
        
        
        
        #ОБРАБОТКА АРТИКУЛА:
        """
        Варианты с артикулами.
        1. Обычная строка с буквами и цифрами.
        2. Строка, которая представляет число int или float, в том числе с ведущими нулями:
        - если эта строка взята из Excel-файла, значение приходит float и имеет вид 123.0 (если изначально было 123)
        - если в начале строки были ведущие нули, например 00123, то, необходимо их сохранить
        """
        #Запомнили исходное значение артикула
        article_buf = self.article
        try:
            #Пробуем привести к float
            float(article_buf)
            
            #Приводится. Значит содержит только цифры и максимум одну точку
            if float(article_buf) == int( float(article_buf) ):
                #После точки - 0
                self.article = str( int( float(self.article) ) )
            else:
                #После точки НЕ 0
                self.article = str( float(self.article) )
            
            self.article_show = self.article
            self.article = re.sub('[^a-zA-Z0-9а-яА-Я]+', '', str(self.article) )
            
            #Если в исходном артикуле были ведущие нули, то, их нужно вернуть
            for i in range(0, len(article_buf)):
                if article_buf[i] == '0':
                    self.article = '0' + self.article
                    self.article_show = '0' + self.article_show
                else:
                    break
            
        except Exception:
            #Не приводится, значит - можно просто привести к строке
            self.article_show = str(self.article)
            self.article = re.sub('[^a-zA-Z0-9а-яА-Я]+', '', str(self.article) )
        
        
        #Артикул для показа оставляем "как есть"
        #self.article_show = self.article
        #Артикул для поиска оставляем только буквы и цифры
        #self.article = re.sub('[^a-zA-Z0-9а-яА-Я]+', '', str(self.article) )
        
        #Количество в наличии
        self.exist = re.sub('[^0-9,.]+', '', str(self.exist) ) #Оставили только цифры и точки-запятые
        try:
            self.exist = int(float(self.exist))
        except Exception:
            self.exist = 0
        
        #Цена
        self.price = Price.fromstring( str(self.price) ).amount
        
        #Срок доставки
        try:
            self.time_to_exe = int(self.time_to_exe)
        except Exception:
            self.time_to_exe = 0
        
        #Минимальный заказ
        try:
            self.min_order = int(self.min_order)
        except Exception:
            self.min_order = 0
        
        
        
    # --------------------------------------------------------------
    
    #Метод получения строки на основе объекта (строка соответствует структуре таблицы shop_docpart_prices_data)
    def get_like_table_string(self, price_id):
        return "NULL;" + str(price_id) + ";" + str(self.manufacturer) + ";" + str(self.article) + ";" + str(self.article_show) + ";" + str(self.name) + ";" + str(self.exist) + ";" + str(self.price) + ";" + str(self.time_to_exe) + ";" + str(self.storage) + ";" + str(self.min_order) + ";" + str(0) + "\n"
    
    # --------------------------------------------------------------
    
    #Метод получения кортежа на основе объекта (состав и порядок соответствуют структуре таблицы shop_docpart_prices_data)
    def get_like_tuple(self, price_id):
        return ( None, price_id, self.manufacturer, self.article, self.article_show, self.name, self.exist, self.price, self.time_to_exe, self.storage, self.min_order, 0)
    
    # --------------------------------------------------------------
    
# --------------------------------------------------------------
# --------------------------------------------------------------