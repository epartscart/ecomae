<?php

/**
 * Скрипт перехода на страницу оплаты alfabank
 *
 * SOAP - Для отправки запросов в банк
 *
**/

class Gateway extends SoapClient 
{
	// Авторизация
	public $login = '';
	public $password = '';
	private function generateWSSecurityHeader() 
	{
        $xml = '
            <wsse:Security SOAP-ENV:mustUnderstand="1" xmlns:wsse="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd">
                <wsse:UsernameToken>
                    <wsse:Username>' . $this->login . '</wsse:Username>
                    <wsse:Password Type="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText">' . $this->password . '</wsse:Password>
                    <wsse:Nonce EncodingType="http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary">' . sha1(mt_rand()) . '</wsse:Nonce>
                </wsse:UsernameToken>
            </wsse:Security>';
         
        return new SoapHeader('http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd', 'Security', new SoapVar($xml, XSD_ANYXML), true);
    }
	
	// Отправка запроса в банк
	public function __call($method, $data) 
	{
        $this->__setSoapHeaders($this->generateWSSecurityHeader()); // Устанавливаем заголовок для авторизации
        return parent::__call($method, $data); // Возвращаем результат метода SoapClient::__call()
    }
}

?>