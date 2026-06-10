<?php
header('Content-Type: application/json;charset=utf-8;');
http_response_code(403);
exit(json_encode(array(
	'status' => false,
	'message' => 'License API disabled for security. Create license/license.lic manually on server if required.',
)));
