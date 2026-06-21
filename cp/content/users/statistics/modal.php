<?php
    defined('_ASTEXE_') or die('No access');

    if ($customer_id > 0) :

?>
    <button class="btn btn-xs btn-info btn-circle" onclick="showCustomerModalInfo(<?php echo $customer_id; ?>)">
        <i class="fa fa-info"></i>
    </button>
    <div class="customer-modal-info-wrapper" id="customer-modal-info-<?php echo $customer_id; ?>" onclick="closeCustomerModalInfo(<?php echo $customer_id; ?>)">
    </div>
<?php
    endif;
?>