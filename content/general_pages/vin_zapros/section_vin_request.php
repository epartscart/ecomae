
<!-- Стили секции -->
<style>
    .section-vin {
        -webkit-box-sizing: border-box;
                box-sizing: border-box;
        background: transparent;
        margin: 0;
        padding: 14px 0;
        color: #FFF;
    }
    .section-vin > .container {
        background-color: #2E2E2E;
        border: 1px solid rgba(255, 255, 255, .08);
        border-radius: 18px;
        box-shadow: 0 16px 42px rgba(15, 23, 42, .18);
        padding: 22px 24px;
    }
    .section-vin__main {
        display: -webkit-box;
        display: -ms-flexbox;
        display: flex;
        -webkit-box-align: center;
            -ms-flex-align: center;
                align-items: center;
    }
    .section-vin__main img {
        max-width: 100px;
        height: 70px;
        -o-object-fit: contain;
        object-fit: contain;
        margin-right: 60px;
        -ms-flex-negative: 0;
            flex-shrink: 0;
    }
    .section-vin__info {
        margin-right: 40px;
    }
    .section-vin__btn {
        -webkit-box-flex: 0;
            -ms-flex: 0 0 300px;
                flex: 0 0 300px;
    }
    .section-vin__main h2 {
        font-weight: 500;
        margin-bottom: 10px;
        margin-top: 0px;
        color: #FFF;
        font-style: normal;
        line-height: normal;
    }
    .section-vin__main p {
        font-size: 14px;
        font-weight: 400;
        margin: 0px;
        font-style: normal;
        line-height: 1.4;
    }
    .section-vin__btn .btn {
        padding: 12px;
        font-size: 18px;
    }

    @media (max-width: 992px) {
        .section-vin__main {
            -ms-flex-wrap: wrap;
                flex-wrap: wrap;
        }
        .section-vin__main img {
            display: none;
        }
        .section-vin__info {
            text-align: center;
            margin: 0;
            margin-bottom: 24px;
        }
        .section-vin__btn {
            -ms-flex-preferred-size: 100%;
                flex-basis: 100%;
            text-align: center;
        }
    }
    @media (max-width: 320px) {
        .section-vin__btn  .btn {
            font-size: 18px;
        }
    }
</style>

<!-- Верстка секции -->
<div class="section-vin">
    <div class="container">
        <div class="section-vin__content">
            <div class="section-vin__main">
                <img src="/content/general_pages/vin_zapros/email.png" alt="">
                <div class="section-vin__info">
                    <h2><?php echo translate_str_by_id(5598); ?></h2>
                    <p><?php echo translate_str_by_id(5599); ?></p>
                </div>
                <div class="section-vin__btn">
                    <a href="<?php echo $multilang_params['lang_href']; ?>/zapros-prodavczu" class="btn btn-ar btn-primary"><?php echo translate_str_by_id(4800); ?></a>
                </div>
            </div>
        </div>
    </div>
</div>