using System.Globalization;
using System.Net;
using System.Text;

namespace EcomAE.Platform.Presentation;

/// <summary>PHP <c>epc_ext_kv_table</c> / <c>epc_ext_field_guide</c> / <c>epc_ext_commentary</c> / <c>epc_ext_cover_page</c> / print JS twin.</summary>
public static class ErpExternalReportingHtml
{
    public static string H(string? value) => WebUtility.HtmlEncode(value ?? "");

    public static string Money(decimal amount, string ccy = "AED") =>
        ccy + " " + amount.ToString("N2", CultureInfo.InvariantCulture);

    public static string KvTable(IEnumerable<(string Label, string Value, bool Strong)> rows)
    {
        var sb = new StringBuilder();
        sb.Append("<table class=\"table table-bordered table-condensed\" style=\"max-width:760px;\">");
        foreach (var r in rows)
        {
            sb.Append(r.Strong ? "<tr style=\"font-weight:700;background:#f5f7fa;\">" : "<tr>");
            sb.Append("<td>").Append(H(r.Label)).Append("</td>");
            sb.Append("<td style=\"text-align:right;white-space:nowrap;\">").Append(H(r.Value)).Append("</td></tr>");
        }

        return sb.Append("</table>").ToString();
    }

    public static string FieldGuide(string title, string intro, IEnumerable<(string Field, string Why)> rows, bool open = false)
    {
        var sb = new StringBuilder();
        sb.Append("<details").Append(open ? " open" : "").Append(" style=\"border:1px solid #cfe0f5;background:#f5f9ff;border-radius:6px;margin:6px 0 16px;padding:6px 12px;\">");
        sb.Append("<summary style=\"cursor:pointer;font-weight:700;color:#1d4e89;\"><i class=\"fa fa-info-circle\"></i> ").Append(H(title)).Append("</summary>");
        sb.Append("<p class=\"text-muted\" style=\"margin:8px 0;\">").Append(H(intro)).Append("</p>");
        sb.Append("<table class=\"table table-bordered table-condensed\" style=\"background:#fff;font-size:12px;margin:0;\">");
        sb.Append("<thead><tr style=\"background:#e8f1fc;\"><th style=\"padding:6px 10px;\">Field / box</th><th style=\"padding:6px 10px;\">What goes here &amp; why</th></tr></thead><tbody>");
        foreach (var r in rows)
        {
            sb.Append("<tr><td style=\"padding:6px 10px;font-weight:600;color:#1d2740;white-space:nowrap;vertical-align:top;\">").Append(H(r.Field)).Append("</td>");
            sb.Append("<td style=\"padding:6px 10px;color:#333;\">").Append(H(r.Why)).Append("</td></tr>");
        }

        return sb.Append("</tbody></table></details>").ToString();
    }

    public static string Commentary(string title, IEnumerable<string> paras)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"mis-commentary\" style=\"background:#f7faff;border:1px solid #d8e3f4;border-left:4px solid #2b6cb0;border-radius:6px;padding:12px 16px;margin:10px 0 16px;line-height:1.6;color:#33415c;font-size:13px;\">");
        sb.Append("<h5 style=\"margin:0 0 8px;font-size:14px;color:#1d2740;\"><i class=\"fa fa-book\"></i> ").Append(H(title)).Append("</h5>");
        foreach (var p in paras)
        {
            sb.Append("<p style=\"margin:0 0 8px;\">").Append(p).Append("</p>");
        }

        return sb.Append("</div>").ToString();
    }

    public static string CoverPage(string entity, string title, string subtitle, IReadOnlyList<(string Label, string Value)> meta, IReadOnlyList<(string No, string Title)> toc)
    {
        var sb = new StringBuilder();
        sb.Append("<div class=\"ext-cover\" style=\"page-break-after:always;background-color:#b3122a;color:#fff;border-radius:10px;padding:40px 44px;margin:0 0 22px;\">");
        sb.Append("<div style=\"font-size:12px;letter-spacing:3px;text-transform:uppercase;color:#ffe2e6;margin-bottom:30px;\">").Append(H(subtitle)).Append("</div>");
        sb.Append("<div style=\"font-size:30px;font-weight:800;line-height:1.2;margin-bottom:6px;color:#fff;\">").Append(H(entity)).Append("</div>");
        sb.Append("<div style=\"font-size:20px;font-weight:600;color:#ffe2e6;margin-bottom:34px;\">").Append(H(title)).Append("</div>");
        sb.Append("<table style=\"border-collapse:collapse;border-top:1px solid rgba(255,255,255,.35);\">");
        foreach (var m in meta)
        {
            sb.Append("<tr><td style=\"padding:6px 14px;color:#ffe2e6;font-weight:600;border-bottom:1px solid rgba(255,255,255,.18);\">").Append(H(m.Label)).Append("</td>");
            sb.Append("<td style=\"padding:6px 14px;color:#fff;font-weight:700;border-bottom:1px solid rgba(255,255,255,.18);\">").Append(H(m.Value)).Append("</td></tr>");
        }

        sb.Append("</table></div>");
        sb.Append("<div class=\"ext-toc\" style=\"page-break-after:always;border:1px solid #e7c3c9;border-radius:8px;padding:18px 22px;margin:0 0 22px;max-width:680px;\">");
        sb.Append("<h4 style=\"margin:0 0 10px;color:#b3122a;border-bottom:2px solid #b3122a;padding-bottom:6px;\">Table of contents</h4>");
        sb.Append("<table style=\"border-collapse:collapse;width:100%;font-size:13px;\">");
        foreach (var t in toc)
        {
            sb.Append("<tr><td style=\"padding:5px 10px;color:#b3122a;font-weight:700;width:46px;\">").Append(H(t.No)).Append("</td>");
            sb.Append("<td style=\"padding:5px 10px;color:#1d2740;\">").Append(H(t.Title)).Append("</td></tr>");
        }

        return sb.Append("</table></div>").ToString();
    }

    public static string BarChart(string title, IReadOnlyList<(string Label, decimal Value, string Color)> bars)
    {
        var max = bars.Count == 0 ? 1m : Math.Max(1m, bars.Max(b => Math.Abs(b.Value)));
        var sb = new StringBuilder();
        sb.Append("<div class=\"epc-ext-bars\" style=\"margin:12px 0 18px;\">");
        sb.Append("<div style=\"font-weight:700;color:#1d2740;margin-bottom:8px;\">").Append(H(title)).Append("</div>");
        foreach (var b in bars)
        {
            var pct = Math.Clamp((double)(Math.Abs(b.Value) / max * 100m), 2, 100);
            sb.Append("<div style=\"display:flex;align-items:center;gap:8px;margin:4px 0;font-size:12px;\">");
            sb.Append("<div style=\"width:150px;color:#5b6577;\">").Append(H(b.Label)).Append("</div>");
            sb.Append("<div style=\"flex:1;background:#eef2f7;border-radius:4px;height:14px;overflow:hidden;\">");
            sb.Append("<div style=\"width:").Append(pct.ToString("0.0", CultureInfo.InvariantCulture)).Append("%;height:14px;background:").Append(H(b.Color)).Append(";\"></div></div>");
            sb.Append("<div style=\"width:110px;text-align:right;font-weight:700;\">").Append(H(b.Value.ToString("N2", CultureInfo.InvariantCulture))).Append("</div></div>");
        }

        return sb.Append("</div>").ToString();
    }

    public static string Section(string title, string inner, bool pageBreak = false)
    {
        var cls = pageBreak ? " epc-aud-sec" : "";
        return "<div class=\"epc-aud-sec" + cls + "\"><h4>" + H(title) + "</h4>" + inner + "</div>";
    }

    public static string VatHeader() =>
        "<div style=\"display:flex;align-items:center;width:100%;gap:8px;background:#2b3a55;color:#fff;padding:7px 6px;font-size:11px;text-transform:uppercase;letter-spacing:.5px;border-radius:4px 4px 0 0;\">"
        + "<span style=\"width:46px;\">Box</span><span style=\"flex:1;\">Description</span>"
        + "<span style=\"width:150px;text-align:right;\">Amount</span>"
        + "<span style=\"width:130px;text-align:right;\">VAT amount</span>"
        + "<span style=\"width:120px;text-align:right;\">Adjustment</span></div>";

    public static string VatBox(string box, string desc, decimal amount, decimal vat, string ccy, bool sample = false, IReadOnlyList<(string Doc, string Date, string Party, string Trn, decimal Net, decimal Vat)>? invoices = null)
    {
        var hint = invoices is { Count: > 0 }
            ? " <span class=\"epc-drill-hint\" style=\"font-size:10px;color:#2b6cb0;font-weight:600;\">▸ " + invoices.Count.ToString(CultureInfo.InvariantCulture) + " invoices</span>"
            : "";
        var sampleMark = sample ? " <span class=\"label label-warning\" style=\"font-size:9px;\">sample</span>" : "";
        var summary = "<div class=\"epc-line\" style=\"display:flex;align-items:center;width:100%;gap:8px;color:#1e293b;\">"
            + "<span style=\"width:46px;font-weight:700;\">" + H(box) + "</span>"
            + "<span style=\"flex:1;\">" + H(desc) + sampleMark + hint + "</span>"
            + "<span style=\"width:150px;text-align:right;\">" + H(Money(amount, ccy)) + "</span>"
            + "<span style=\"width:130px;text-align:right;font-weight:600;\">" + H(Money(vat, ccy)) + "</span>"
            + "<span style=\"width:120px;text-align:right;color:#475569;\">" + H(Money(0, ccy)) + "</span></div>";
        if (invoices is not { Count: > 0 })
        {
            return "<div style=\"border-bottom:1px solid #edf0f5;padding:8px 6px;background:#fff;\">" + summary + "</div>";
        }

        var sb = new StringBuilder();
        sb.Append("<details class=\"epc-box-drill\" style=\"border-bottom:1px solid #edf0f5;background:#fff;\">");
        sb.Append("<summary style=\"cursor:pointer;padding:8px 6px;list-style:none;\">").Append(summary).Append("</summary>");
        sb.Append("<div class=\"epc-print-hide\" style=\"background:#fafbfd;padding:6px 10px 12px 52px;overflow-x:auto;\">");
        sb.Append("<table class=\"table table-condensed\" style=\"margin:0;background:#fff;border:1px solid #e6eaf1;font-size:11.5px;\">");
        sb.Append("<thead><tr style=\"background:#f0f3f8;\"><th>Invoice</th><th>Date</th><th>Party</th><th>TRN</th><th style=\"text-align:right;\">Net</th><th style=\"text-align:right;\">VAT</th></tr></thead><tbody>");
        decimal tn = 0, tv = 0;
        foreach (var iv in invoices)
        {
            tn += iv.Net;
            tv += iv.Vat;
            sb.Append("<tr><td style=\"font-weight:600;\">").Append(H(iv.Doc)).Append("</td><td>").Append(H(iv.Date))
                .Append("</td><td>").Append(H(iv.Party)).Append("</td><td class=\"text-muted\">").Append(H(iv.Trn))
                .Append("</td><td style=\"text-align:right;\">").Append(H(Money(iv.Net, ccy)))
                .Append("</td><td style=\"text-align:right;\">").Append(H(Money(iv.Vat, ccy))).Append("</td></tr>");
        }

        sb.Append("<tr style=\"background:#eef3fb;font-weight:700;\"><td colspan=\"4\">Total — ")
            .Append(invoices.Count.ToString(CultureInfo.InvariantCulture)).Append(" invoices</td><td style=\"text-align:right;\">")
            .Append(H(Money(tn, ccy))).Append("</td><td style=\"text-align:right;\">").Append(H(Money(tv, ccy))).Append("</td></tr>");
        return sb.Append("</tbody></table></div></details>").ToString();
    }

    public static string AmtTable(IEnumerable<(string Label, decimal Amount, string Kind)> rows, string ccy)
    {
        var sb = new StringBuilder();
        sb.Append("<table class=\"table table-condensed\" style=\"max-width:820px;\">");
        foreach (var r in rows)
        {
            if (r.Kind == "head")
            {
                sb.Append("<tr style=\"background:#eef2f8;\"><td colspan=\"2\" style=\"padding:5px 8px;font-weight:700;color:#13294b;\">").Append(H(r.Label)).Append("</td></tr>");
                continue;
            }

            var lbl = "padding:5px 8px;";
            var num = "padding:5px 8px;text-align:right;white-space:nowrap;";
            if (r.Kind == "sub")
            {
                lbl += "font-weight:700;border-top:1px solid #cfd8e6;";
                num += "font-weight:700;border-top:1px solid #cfd8e6;";
            }
            else if (r.Kind == "total")
            {
                lbl += "font-weight:800;border-top:2px solid #13294b;background:#f4fbf6;";
                num += "font-weight:800;border-top:2px solid #13294b;background:#f4fbf6;";
            }

            sb.Append("<tr><td style=\"").Append(lbl).Append("\">").Append(H(r.Label)).Append("</td><td style=\"")
                .Append(num).Append("\">").Append(H(Money(r.Amount, ccy))).Append("</td></tr>");
        }

        return sb.Append("</table>").ToString();
    }

    public static string CheckTable(IEnumerable<(string Status, string Msg)> checks)
    {
        var sb = new StringBuilder();
        var errors = 0;
        var warns = 0;
        var rows = new StringBuilder();
        foreach (var c in checks)
        {
            string badge, bg;
            if (c.Status == "error") { errors++; badge = "<span class=\"label label-danger\">FAIL</span>"; bg = "#fff3f3"; }
            else if (c.Status == "warn") { warns++; badge = "<span class=\"label label-warning\">REVIEW</span>"; bg = "#fffaf0"; }
            else { badge = "<span class=\"label label-success\">PASS</span>"; bg = "#f4fbf6"; }
            rows.Append("<tr style=\"background:").Append(bg).Append(";\"><td style=\"padding:5px 8px;width:70px;\">")
                .Append(badge).Append("</td><td style=\"padding:5px 8px;\">").Append(H(c.Msg)).Append("</td></tr>");
        }

        if (errors == 0 && warns == 0)
            sb.Append("<div class=\"alert alert-success\" style=\"margin:8px 0;\">All compliance checks passed.</div>");
        else
            sb.Append("<div class=\"alert ").Append(errors > 0 ? "alert-danger" : "alert-warning")
                .Append("\" style=\"margin:8px 0;\"><strong>")
                .Append(errors.ToString(CultureInfo.InvariantCulture)).Append(" error(s), ")
                .Append(warns.ToString(CultureInfo.InvariantCulture)).Append(" review item(s)</strong> — address before filing.</div>");

        return sb.Append("<table class=\"table table-condensed\" style=\"font-size:12px;\"><thead><tr style=\"background:#f0f3f8;\"><th>Result</th><th>Compliance check</th></tr></thead><tbody>")
            .Append(rows).Append("</tbody></table>").ToString();
    }

    public static string DocName(string name, DateTime from)
    {
        var raw = name + "_FY" + from.Year.ToString(CultureInfo.InvariantCulture);
        var sb = new StringBuilder(raw.Length);
        foreach (var ch in raw)
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        return sb.ToString();
    }

    public static string PrintCtxJs(IReadOnlyDictionary<string, string> ctx)
    {
        var sb = new StringBuilder();
        sb.Append("<script>window.__epcExtCtx={");
        var first = true;
        foreach (var kv in ctx)
        {
            if (!first) sb.Append(',');
            first = false;
            sb.Append(System.Text.Json.JsonSerializer.Serialize(kv.Key));
            sb.Append(':');
            sb.Append(System.Text.Json.JsonSerializer.Serialize(kv.Value ?? ""));
        }

        return sb.Append("};</script>").ToString();
    }

    public const string PrintFnJs = """
<script>
function epcExtBuildDoc(opts){
	opts = opts || {};
	var WORD = !!opts.word;
	var doc = document.getElementById('epc_ext_doc');
	if(!doc){ return null; }
	var c = window.__epcExtCtx || {};
	var clone = doc.cloneNode(true);
	var rm = function(sel){ var n=clone.querySelectorAll(sel); for(var i=0;i<n.length;i++){ if(n[i].parentNode){ n[i].parentNode.removeChild(n[i]); } } };
	rm('.epc-print-hide');
	rm('.epc-ct-drill');
	rm('.epc-drill-hint');
	rm('button, textarea, script, .btn, form');
	var docd = clone.ownerDocument;
	var bd = clone.querySelectorAll('details.epc-box-drill');
	for(var i=0;i<bd.length;i++){ var d=bd[i]; if(!d.parentNode) continue; var s=d.querySelector('summary'); var rep=docd.createElement('div'); rep.className='epc-line'; rep.innerHTML=s?s.innerHTML:''; d.parentNode.replaceChild(rep,d); }
	var sd = clone.querySelectorAll('details');
	for(var j=0;j<sd.length;j++){ var e=sd[j]; if(!e.parentNode) continue; var su=e.querySelector('summary'); var wrap=docd.createElement('div'); wrap.className='mis-sched'; if(su){ var h=docd.createElement('div'); h.className='mis-sched-h'; h.innerHTML=su.innerHTML; wrap.appendChild(h); } var kids=[]; for(var k=0;k<e.childNodes.length;k++){ kids.push(e.childNodes[k]); } for(var k2=0;k2<kids.length;k2++){ if(kids[k2]!==su){ wrap.appendChild(kids[k2]); } } e.parentNode.replaceChild(wrap,e); }
	var as=clone.querySelectorAll('a'); for(var a=0;a<as.length;a++){ var an=as[a]; if(an.parentNode){ an.parentNode.replaceChild(clone.ownerDocument.createTextNode(an.textContent),an); } }
	var pal=['#2b6cb0','#2f855a','#b7791f','#805ad5','#c53030','#0987a0'];
	var cards=clone.querySelectorAll('[style*="min-width:140px"],[style*="min-width:150px"]');
	for(var cc=0;cc<cards.length;cc++){ var col=pal[cc%pal.length]; cards[cc].style.borderTop='3px solid '+col; cards[cc].style.borderColor=col; cards[cc].style.background='#fff'; var vv=cards[cc].lastElementChild; if(vv){ vv.style.color=col; } }
	if(WORD){
		var isFlexRow=function(el){ if(!el||el.nodeType!==1){ return false; } var d=(el.style&&el.style.display)?String(el.style.display):''; var cls=(typeof el.className==='string')?el.className:''; return d.indexOf('flex')!==-1 || /(^|\s)epc-line(\s|$)/.test(cls); };
		var allEls=clone.querySelectorAll('*');
		var flexRows=[];
		for(var fi=0;fi<allEls.length;fi++){
			var elx=allEls[fi];
			if(!isFlexRow(elx)){ continue; }
			if(elx.querySelector && elx.querySelector('table')){ continue; }
			var nested=false, sub=elx.querySelectorAll('*');
			for(var si=0;si<sub.length;si++){ if(isFlexRow(sub[si])){ nested=true; break; } }
			if(nested){ continue; }
			flexRows.push(elx);
		}
		for(var fr=0;fr<flexRows.length;fr++){
			var row=flexRows[fr];
			var kids=[]; for(var kc=0;kc<row.children.length;kc++){ kids.push(row.children[kc]); }
			if(kids.length===0){ continue; }
			var cells='';
			for(var kk=0;kk<kids.length;kk++){
				var kid=kids[kk], ks=kid.style||{};
				var ts='border:none;padding:2px 6px;vertical-align:top;';
				if(ks.width){ ts+='width:'+ks.width+';'; }
				if(ks.textAlign){ ts+='text-align:'+ks.textAlign+';'; }
				else if(!ks.width){ ts+='text-align:left;'; }
				cells+='<td style="'+ts+'">'+kid.innerHTML+'</td>';
			}
			row.style.display='block';
			row.innerHTML='<table style="border-collapse:collapse;width:100%;margin:0;border:none;table-layout:fixed;"><tr>'+cells+'</tr></table>';
		}
	}
	var esc = function(s){ return String(s==null?'':s).replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;'); };
	var trnLine = c.trn ? (esc(c.trnL)+': '+esc(c.trn)) : '';
	var TH = (c.theme==='red');
	var P1 = TH ? '#b3122a' : '#2b6cb0';
	var P2 = TH ? '#7a0c1c' : '#1d2740';
	var LT = TH ? '#fdeef0' : '#eef4fc';
	var GRAD  = WORD ? P2 : (TH ? 'linear-gradient(90deg,#7a0c1c,#b3122a)' : 'linear-gradient(90deg,#1d2740,#2b6cb0)');
	var HGRAD = WORD ? P1 : (TH ? 'linear-gradient(90deg,#8f0f22,#b3122a)' : 'linear-gradient(90deg,#2b3a55,#3a6ea5)');
	var COVBG = WORD ? LT : (TH ? 'linear-gradient(135deg,#fff6f7,#fbe3e7)' : 'linear-gradient(180deg,#fbfdff,#eef4fc)');
	var css =
	(WORD ? '@page WordSection1{size:21.0cm 29.7cm;margin:2.0cm;}div.WordSection1{}' : '@page{size:A4;margin:24mm 14mm 20mm;}')
	+'*{box-sizing:border-box;-webkit-print-color-adjust:exact;print-color-adjust:exact;}'
	+'body{font-family:"Segoe UI",Arial,Helvetica,sans-serif;color:#1f2733;font-size:11.5px;line-height:1.45;margin:0;}'
	+'.mis-cover{min-height:220mm;display:flex;flex-direction:column;justify-content:center;align-items:center;text-align:center;border:2px solid '+P1+';border-top:14px solid '+P2+';border-radius:6px;padding:40px;page-break-after:always;background:'+COVBG+';}'
	+'.mis-cover .badge{font-size:11px;letter-spacing:3px;text-transform:uppercase;color:#fff;background:'+P1+';padding:4px 14px;border-radius:14px;}'
	+'.mis-cover .co{font-size:30px;font-weight:800;color:'+P2+';margin:16px 0 2px;}'
	+'.mis-cover .ttl{font-size:24px;font-weight:800;color:#fff;background:'+GRAD+';padding:10px 26px;border-radius:6px;margin:46px 0 6px;}'
	+'h3{font-size:16px;color:'+P2+';border-bottom:3px solid #c2a14d;padding-bottom:5px;}'
	+'h4{font-size:12.5px;color:#fff !important;margin:18px 0 6px;padding:6px 12px;background:'+HGRAD+';border-radius:4px;}'
	+'table{border-collapse:collapse;width:100%;margin:6px 0;}td,th{border:1px solid #c7cedb;padding:5px 8px;font-size:11px;}th{background:'+HGRAD+';color:#fff;}'
	+'.mis-sign{margin-top:34px;display:flex;justify-content:space-between;gap:40px;}'
	+'.mis-sign>div{flex:1;border-top:1px solid #7a869a;padding-top:6px;font-size:11px;color:#5b6577;}'
	+'.mis-commentary{background:'+LT+';border-left:4px solid '+P1+';padding:10px 14px;margin:10px 0;}'
	+'.epc-ext-bars{page-break-inside:avoid;}';
	var cover =
	'<div class="mis-cover"><div class="badge">Statutory / Management Report</div>'
	+'<div class="co">'+esc(c.co)+'</div><div class="rule"></div>'
	+'<div class="ttl">'+esc(c.ttl)+'</div>'
	+'<div class="juris">Jurisdiction: '+esc(c.juris)+'</div>'
	+'<div class="period">Reporting period: '+esc(c.perL)+'</div>'
	+'<div class="perd">'+esc(c.perR)+'</div>'
	+'<div class="meta">Submitted to: '+esc(c.auth)+'<br>Governing law: '+esc(c.law)+'<br>Prepared on '+esc(c.gen)+'</div></div>';
	var sign = WORD
		? '<table class="mis-sign" style="width:100%;border-collapse:collapse;"><tr><td style="width:33%;border:none;border-top:1px solid #7a869a;">Prepared by &amp; date</td><td style="width:33%;border:none;border-top:1px solid #7a869a;">Reviewed by &amp; date</td><td style="width:34%;border:none;border-top:1px solid #7a869a;">Authorised signatory &amp; stamp</td></tr></table>'
		: '<div class="mis-sign"><div>Prepared by &amp; date</div><div>Reviewed by &amp; date</div><div>Authorised signatory &amp; stamp</div></div>';
	var inner = cover + clone.innerHTML + sign;
	if(WORD){ inner = '<div class="WordSection1">'+inner+'</div>'; }
	var head = '<meta charset="utf-8"><title>'+esc(c.ttl)+' — '+esc(c.co)+'</title>';
	if(WORD){ head += '<!--[if gte mso 9]><xml><w:WordDocument><w:View>Print</w:View><w:Zoom>100</w:Zoom></w:WordDocument></xml><![endif]-->'; }
	head += '<style>'+css+'</style>';
	var htmlOpen = WORD
		? '<html xmlns:o="urn:schemas-microsoft-com:office:office" xmlns:w="urn:schemas-microsoft-com:office:word" xmlns="http://www.w3.org/TR/REC-html40">'
		: '<html>';
	return { title: esc(c.ttl)+' — '+esc(c.co), html: htmlOpen+'<head>'+head+'</head><body>'+inner+'</body></html>' };
}
function epcExtPrint(){
	var b = epcExtBuildDoc({word:false});
	if(!b){ window.print(); return; }
	var w = window.open('', '_blank');
	w.document.write(b.html);
	w.document.close();
	setTimeout(function(){ w.focus(); w.print(); }, 350);
}
function epcExtWord(fname){
	var b = epcExtBuildDoc({word:true});
	if(!b){ alert('Report not ready.'); return; }
	var blob = new Blob(['\ufeff'+b.html], {type:'application/msword'});
	var url = URL.createObjectURL(blob);
	var a = document.createElement('a');
	a.href = url; a.download = (fname||'Report')+'.doc';
	document.body.appendChild(a); a.click(); document.body.removeChild(a);
	setTimeout(function(){ URL.revokeObjectURL(url); }, 1500);
}
</script>
""";
}
