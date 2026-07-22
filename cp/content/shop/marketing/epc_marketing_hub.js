/**
 * Marketing Growth Hub — CP interactions
 * Posts to the page URL (marketing_main.php → ajax_marketing.php).
 */
(function () {
  'use strict';

  function ready(fn) {
    if (document.readyState === 'loading') {
      document.addEventListener('DOMContentLoaded', fn);
    } else {
      fn();
    }
  }

  function root() {
    return document.getElementById('epcMktHub');
  }

  function toast(msg, isError) {
    var el = document.getElementById('epcMktToast');
    if (!el) return;
    el.textContent = msg || '';
    el.className = 'epc-mkt-toast' + (isError ? ' is-error' : ' is-ok');
    el.hidden = false;
    clearTimeout(el._t);
    el._t = setTimeout(function () {
      el.hidden = true;
    }, 2800);
  }

  function postUrl() {
    var el = root();
    return (el && el.getAttribute('data-post-url')) ||
      '/cp/content/shop/marketing/ajax_marketing_endpoint.php';
  }

  function csrf() {
    var el = root();
    return (el && el.getAttribute('data-csrf')) || '';
  }

  function post(action, data) {
    var body = new FormData();
    body.append('action', action);
    var token = csrf();
    if (token) body.append('csrf_guard_key', token);
    Object.keys(data || {}).forEach(function (k) {
      body.append(k, data[k] == null ? '' : String(data[k]));
    });
    return fetch(postUrl(), {
      method: 'POST',
      credentials: 'same-origin',
      body: body
    }).then(function (r) {
      return r.json();
    });
  }

  function setSubPanel(strategyEl, panelName) {
    if (!strategyEl) return;
    strategyEl.querySelectorAll('.epc-mkt-sub').forEach(function (btn) {
      var on = btn.getAttribute('data-panel') === panelName;
      btn.classList.toggle('is-active', on);
      btn.classList.toggle('btn-primary', on);
      btn.classList.toggle('btn-default', !on);
    });
    strategyEl.querySelectorAll('.epc-mkt-panel').forEach(function (p) {
      p.classList.toggle('active', p.getAttribute('data-panel') === panelName);
    });
  }

  function activateStrategy(id) {
    var hub = root();
    if (!hub) return;
    var overview = document.getElementById('epcMktOverview');
    var workbench = document.getElementById('epcMktWorkbench');
    var titleEl = document.getElementById('epcMktActiveTitle');
    var metaEl = document.getElementById('epcMktActiveMeta');

    hub.querySelectorAll('.epc-mkt-nav__list a[data-strategy]').forEach(function (a) {
      a.classList.toggle('is-active', a.getAttribute('data-strategy') === id);
    });

    hub.querySelectorAll('.epc-mkt-strategy[data-strategy]').forEach(function (panel) {
      panel.hidden = panel.getAttribute('data-strategy') !== id;
    });

    if (id === 'overview') {
      if (overview) overview.hidden = false;
      if (workbench) workbench.hidden = true;
      if (titleEl) titleEl.textContent = 'Growth overview';
      if (metaEl) metaEl.textContent = 'Channel health, cadence, and strategy progress';
      try {
        history.replaceState(null, '', '#overview');
      } catch (e) {}
      return;
    }

    if (overview) overview.hidden = true;
    if (workbench) workbench.hidden = false;
    var active = hub.querySelector('.epc-mkt-strategy[data-strategy="' + id + '"]');
    var nav = hub.querySelector('.epc-mkt-nav__list a[data-strategy="' + id + '"]');
    if (titleEl && nav) titleEl.textContent = nav.getAttribute('data-title') || id;
    if (metaEl && nav) metaEl.textContent = nav.getAttribute('data-meta') || '';
    if (active) setSubPanel(active, 'guide');
    try {
      history.replaceState(null, '', '#' + id);
    } catch (e2) {}
  }

  function bindNav() {
    var hub = root();
    if (!hub) return;
    hub.querySelectorAll('[data-strategy-goto]').forEach(function (el) {
      el.addEventListener('click', function (e) {
        e.preventDefault();
        activateStrategy(el.getAttribute('data-strategy-goto'));
      });
    });
    hub.querySelectorAll('.epc-mkt-nav__list a[data-strategy]').forEach(function (el) {
      el.addEventListener('click', function (e) {
        e.preventDefault();
        activateStrategy(el.getAttribute('data-strategy'));
      });
    });
  }

  function bindSubnav() {
    document.querySelectorAll('.epc-mkt-strategy').forEach(function (strategyEl) {
      strategyEl.querySelectorAll('.epc-mkt-sub').forEach(function (btn) {
        btn.addEventListener('click', function () {
          setSubPanel(strategyEl, btn.getAttribute('data-panel'));
        });
      });
    });
  }

  function updateProgressUi(completion) {
    if (!completion) return;
    var overall = document.getElementById('epcMktOverallPct');
    if (overall && completion.pct != null) overall.textContent = String(completion.pct) + '%';
    var doneEl = document.getElementById('epcMktOverallDone');
    if (doneEl && completion.done != null) doneEl.textContent = String(completion.done);
    var totalEl = document.getElementById('epcMktOverallTotal');
    if (totalEl && completion.total != null) totalEl.textContent = String(completion.total);
    var by = completion.by_strategy || {};
    Object.keys(by).forEach(function (key) {
      var bs = by[key] || {};
      var pct = bs.pct != null ? String(bs.pct) : null;
      if (!pct) return;
      var navMeta = document.querySelector('[data-nav-pct="' + key + '"]');
      if (navMeta) navMeta.textContent = pct + '%';
      var bar = document.querySelector('[data-progress-bar="' + key + '"]');
      if (bar) bar.style.width = pct + '%';
      var label = document.querySelector('[data-progress-label="' + key + '"]');
      if (label) label.textContent = pct + '%';
      var counts = document.querySelector('[data-progress-counts="' + key + '"]');
      if (counts && bs.done != null && bs.total != null) {
        counts.textContent = String(bs.done) + ' / ' + String(bs.total);
      }
    });
  }

  function bindTasks() {
    document.querySelectorAll('.epc-mkt-task-cb').forEach(function (cb) {
      cb.addEventListener('change', function () {
        var strategy = cb.getAttribute('data-strategy');
        var task = cb.getAttribute('data-task');
        var done = cb.checked ? '1' : '0';
        var row = cb.closest('.epc-mkt-task');
        if (row) row.classList.toggle('done', !!cb.checked);
        post('toggle_task', {
          strategy_key: strategy,
          task_key: task,
          is_done: done
        })
          .then(function (res) {
            if (!res || !res.status) throw new Error((res && res.message) || 'Save failed');
            toast(res.message || 'Task updated');
            updateProgressUi(res.completion);
          })
          .catch(function (err) {
            cb.checked = !cb.checked;
            if (row) row.classList.toggle('done', !!cb.checked);
            toast(err.message || 'Could not save task', true);
          });
      });
    });
  }

  function bindKpiForms() {
    document.querySelectorAll('form.epc-mkt-kpi-form').forEach(function (form) {
      form.addEventListener('submit', function (e) {
        e.preventDefault();
        var fd = new FormData(form);
        post('save_kpi', {
          strategy_key: fd.get('strategy_key'),
          kpi_key: fd.get('kpi_key'),
          value: fd.get('value'),
          note: fd.get('note') || ''
        })
          .then(function (res) {
            if (!res || !res.status) throw new Error((res && res.message) || 'Save failed');
            toast(res.message || 'KPI saved');
            var msg = form.querySelector('.epc-mkt-form-msg');
            if (msg) {
              msg.hidden = false;
              msg.textContent = res.message || 'KPI saved';
              msg.className = 'epc-mkt-form-msg is-ok';
            }
            setTimeout(function () {
              window.location.reload();
            }, 700);
          })
          .catch(function (err) {
            toast(err.message || 'Could not save KPI', true);
          });
      });
    });
  }

  function bindReviewForms() {
    document.querySelectorAll('form.epc-mkt-review-form').forEach(function (form) {
      form.addEventListener('submit', function (e) {
        e.preventDefault();
        var fd = new FormData(form);
        post('save_review', {
          strategy_key: fd.get('strategy_key'),
          review_type: fd.get('review_type'),
          score: fd.get('score'),
          notes: fd.get('notes') || ''
        })
          .then(function (res) {
            if (!res || !res.status) throw new Error((res && res.message) || 'Save failed');
            toast(res.message || 'Review saved');
            var notes = form.querySelector('[name="notes"]');
            if (notes) notes.value = '';
          })
          .catch(function (err) {
            toast(err.message || 'Could not save review', true);
          });
      });
    });
  }

  function bindSnapshot() {
    var btn = document.getElementById('epcMktSnapshot');
    if (!btn) return;
    btn.addEventListener('click', function () {
      btn.disabled = true;
      post('snapshot', {})
        .then(function (res) {
          if (!res || !res.status) throw new Error((res && res.message) || 'Snapshot failed');
          toast('Live snapshot refreshed');
        })
        .catch(function (err) {
          toast(err.message || 'Snapshot failed', true);
        })
        .finally(function () {
          btn.disabled = false;
        });
    });
  }

  ready(function () {
    if (!root()) return;
    bindNav();
    bindSubnav();
    bindTasks();
    bindKpiForms();
    bindReviewForms();
    bindSnapshot();

    var hash = (location.hash || '').replace(/^#/, '');
    var initial = root().getAttribute('data-initial') || 'overview';
    var candidate = hash || initial;
    var exists = document.querySelector('.epc-mkt-nav__list a[data-strategy="' + candidate + '"]');
    activateStrategy(exists ? candidate : 'overview');
  });
})();
