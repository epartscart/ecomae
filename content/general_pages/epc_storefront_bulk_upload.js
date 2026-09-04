/**
 * Storefront Excel / CSV bulk upload — PHP ajax_process UX on ASP.NET endpoints.
 * Static page: no Blazor interactivity. Form posts via fetch.
 */
(function () {
    var root = document.getElementById('epc-bulk-wrap');
    if (!root) { return; }

    var checkUrl = root.getAttribute('data-check-url') || '/storefront/bulk-upload/check';
    var crossUrl = root.getAttribute('data-cross-url') || '/storefront/bulk-upload/cross';
    var addUrl = root.getAttribute('data-add-url') || '/storefront/bulk-upload/add-selected';
    var cartUrl = root.getAttribute('data-cart-url') || '/storefront/cart-app';
    var maxBytes = 8 * 1024 * 1024;

    var bulkResults = [];
    var lastCsv = '';
    var currentUploadId = 0;
    var bulkCrossRunning = false;

    function $(id) { return document.getElementById(id); }
    function esc(v) {
        return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
            return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#039;' }[c];
        });
    }
    function money(v) {
        var n = Number(v || 0);
        return n.toFixed(2);
    }
    function showBanner(text, ok) {
        var box = $('epc_bulk_banner');
        if (!box) { return; }
        box.hidden = !text;
        box.className = 'epc-bulk-banner' + (ok ? ' epc-bulk-banner--ok' : ' epc-bulk-banner--err');
        box.textContent = text || '';
    }
    function applyFilter() {
        var filterEl = $('epc_bulk_filter');
        var filter = filterEl ? filterEl.value : 'all';
        document.querySelectorAll('.epc-bulk-result').forEach(function (el) {
            var show = filter === 'all' || el.getAttribute('data-filter-' + filter) === '1';
            el.style.display = show ? '' : 'none';
        });
    }
    function selectedOption(row) {
        if (row.cross && (!row.exact || Number(row.exact.exist || 0) < Number(row.input.qty || 1))) {
            return row.cross;
        }
        return row.exact || row.cross || null;
    }
    function recalcSummary() {
        var s = { uploaded: bulkResults.length, available: 0, cross: 0, short: 0, notfound: 0 };
        bulkResults.forEach(function (row) {
            if (row.available) { s.available++; } else { s.notfound++; }
            if (row.cross_found) { s.cross++; }
            if (row.short_qty) { s.short++; }
        });
        return s;
    }
    function setProcessProgress(percent, message) {
        var box = $('epc_bulk_process_progress');
        var text = $('epc_bulk_process_progress_text');
        var bar = $('epc_bulk_process_progress_bar');
        percent = Math.max(0, Math.min(100, Math.round(percent)));
        if (!box || !text || !bar) { return; }
        box.className = box.className.replace(/\bis-waiting\b/g, '');
        box.style.display = 'block';
        text.textContent = (message || 'Processing file and checking availability') + ': ' + percent + '%';
        bar.style.width = percent + '%';
    }
    function setProcessWaiting(percent, elapsedSeconds) {
        var box = $('epc_bulk_process_progress');
        var text = $('epc_bulk_process_progress_text');
        var bar = $('epc_bulk_process_progress_bar');
        percent = Math.max(95, Math.min(99, Math.round(percent)));
        if (!box || !text || !bar) { return; }
        if ((' ' + box.className + ' ').indexOf(' is-waiting ') === -1) { box.className += ' is-waiting'; }
        box.style.display = 'block';
        text.textContent = 'Checking warehouse offers: ' + percent + '% | Finalizing result table | Elapsed ' + elapsedSeconds + 's';
        bar.style.width = percent + '%';
    }
    function hideProcessProgress() {
        var box = $('epc_bulk_process_progress');
        var bar = $('epc_bulk_process_progress_bar');
        if (box) {
            box.className = box.className.replace(/\bis-waiting\b/g, '');
            box.style.display = 'none';
        }
        if (bar) { bar.style.width = '0%'; }
    }
    function setCrossProgress(done, total, active) {
        var percent = total > 0 ? Math.round((done / total) * 100) : 0;
        var pending = Math.max(0, total - done);
        var box = $('epc_bulk_cross_progress');
        var text = $('epc_bulk_cross_progress_text');
        var bar = $('epc_bulk_cross_progress_bar');
        if (!box || !text || !bar) { return; }
        box.style.display = 'block';
        text.textContent = 'Cross availability progress: ' + percent + '% complete | Completed ' + done + ' of ' + total + ' | Pending ' + pending + (active ? ' | Checking ' + active + ' rows now' : '');
        bar.style.width = percent + '%';
    }
    function hideCrossProgress() {
        var box = $('epc_bulk_cross_progress');
        var bar = $('epc_bulk_cross_progress_bar');
        if (box) { box.style.display = 'none'; }
        if (bar) { bar.style.width = '0%'; }
    }
    function render(data) {
        bulkResults = data.rows || [];
        lastCsv = data.csv || '';
        if (data.upload_id) { currentUploadId = Number(data.upload_id) || 0; }
        var summary = data.summary || {};
        var html = '<div class="epc-bulk-summary" aria-label="Upload result counts">' +
            '<div class="epc-bulk-stat"><span>Uploaded</span><strong>' + esc(summary.uploaded || 0) + '</strong></div>' +
            '<div class="epc-bulk-stat epc-bulk-stat--available"><span>Available</span><strong>' + esc(summary.available || 0) + '</strong></div>' +
            '<div class="epc-bulk-stat epc-bulk-stat--cross"><span>Cross found</span><strong>' + esc(summary.cross || 0) + '</strong></div>' +
            '<div class="epc-bulk-stat epc-bulk-stat--short"><span>Short qty</span><strong>' + esc(summary.short || 0) + '</strong></div>' +
            '<div class="epc-bulk-stat epc-bulk-stat--notfound"><span>Not found</span><strong>' + esc(summary.notfound || 0) + '</strong></div>' +
            '</div>';
        html += '<div class="epc-bulk-table-wrap"><table class="epc-bulk-table"><thead><tr>' +
            '<th>#</th><th>Add</th><th>Requested Brand</th><th>Requested Part</th><th>Need</th>' +
            '<th>Matched Brand</th><th>Matched Part</th><th>Name</th><th>Avail</th><th>Short</th>' +
            '<th>Price</th><th>Delivery</th><th>Match</th><th>Status / Cross</th></tr></thead><tbody>';
        bulkResults.forEach(function (row, idx) {
            var opt = selectedOption(row);
            var side = (opt && row.cross && opt === row.cross) ? 'cross' : (opt && row.exact ? 'exact' : '');
            var trClass = row.short_qty ? 'is-short' : (!row.available ? 'is-notfound' : (row.cross ? 'is-cross' : ''));
            var canFetchCross = (!row.available || row.short_qty) && !row.cross_checked;
            var actionHtml = canFetchCross
                ? (bulkCrossRunning
                    ? '<span class="epc-bulk-muted">Queued for cross check</span>'
                    : '<button class="btn btn-xs btn-info epc-bulk-fetch-cross" type="button" data-row="' + idx + '">Fetch cross availability</button>')
                : esc(row.status_label);
            var checkbox = opt ? '<input type="checkbox" class="epc-bulk-select" data-row="' + idx + '" data-side="' + side + '" checked>' : '';
            html += '<tr class="epc-bulk-result ' + trClass + '" data-filter-available="' + (row.available ? '1' : '0') + '" data-filter-short="' + (row.short_qty ? '1' : '0') + '" data-filter-cross="' + (row.cross_found ? '1' : '0') + '" data-filter-notfound="' + (!row.available ? '1' : '0') + '">';
            html += '<td class="epc-bulk-num">' + (idx + 1) + '</td>';
            html += '<td>' + checkbox + '</td>';
            html += '<td>' + esc((row.input && row.input.brand) || 'Any') + '</td>';
            html += '<td class="epc-bulk-part">' + esc(row.input ? row.input.article : '') + '</td>';
            html += '<td class="epc-bulk-num">' + esc(row.input ? row.input.qty : '') + '</td>';
            html += '<td>' + esc(opt ? opt.manufacturer : '-') + '</td>';
            html += '<td class="epc-bulk-part">' + esc(opt ? (opt.article_show || opt.article) : '-') + '</td>';
            html += '<td class="epc-bulk-name" title="' + esc(opt ? opt.name : '') + '">' + esc(opt ? opt.name : '-') + '</td>';
            html += '<td class="epc-bulk-num">' + esc(opt ? opt.exist : '-') + '</td>';
            html += '<td class="epc-bulk-num">' + (row.short_qty && opt ? esc(Math.max(0, Number(row.input.qty || 0) - Number(opt.exist || 0))) : '0') + '</td>';
            html += '<td class="epc-bulk-num">' + (opt ? money(opt.price) : '-') + '</td>';
            html += '<td class="epc-bulk-num">' + (opt ? esc(opt.time_to_exe) + ' d' : '-') + '</td>';
            html += '<td>' + (opt ? '<span class="epc-bulk-badge ' + (opt.match_type === 'exact' ? 'epc-bulk-badge--exact' : 'epc-bulk-badge--cross') + '">' + esc(opt.match_label) + '</span>' : '-') + '</td>';
            html += '<td class="epc-bulk-actions-cell">' + actionHtml + '</td>';
            html += '</tr>';
        });
        html += '</tbody></table></div>';
        var target = $('epc_bulk_results');
        if (target) { target.innerHTML = html; }
        var addBtn = $('epc_bulk_add_selected');
        var fetchBtn = $('epc_bulk_fetch_all_cross');
        var dlBtn = $('epc_bulk_download');
        if (addBtn) { addBtn.disabled = (summary.available || 0) <= 0; }
        if (fetchBtn) {
            fetchBtn.disabled = bulkCrossRunning || bulkResults.filter(function (row) {
                return (!row.available || row.short_qty) && !row.cross_checked;
            }).length === 0;
        }
        if (dlBtn) { dlBtn.disabled = !lastCsv; }
        applyFilter();
    }

    function validateFile(file) {
        if (!file) { return 'Choose an Excel or CSV file.'; }
        var name = (file.name || '').toLowerCase();
        if (!/\.(xlsx|xls|csv|txt)$/.test(name)) {
            return 'Use .xlsx, .xls, .csv, or .txt.';
        }
        if (file.size > maxBytes) {
            return 'File is larger than 8 MB. Split the list or save as CSV.';
        }
        return '';
    }

    var form = $('epc_bulk_form');
    if (form) {
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            var fileInput = form.querySelector('input[name="bulk_file"]');
            var file = fileInput && fileInput.files ? fileInput.files[0] : null;
            var fileError = validateFile(file);
            if (fileError) {
                showBanner(fileError, false);
                return;
            }
            showBanner('', true);
            hideCrossProgress();
            hideProcessProgress();
            var loading = $('epc_bulk_loading');
            if (loading) { loading.style.display = 'block'; }
            setProcessProgress(0, 'Starting upload');
            var progressPercent = 0;
            var elapsedSeconds = 0;
            var progressTimer = window.setInterval(function () {
                elapsedSeconds++;
                if (progressPercent < 95) {
                    progressPercent += progressPercent < 35 ? 3 : 1;
                    setProcessProgress(progressPercent, progressPercent < 35 ? 'Uploading file' : 'Checking availability');
                } else {
                    setProcessWaiting(95 + (elapsedSeconds % 5), elapsedSeconds);
                }
            }, 1000);
            var data = new FormData(form);
            var xhr = new XMLHttpRequest();
            xhr.open('POST', checkUrl, true);
            xhr.withCredentials = true;
            xhr.upload.onprogress = function (ev) {
                if (ev.lengthComputable) {
                    progressPercent = Math.min(35, Math.round((ev.loaded / ev.total) * 35));
                    setProcessProgress(progressPercent, 'Uploading file');
                }
            };
            xhr.onload = function () {
                window.clearInterval(progressTimer);
                if (loading) { loading.style.display = 'none'; }
                try {
                    var r = JSON.parse(xhr.responseText || '{}');
                    if (!r.status) {
                        hideProcessProgress();
                        showBanner(r.message || 'Upload error', false);
                        return;
                    }
                    setProcessProgress(100, 'Processing complete');
                    render(r);
                    showBanner('Checked ' + ((r.summary && r.summary.uploaded) || 0) + ' lines. Review matches, fetch crosses, then add selected parts to cart.', true);
                } catch (err) {
                    hideProcessProgress();
                    showBanner('Upload error. Sign in and try again.', false);
                }
            };
            xhr.onerror = function () {
                window.clearInterval(progressTimer);
                hideProcessProgress();
                if (loading) { loading.style.display = 'none'; }
                showBanner('Upload error. Check the connection and try again.', false);
            };
            xhr.send(data);
        });
    }

    var filter = $('epc_bulk_filter');
    if (filter) { filter.addEventListener('change', applyFilter); }

    function fetchCrossForRow(idx) {
        var row = bulkResults[idx];
        if (!row || row.cross_checked || (row.available && !row.short_qty)) {
            return Promise.resolve(false);
        }
        var body = new URLSearchParams({
            priority: ($('epc_bulk_priority') || {}).value || 'price',
            article: (row.input && row.input.article) || '',
            qty: (row.input && row.input.qty) || 1
        });
        return fetch(crossUrl, {
            method: 'POST',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8' },
            body: body.toString(),
            credentials: 'same-origin'
        }).then(function (r) { return r.json(); }).then(function (r) {
            if (!r.status) { return false; }
            var hadExactBefore = !!row.exact;
            var relatedByPartNumber = null;
            if (!hadExactBefore && r.exact) {
                relatedByPartNumber = r.exact;
                relatedByPartNumber.match_type = 'cross';
                relatedByPartNumber.match_label = 'Related';
            }
            row.cross_checked = true;
            row.cross = r.cross || row.cross || relatedByPartNumber || null;
            if (!hadExactBefore && !relatedByPartNumber) {
                row.exact = r.exact || null;
            }
            row.cross_found = !!row.cross;
            row.available = !!(row.exact || row.cross);
            var selected = selectedOption(row);
            row.short_qty = selected ? Number(selected.exist) < Number(row.input.qty || 1) : false;
            row.status_label = row.available ? (row.short_qty ? 'Available but short quantity' : 'Available') : 'No cross availability found';
            return true;
        }).catch(function () { return false; });
    }

    var results = $('epc_bulk_results');
    if (results) {
        results.addEventListener('click', function (e) {
            var btn = e.target.closest('.epc-bulk-fetch-cross');
            if (!btn) { return; }
            var idx = Number(btn.getAttribute('data-row'));
            var row = bulkResults[idx];
            if (!row) { return; }
            btn.disabled = true;
            btn.textContent = 'Checking...';
            fetchCrossForRow(idx).then(function (ok) {
                if (!ok) { showBanner('Cross check error or no cross result.', false); }
                render({ rows: bulkResults, summary: recalcSummary(), csv: lastCsv, upload_id: currentUploadId });
            }).finally(function () {
                btn.disabled = false;
                btn.textContent = 'Fetch cross availability';
            });
        });
    }

    var fetchAll = $('epc_bulk_fetch_all_cross');
    if (fetchAll) {
        fetchAll.addEventListener('click', function () {
            var btn = this;
            var indexes = [];
            bulkResults.forEach(function (row, idx) {
                if ((!row.available || row.short_qty) && !row.cross_checked) {
                    indexes.push(idx);
                }
            });
            if (indexes.length === 0) {
                showBanner('No not-found or short-quantity rows need cross checking.', false);
                return;
            }
            btn.disabled = true;
            var originalText = btn.innerHTML;
            var done = 0;
            var active = 0;
            var cursor = 0;
            var concurrency = Math.min(4, indexes.length);
            bulkCrossRunning = true;
            setCrossProgress(0, indexes.length, 0);
            render({ rows: bulkResults, summary: recalcSummary(), csv: lastCsv, upload_id: currentUploadId });
            btn = $('epc_bulk_fetch_all_cross') || btn;
            function finishIfDone() {
                if (done >= indexes.length && active === 0) {
                    bulkCrossRunning = false;
                    btn = $('epc_bulk_fetch_all_cross') || btn;
                    if (btn) { btn.innerHTML = originalText; }
                    setCrossProgress(indexes.length, indexes.length, 0);
                    render({ rows: bulkResults, summary: recalcSummary(), csv: lastCsv, upload_id: currentUploadId });
                    return;
                }
                startWorkers();
            }
            function startWorkers() {
                while (active < concurrency && cursor < indexes.length) {
                    active++;
                    var rowNumber = cursor + 1;
                    var idx = indexes[cursor++];
                    var percent = Math.round((done / indexes.length) * 100);
                    btn = $('epc_bulk_fetch_all_cross') || btn;
                    if (btn) { btn.innerHTML = 'Checking cross ' + rowNumber + ' / ' + indexes.length + ' (' + percent + '%)...'; }
                    setCrossProgress(done, indexes.length, active);
                    fetchCrossForRow(idx).then(function () {
                        done++;
                        active--;
                        setCrossProgress(done, indexes.length, active);
                        if (done % 8 === 0 || done === indexes.length) {
                            render({ rows: bulkResults, summary: recalcSummary(), csv: lastCsv, upload_id: currentUploadId });
                        }
                        finishIfDone();
                    });
                }
            }
            startWorkers();
        });
    }

    var addSelected = $('epc_bulk_add_selected');
    if (addSelected) {
        addSelected.addEventListener('click', function () {
            var selected = [];
            document.querySelectorAll('.epc-bulk-select:checked').forEach(function (chk) {
                var row = bulkResults[Number(chk.getAttribute('data-row'))];
                var side = chk.getAttribute('data-side');
                if (row && row[side] && row[side].product_object) {
                    selected.push(row[side].product_object);
                }
            });
            if (selected.length === 0) {
                showBanner('Select at least one available item.', false);
                return;
            }
            addSelected.disabled = true;
            fetch(addUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json; charset=UTF-8' },
                credentials: 'same-origin',
                body: JSON.stringify({ items: selected, confirmWrites: true })
            }).then(function (r) { return r.json(); }).then(function (r) {
                if (r.status) {
                    showBanner((r.message || 'Items added to cart.') + ' Opening cart…', true);
                    window.setTimeout(function () { window.location.href = cartUrl; }, 400);
                } else {
                    showBanner(r.message || 'Some items were not added. They may already be in cart.', false);
                }
            }).catch(function () {
                showBanner('Add to cart error.', false);
            }).finally(function () {
                addSelected.disabled = false;
            });
        });
    }

    var download = $('epc_bulk_download');
    if (download) {
        download.addEventListener('click', function () {
            if (!lastCsv) { return; }
            var blob = new Blob([lastCsv], { type: 'text/csv;charset=utf-8;' });
            var url = URL.createObjectURL(blob);
            var a = document.createElement('a');
            a.href = url;
            a.download = 'bulk-upload-results.csv';
            a.click();
            URL.revokeObjectURL(url);
        });
    }

    var fileInput = form ? form.querySelector('input[name="bulk_file"]') : null;
    var fileHint = $('epc_bulk_file_hint');
    if (fileInput && fileHint) {
        fileInput.addEventListener('change', function () {
            var file = fileInput.files && fileInput.files[0];
            if (!file) {
                fileHint.textContent = 'No file selected.';
                return;
            }
            var err = validateFile(file);
            fileHint.textContent = err || (file.name + ' · ' + Math.max(1, Math.round(file.size / 1024)) + ' KB');
            if (err) { showBanner(err, false); } else { showBanner('', true); }
        });
    }
})();
