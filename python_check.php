<?php
echo "<pre>";
print_r(shell_exec("python --version 2>&1"));
print_r(shell_exec("where python 2>&1"));  // Windows equivalent of 'which'
echo "</pre>";
?>