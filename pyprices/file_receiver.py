#Здесь определения классов для получения файлов из источников

import os
import shutil
import zipfile #Для распаковки zip
import patoolib #Для распаковки tar
import rarfile #Для распаковки rar
import py7zr #Для распаковки 7z

#Работы с файлами и директориями
from os import listdir
from os.path import isfile, join


#Для работы с URL
import ssl
import urllib
import urllib.request
import urllib.error
from urllib.parse import urlparse
from pathlib import Path

#Для работы с FTP
from ftplib import FTP

#Для работы с E-mail (напрямую не требуется, т.к. работа идет через docpart_imap)
#from imap_tools import MailBox, A

# --------------------------------------------------------------
# --------------------------------------------------------------

#Родительский класс
class file_receiver:
    
    # --------------------------------------------------------------
    
    extensions_suitable_arch = ['zip', 'rar', '7z', 'tar'] #Подходящие расширения для архивов
    
    extensions_suitable = ['txt', 'csv', 'xls', 'xlsx'] #Подходящие расширения конечных файлов
    
    # --------------------------------------------------------------
    
    #Общий конструктор. Инициализируеет свое поле "объект задания"
    def __init__(self, item, launch_id):
        self.item = item
        self.launch_id = launch_id
    
    # --------------------------------------------------------------
    
    #Чтобы можно было использовать контекстный менеджер
    def __enter__(self):
        return self
    def __exit__(self, exception_type, exception_value, traceback):
        #Тут можно что-то сделать
        pass
    
    # --------------------------------------------------------------
    
    #Метод получения файла. Будет переопределяться в классах-потомках
    def get_file(self):
        pass
    
    # --------------------------------------------------------------
    
    #Метод создания папки
    def create_item_folder(self):
        
        if not os.path.isdir( 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) ):
            #Создаем директорию
            os.mkdir( 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) )
            if not os.path.isdir( 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) ):
                #Не создалин
                raise Exception('Could not create directory ' + 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) )
                return
        else:
            #Директория уже была до вызова метода. Такого быть не должно
            raise Exception('Directory ' + 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) + ' already existed before the method was called. This should not happen' )
            return
    
    # --------------------------------------------------------------
    
    #Метод получения расширения файла (статический, чтобы им можно было бы пользоваться в других местах)
    @staticmethod
    def get_file_extension(filename):
        
        #Сплитим по точке
        file_extension = filename.split('.')
        
        #Если точки не было в имени файла, возвращаем некорректное расширение - файл не пройдет проверку
        if len(file_extension) == 1:
            return 'noext'
        
        #Возвращаем фактическое расширение
        return str(file_extension[ len(file_extension) - 1 ]).lower()
    
    # --------------------------------------------------------------
    
    #Метод проверки применимости файла по подстроке (файл НЕ архив)
    def is_file_suitable_by_substring(self, filename):
        
        #В объекте задания подстрока не задана - имя файла можно не проверять - он подходит
        if self.item.file_name_substring is None:
            return True
        
        
        #Проверяем вхождение подстроки в имени файла
        if filename.find( self.item.file_name_substring ) == -1:
            return False
        else:
            return True
    
    # --------------------------------------------------------------
    
    #Метод проверки применимости файла по подстроке (файл - АРХИВ)
    def is_file_suitable_by_substring_arch(self, filename):
        
        #В объекте задания подстрока для архива не задана - имя файла можно не проверять - он подходит
        if self.item.file_name_substring_arch is None:
            return True
        
        
        #Проверяем вхождение подстроки в имени файла
        if filename.find( self.item.file_name_substring_arch ) == -1:
            return False
        else:
            return True
    
    # --------------------------------------------------------------
    
    #Универсальный метод распаковки архив. Архив должен находиться в папке tmp/launch_id/<price_id>. Распаковка происходит тут же и сам архив удаляется после распаковки.
    def extract_archive(self, arch_name):
        
        #Получили расширение архива
        file_extension = self.get_file_extension(arch_name)
        
        #Путь к директории, в которой лежит архив
        arch_dir = 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id)
        
        
        #Путь к архиву, в месте с директорией (директория и имя файла)
        arch_file_path = arch_dir + '/' + arch_name
        
        
        #В зависимости от типа архива, можем использовать разные инструменты
        if file_extension == 'zip':
            with zipfile.ZipFile(arch_file_path, 'r') as zip_ref:
                
                #Цикл по файлам внутри архива
                for fileinfo in zip_ref.infolist():
                    #Получаем имя файла (с учетом того, что оно может быть кириллическим)
                    #filename = fileinfo.filename.encode('cp437').decode('cp866')
                    #filename = fileinfo.filename.encode('').decode('utf-8')
                    filename = fileinfo.filename
                    #Просто распаковать не можем, если файл с кириллическим именем. Поступаем хитро - открываем новый файл на запись в бинарном виде и указываем декодированное имя. Затем с помощью shutil.copyfileobj копируем объект файла из архива в бинаром виде в созданный файл - получится распакованный файл с исходным именем
                    with open(arch_dir + '/' + filename, "wb") as outputfile:
                        shutil.copyfileobj(zip_ref.open(fileinfo.filename), outputfile)
                
                #zip_ref.extractall( arch_dir ) #Не рабочий вариант, если запакованные файлы имеют кириллические имена
        elif file_extension == 'rar':
            
            #Для распаковки rar используем библиотеку rarfile. Она использует для распаковки стандартный unrar. Если на сервере не будет установлен unrar - работать не будет. Поэтому, в дистрибутиве предусмотреть проверки наличия unrar
            with rarfile.RarFile(arch_file_path, 'r') as rar_ref:

                try:
                    rar_ref.extractall( arch_dir )
                except Exception as err:
                    raise Exception( "Archive " + str(err) )
                    #Здесь можем записать лог - не распаковался архив
            
        elif file_extension == 'tar':
            
            #Для tar, patoolib не вызывает проблем с кириллическими именами файлов внутри архива
            patoolib.extract_archive(arch_file_path, outdir = arch_dir, verbosity = -1)
            
        elif file_extension == '7z':
            
            #Протестировали с кириллическими именами - работает
            py7zr.unpack_7zarchive(arch_file_path, arch_dir)
            
        else:
            raise Exception('Archive with extension ' + file_extension + ' cannot be extracted')
            return
        
        #Здесь нужно будет удалить архив
        self.del_file_from_tmp(arch_name)
    
    # --------------------------------------------------------------
    
    #Метод удаления файла из директории в tmp/launch_id/<price_id> (распаковнный архив, либо, файл, который не подходит по имени или типу)
    def del_file_from_tmp(self, file_name):
        if Path('tmp/' +str(self.launch_id) + '/' + str(self.item.price_id) + '/' + file_name).is_file():
            os.remove( 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) + '/' + file_name )
    
    # --------------------------------------------------------------
    
# --------------------------------------------------------------
# --------------------------------------------------------------


#Класс для получения файла с local_path
class file_receiver_local_path(file_receiver):
    
    # --------------------------------------------------------------
    
    #Метод удаления исходного файла (оттуда, где он находился перед вызовом загрузчика - куда его загрузил пользователь через веб-интерфейс). Удаляется именно временная папка с уникальным именем, в которую был предварительно загружен этот файл с ПК на сервер. Папка с уникальнмы именем была создана именно под этот файл - чтобы защитить процесс от колизий (от перетирания файлов с одинаковыми именами, если пользователь будет загружать такие файлы одновременно)
    def del_file(self):
        #Если установлен соответствющий флаг
        if self.item.del_file_from_local_path:
            #Директория, в которой находится файл от корня (последний элемент в этой директории - как раз удаляемая временная папка с уникальным именем):
            path_to_file = os.path.dirname(self.item.local_path)
            
            #Имя временной папки, которую нужно удалить (уникальное имя)
            tmp_folder_name = os.path.basename(path_to_file)
            
            #Значения должны совпасть (то, что получили из self.item.local_path и то, что получили от пользователя в объекте задания)
            if tmp_folder_name != self.item.tmp_folder_name:
                raise Exception("Error checking the unique name of a temporary folder before deleting it. Deletion was not executed.")
                return
            
            #Проверяем наличие самой папки
            if os.path.isdir(path_to_file):
                #Удаляем временную папку (вместе с исходным файлов в ней)
                shutil.rmtree(path_to_file, ignore_errors=True)
    
    # --------------------------------------------------------------
    
    #Метод получения файла
    def get_file(self):
        
        #Перед получением файла создаем папку для него
        try:
            self.create_item_folder()
        except Exception as err:
            raise Exception(err)
            return
        
        
        #Проверяем наличие файла
        if not os.path.isfile(self.item.local_path):
            raise Exception('The file specified in local_path was not found')
            return
        
        
        
        #Имя файла в формате <filename>.<extension>
        filename = os.path.basename(self.item.local_path)
        
        
        #Проверяем тип файла
        file_extension = self.get_file_extension(filename)
        if file_extension not in set( self.extensions_suitable_arch ) and file_extension not in set( self.extensions_suitable ):
            raise Exception( 'The file specified in local_path has incorrect format: ' + file_extension )
            return
        
        
        #Если файл - НЕ архив, т.е. готовый рабочий рабочий файл
        if file_extension in set( self.extensions_suitable ):
            #Проверяем, подходит ли по фильтру имени файла
            if not self.is_file_suitable_by_substring(filename):
                #Удаляем исходный файл
                self.del_file()
                raise Exception( 'The file name did not match' )
                return
            else:
                #Файл подошел - копируем его в папку для последующей обработки
                shutil.copyfile(self.item.local_path, 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) + '/' + filename )
                #Удаляем исходный файл
                self.del_file()
        else:
            #Файл является архивом.
            #Проверяем, подходит ли он по имени
            if not self.is_file_suitable_by_substring_arch(filename):
                #Удаляем исходный файл
                self.del_file()
                raise Exception( 'The file name did not match' )
                return
            else:
                #Файл подошел по имени - копируем его в папку, распаковываем и проверяем дальше
                shutil.copyfile(self.item.local_path, 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) + '/' + filename )
                #Удаляем исходный файл
                self.del_file()
                
                #Распаковываем архив (распаковка архива, удаление архива после распаковки)
                self.extract_archive(filename)
                
                #ЗДЕСЬ БУДЕТ ЦИКЛ ПО РАСПАКОВАННЫМ ФАЙЛАМ
                
                #Получаем список файлов в директории (через генераторное выражение)
                item_path = 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id)
                unpacked_files_list = [f for f in listdir(item_path) if isfile(join(item_path, f))]
                
                #Цикл по распакованным файлам
                for file_name_in_arch in unpacked_files_list:
                    
                    #Проверяем тип файла
                    file_extension_unp = self.get_file_extension(file_name_in_arch)
                    if file_extension_unp not in set( self.extensions_suitable ):
                        #Добавляем сообщение в объект задания
                        self.item.other_messages.append( 'Extracted file [' + str(file_name_in_arch) + '] has wrong type. Accepted file types: ' + ', '.join(self.extensions_suitable) )
                        #Удаляем неподходящий файл
                        self.del_file_from_tmp(file_name_in_arch)
                        #Переходим к следующему файлу
                        continue
                    
                    #Тип файла подходящий. Проверяем по подстроке
                    #Проверяем, подходит ли по фильтру имени файла
                    if not self.is_file_suitable_by_substring(file_name_in_arch):
                        #Добавляем сообщение в объект задания
                        self.item.other_messages.append( 'Extracted file [' + str(file_name_in_arch) + '] does not match the substring in the file name: ' + str(self.item.file_name_substring) )
                        #Удаляем неподходящий файл
                        self.del_file_from_tmp(file_name_in_arch)
                        #Переходим к следующему файлу
                        continue
                    
                    
                    #Файл прошел проверки и остается в tmp/launch_id/<price_id> для последующего импорта. Добавляем сообщение в объект задания
                    self.item.other_messages.append( 'Extracted file [' + str(file_name_in_arch) + '] has SUCCESSFULLY passed the checks and remains for further processing' )
    
    
    # --------------------------------------------------------------

# --------------------------------------------------------------
# --------------------------------------------------------------


#Класс для получения файла с URL
class file_receiver_url(file_receiver):
    
    # --------------------------------------------------------------
    
    #Метод получения файла
    def get_file(self):
        
        #Перед получением файла создаем папку для него
        try:
            self.create_item_folder()
        except Exception as err:
            raise Exception(err)
            return
        
        
        #Имя файла в формате <filename>.<extension> (имя файла берется корректно даже при наличии GET-параметров в URL)
        filename = Path( urlparse(self.item.url).path ).name
        
        
        #Проверяем тип файла
        file_extension = self.get_file_extension(filename)
        if file_extension not in set( self.extensions_suitable_arch ) and file_extension not in set( self.extensions_suitable ):
            raise Exception( 'The file specified in the URL has incorrect format: ' + file_extension )
            return
        
        
        
        #Для запроса - игнорим SSL
        ctx = ssl.create_default_context()
        ctx.check_hostname = False
        ctx.verify_mode = ssl.CERT_NONE
        
        
        #Формируем запрос. В заголовках указываем браузер, т.к. некоторые сервера не отдают ответ роботам.
        req = urllib.request.Request(
            self.item.url, 
            data=None, 
            headers={
                'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_9_3) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/35.0.1916.47 Safari/537.36'
            }
        )
        
        

        #Открываем соединение по URL. Наличие файла явно не проверяем, т.к. работаем через try exсept и при ошибке, она обработается выше (при вызове данного метода)
        with urllib.request.urlopen(req, context=ctx) as response:
            
            #Если файл - НЕ архив, т.е. готовый рабочий рабочий файл
            if file_extension in set( self.extensions_suitable ):
                #Проверяем, подходит ли по фильтру имени файла
                if not self.is_file_suitable_by_substring(filename):
                    raise Exception( 'The file name did not match: ' + filename )
                    return
                else:
                    #Файл подошел - копируем его в папку для последующей обработки
                    with open('tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) + '/' + filename, "wb") as target_file:
                        #shutil.copyfileobj - функция позволяет обрабатывать данные частями, избегая проблем с памятью
                        shutil.copyfileobj( response, target_file )
                        #raise Exception( 'СКОПИРОВАЛИ' )
            else:
                #Файл является архивом.
                #Проверяем, подходит ли он по имени
                if not self.is_file_suitable_by_substring_arch(filename):
                    raise Exception( 'The file name did not match: ' + filename )
                    return
                else:
                    #Файл подошел по имени - копируем его в папку, распаковываем и проверяем дальше
                    with open('tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) + '/' + filename, "wb") as target_file:
                        shutil.copyfileobj( response, target_file )
                    
                    #Проверяем наличие файла после его скачивания
                    if not Path('tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) + '/' + filename).is_file():
                        raise Exception('Failed to download file')
                        return
                    
                    #Распаковываем архив (распаковка архива, удаление архива после распаковки)
                    self.extract_archive(filename)
                    
                    #ЗДЕСЬ БУДЕТ ЦИКЛ ПО РАСПАКОВАННЫМ ФАЙЛАМ
                    
                    #Получаем список файлов в директории (через генераторное выражение)
                    item_path = 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id)
                    unpacked_files_list = [f for f in listdir(item_path) if isfile(join(item_path, f))]
                    
                    #Цикл по распакованным файлам
                    for file_name_in_arch in unpacked_files_list:
                        
                        #Проверяем тип файла
                        file_extension_unp = self.get_file_extension(file_name_in_arch)
                        if file_extension_unp not in set( self.extensions_suitable ):
                            #Добавляем сообщение в объект задания
                            self.item.other_messages.append( 'Extracted file [' + str(file_name_in_arch) + '] has wrong type. Accepted file types: ' + ', '.join(self.extensions_suitable) )
                            #Удаляем неподходящий файл
                            self.del_file_from_tmp(file_name_in_arch)
                            #Переходим к следующему файлу
                            continue
                        
                        #Тип файла подходящий. Проверяем по подстроке
                        #Проверяем, подходит ли по фильтру имени файла
                        if not self.is_file_suitable_by_substring(file_name_in_arch):
                            #Добавляем сообщение в объект задания
                            self.item.other_messages.append( 'Extracted file [' + str(file_name_in_arch) + '] does not match the substring in the file name: ' + str(self.item.file_name_substring) )
                            #Удаляем неподходящий файл
                            self.del_file_from_tmp(file_name_in_arch)
                            #Переходим к следующему файлу
                            continue
                        
                        
                        #Файл прошел проверки и остается в tmp/launch_id/<price_id> для последующего импорта. Добавляем сообщение в объект задания
                        self.item.other_messages.append( 'Extracted file [' + str(file_name_in_arch) + '] SUCCESSFULLY passed the checks and remains for further processing' )
        
        
    
    # --------------------------------------------------------------


# --------------------------------------------------------------
# --------------------------------------------------------------


#Класс для получения файлов с FTP
class file_receiver_ftp(file_receiver):
    
    # --------------------------------------------------------------
    
    #Метод получения файла
    def get_file(self):
        
        #Перед получением файла создаем папку для него
        try:
            self.create_item_folder()
        except Exception as err:
            raise Exception(err)
            return
        
        
        #Подключаемся к FTP-серверу. В случае ошибки, исключение будет обработано выше, т.к. метод вызывается внутри try except
        with FTP(host = self.item.ftp_host, user = self.item.ftp_username, passwd = self.item.ftp_password) as ftp:
            
            #Устанавливаем кодировку для корректного чтения имен файлов на FTP-сервере
            ftp.encoding = "utf-8"
            
            #Заходим в директорию на FTP-сервере
            if self.item.ftp_folder is not None:
                ftp.cwd( self.item.ftp_folder )
            
            
            #Получаем список файлов на FTP-сервере
            filenames = ftp.nlst()
            
            #Цикл по списку файлов на FTP-сервере
            for filename in filenames:
                
                #Проверяем тип файла
                file_extension = self.get_file_extension(filename)
                if file_extension not in set( self.extensions_suitable_arch ) and file_extension not in set( self.extensions_suitable ):
                    self.item.other_messages.append( "File on FTP with file name [" + filename + "] has incorrect type" )
                    continue #К следующему файлу
                
                #Файл подходит по типу
                
                #Если это НЕ архив
                if file_extension in set( self.extensions_suitable ):
                    #Проверяем, подходит ли по фильтру имени файла
                    if not self.is_file_suitable_by_substring(filename):
                        self.item.other_messages.append( "File on FTP with file name [" + filename + "] does not match by name" )
                    else:
                        #Файл подошел - копируем его в папку для последующей обработки
                        try:
                            with open('tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) + '/' + filename, "wb") as target_file:
                                ftp.retrbinary('RETR ' + filename, target_file.write)
                                self.item.other_messages.append( "File downloaded from FTP and has file name [" + filename + "]" )
                        except Exception as err:
                            self.item.other_messages.append( "Failed to download file from FTP [" + filename + "]" )
                    continue #К следующему файлу
                else:
                    #Это АРХИВ
                    #Проверяем, подходит ли он по имени
                    if not self.is_file_suitable_by_substring_arch(filename):
                        self.item.other_messages.append( "File on FTP with file name [" + filename + "] does not match by name" )
                        continue #К следующему файлу
                    else:
                        #Архив подошел по имени, скачиваем
                        try:
                            with open('tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) + '/' + filename, "wb") as target_file:
                                ftp.retrbinary('RETR ' + filename, target_file.write)
                                self.item.other_messages.append( "DOWNLOADED file (archive) from FTP with the name [" + filename + "]" )
                        except Exception as err:
                            self.item.other_messages.append( "Failed to download file (archive) from FTP [" + filename + "]. Error 1" )
                            continue #К следующему файлу
                        
                        
                        #Архив скачан
                        #Распаковка архива
                        
                        #Проверяем наличие файла после его скачивания
                        if not Path('tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) + '/' + filename).is_file():
                            self.item.other_messages.append( "Failed to download file (archive) from FTP [" + filename + "]. Error 2" )
                            continue #К следующему файлу
                        
                        #Распаковываем архив (распаковка архива, удаление архива после распаковки)
                        self.extract_archive(filename)
                        
                        #ЗДЕСЬ БУДЕТ ЦИКЛ ПО РАСПАКОВАННЫМ ФАЙЛАМ
                        
                        #Получаем список файлов в директории (через генераторное выражение)
                        item_path = 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id)
                        unpacked_files_list = [f for f in listdir(item_path) if isfile(join(item_path, f))]
                        
                        #Цикл по распакованным файлам
                        for file_name_in_arch in unpacked_files_list:
                            
                            #Проверяем тип файла
                            file_extension_unp = self.get_file_extension(file_name_in_arch)
                            if file_extension_unp not in set( self.extensions_suitable ):
                                #Добавляем сообщение в объект задания
                                self.item.other_messages.append( 'Extracted file [' + str(file_name_in_arch) + '] has wrong type. Accepted file types: ' + ', '.join(self.extensions_suitable) )
                                #Удаляем неподходящий файл
                                self.del_file_from_tmp(file_name_in_arch)
                                #Переходим к следующему РАСПАКОВАННОМУ файлу
                                continue
                            
                            #Тип файла подходящий. Проверяем по подстроке
                            #Проверяем, подходит ли по фильтру имени файла
                            if not self.is_file_suitable_by_substring(file_name_in_arch):
                                #Добавляем сообщение в объект задания
                                self.item.other_messages.append( 'Extracted file [' + str(file_name_in_arch) + '] does not match the substring in the file name: ' + str(self.item.file_name_substring) )
                                #Удаляем неподходящий файл
                                self.del_file_from_tmp(file_name_in_arch)
                                #Переходим к следующему РАСПАКОВАННОМУ файлу
                                continue
                            
                            
                            #Файл прошел проверки и остается в tmp/launch_id/<price_id> для последующего импорта. Добавляем сообщение в объект задания
                            self.item.other_messages.append( 'Extracted file "' + str(file_name_in_arch) + '" has SUCCESSFULLY passed the checks and remains for further processing' )


    # --------------------------------------------------------------

# --------------------------------------------------------------
# --------------------------------------------------------------


#Класс для получения файлов с E-mail
class file_receiver_email(file_receiver):
    
    # --------------------------------------------------------------
    
    #Переопределенный конструктор. Инициализируеет свое поле "объекты заданий для одного отправителя"
    def __init__(self, items, imap_client, launch_id):
        self.items = items #Массив (список) с заданиями
        self.email_price_sender = self.items[0].email_price_sender #Адрес отправителя, от которого получать письма
        self.imap_client = imap_client #Почтовый клиент
        self.launch_id = launch_id
    
    # --------------------------------------------------------------
    
    #Метод добавления сообщения в список other_messages для каждого объекта заданий
    def append_other_message_to_all_items(self, message):
        
        for item in self.items:
            item.other_messages.append(message)
    
    # --------------------------------------------------------------
    
    #Метод проверки применимости для текущего задания (self.item) файла из письма по подстроке в заголовке письма
    def is_file_suitable_by_message_header_substring(self, message_subject ):
        
        #В объекте задания подстрока не задана - заголовок письма можно не проверять - он подходит
        if self.item.email_message_header_substring is None:
            self.item.other_messages.append('Filter by email subject is not specified')
            return True
        else:
            self.item.other_messages.append('Filter by email subject is SET: ' + self.item.email_message_header_substring)
            if type(self.item.email_message_header_substring) is str:
                self.item.other_messages.append('This is a string...')
        
        
        #Проверяем вхождение подстроки в заголовке письма
        if message_subject.find( str(self.item.email_message_header_substring) ) == -1:
            return False
        else:
            return True
    
    # --------------------------------------------------------------
    
    #Метод получений файлов для заданий с одинаковым отправителем
    def get_files_for_tasks(self):
        
        #Создаем папки для всех заданий группы (tmp/launch_id/<price_id>)
        for item in self.items:
            
            #Для возможности использования методов родительского класса, присваиваем self.item данное задание (для этой итерации)
            self.item = item
            
            #Через метод родительского класса создаем папку tmp/launch_id/<price_id>
            try:
                self.create_item_folder()
            except Exception as err:
                raise Exception(err)
                return
        
        
        #Получаем список новых писем от отправителя для группы заданий
        messages = self.imap_client.get_messages_by_sender(self.email_price_sender, not self.items[0].not_mark_seen_email_messages)
        
        #Если новых писем от данного отправителя нет
        if len(messages) == 0:
            self.append_other_message_to_all_items("No new messages from " + str(self.email_price_sender) )
            return
        
        
        #print("Получили письма<br>")
        
        #Цикл по списку писем
        for message in messages:
            
            #В каждый объект задания добавляем сообщение о письме с которым работаем
            self.append_other_message_to_all_items("Processing a letter from " + str(message['from_']) + " received " + str(message['date']))
            
            
            #Если вложений в письме не оказалось
            if len(message['attachments']) == 0:
                #Добавляем в каждое задание, сообщение
                self.append_other_message_to_all_items("The letter without attachments")
                continue #К следующему письму
            
            
            #Цикл по списку имен вложенных файлов в письме
            for filename in message['attachments']:
                
                #Для каждого объекта указываем имя файла, с которым работаем
                self.append_other_message_to_all_items("A file was found in the letter: " + str(filename) )
                
                #Проверяем тип файла
                file_extension = self.get_file_extension(filename)
                if file_extension not in set( self.extensions_suitable_arch ) and file_extension not in set( self.extensions_suitable ):
                    #Добавляем в каждое задание, сообщение
                    self.append_other_message_to_all_items("This file has incorrect format " + file_extension )
                    continue #К следующему файлу письма
                
                
                #Файл подходит по типу
                #Цикл по заданиям (объектам) группы
                for item in self.items:
                    
                    #Для возможности использования методов родительского класса, указываем объект в self.item на время данной итерации
                    self.item = item
                    
                    #Проверям, подходит ли данный файл по подстроке в теме письма
                    if not self.is_file_suitable_by_message_header_substring(message['subject']):
                        self.item.other_messages.append('The file does not match the subject of the letter. Letter subject: [' + str(message['subject'])+ '], filter in price list settings: [' + str(self.item.email_message_header_substring) + ']' )
                        continue #К следующему заданию группы
                    
                    
                    #Если это НЕ архив
                    if file_extension in set( self.extensions_suitable ):
                        #Проверяем, подходит ли по фильтру имени файла
                        if not self.is_file_suitable_by_substring(filename):
                            self.item.other_messages.append( "File (not archive) named [" + filename + "] does not match the name" )
                        else:
                            #Файл подошел - копируем его в папку текущего задания (tmp/launch_id/<price_id>) для последующей обработки
                            self.item.other_messages.append( "File named [" + filename + "] MATCH AND TO BE DOWNLOADED FROM E-MAIL" )
                            
                            #Скачиваем файл в tmp/launch_id/price_id
                            if not self.imap_client.download_file_from_message( message['uid'] , filename , 'tmp/' + str(self.launch_id) + '/' + str(item.price_id) + '/', not item.not_mark_seen_email_messages ):
                                self.item.other_messages.append("Failed to download file [" + filename + "] from message")
                            else:
                                self.item.other_messages.append("File [" + filename + "] sucessfully downloaded from the message")
                            
                        continue #К следующему заданию группы
                    else:
                        #ЭТО АРХИВ.
                        #Проверяем, подходит ли по наименованию
                        if not self.is_file_suitable_by_substring_arch(filename):
                            self.item.other_messages.append( "File (archive) named [" + filename + "] does not match the name" )
                            continue #К следующему заданию (объекту) группы
                        
                        #Подходит по имени, скачиваем
                        #Скачиваем файл в tmp/launch_id/price_id
                        if not self.imap_client.download_file_from_message( message['uid'] , filename , 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) + '/', not self.item.not_mark_seen_email_messages ):
                            self.item.other_messages.append("Failed to download file [" + filename + "] from message")
                        else:
                            self.item.other_messages.append("File [" + filename + "] sucessfully downloaded from the message")
                        
                        
                        #Архив скачан
                        self.item.other_messages.append("Trying to extract")
                        #Распаковка архива
                        
                        #Проверяем наличие файла после его скачивания
                        if not Path('tmp/' + str(self.launch_id) + '/' + str(self.item.price_id) + '/' + filename).is_file():
                            self.item.other_messages.append( "Failed to download file from E-mail (archive) [" + filename + "]" )
                            continue #К следующему заданию (объекту) группы
                        
                        #Распаковываем архив (распаковка архива, удаление архива после распаковки)
                        self.extract_archive(filename)
                        
                        #ЗДЕСЬ БУДЕТ ЦИКЛ ПО РАСПАКОВАННЫМ ФАЙЛАМ
                        
                        #Получаем список файлов в директории (через генераторное выражение)
                        item_path = 'tmp/' + str(self.launch_id) + '/' + str(self.item.price_id)
                        unpacked_files_list = [f for f in listdir(item_path) if isfile(join(item_path, f))]
                        
                        #Цикл по распакованным файлам
                        for file_name_in_arch in unpacked_files_list:
                            
                            #Проверяем тип файла
                            file_extension_unp = self.get_file_extension(file_name_in_arch)
                            if file_extension_unp not in set( self.extensions_suitable ):
                                #Добавляем сообщение в объект задания
                                self.item.other_messages.append( 'Extracted file [' + str(file_name_in_arch) + '] has wrong type. Accepted file types: ' + ', '.join(self.extensions_suitable) )
                                #Удаляем неподходящий файл
                                self.del_file_from_tmp(file_name_in_arch)
                                #Переходим к следующему РАСПАКОВАННОМУ файлу
                                continue
                            
                            #Тип файла подходящий. Проверяем по подстроке
                            #Проверяем, подходит ли по фильтру имени файла
                            if not self.is_file_suitable_by_substring(file_name_in_arch):
                                #Добавляем сообщение в объект задания
                                self.item.other_messages.append( 'Extracted file [' + str(file_name_in_arch) + '] does not match the substring in the file name: ' + str(self.item.file_name_substring) )
                                #Удаляем неподходящий файл
                                self.del_file_from_tmp(file_name_in_arch)
                                #Переходим к следующему РАСПАКОВАННОМУ файлу
                                continue
                            
                            
                            #Файл прошел проверки и остается в tmp/launch_id/<price_id> для последующего импорта. Добавляем сообщение в объект задания
                            self.item.other_messages.append( 'Extracted file [' + str(file_name_in_arch) + '] SUCCESSFULLY passed the checks and remains for further processing' )
        
    # --------------------------------------------------------------


# --------------------------------------------------------------
# --------------------------------------------------------------