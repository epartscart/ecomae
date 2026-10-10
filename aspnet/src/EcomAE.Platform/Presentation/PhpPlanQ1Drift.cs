using System.Globalization;
using System.Text;
using System.Text.Json;
using EcomAE.Platform.Erp;

namespace EcomAE.Platform.Presentation;

/// <summary>
/// Plan Q1-drift MFA UI renderers. PHP identifiers kept for the inventory:
/// <c>epc_mfa_render_enroll_page</c>, <c>epc_mfa_render_verify_page</c>,
/// <c>epc_mfa_render_settings_panel</c>, <c>epcMfaAjax</c>,
/// <c>epcMfaStartEnroll</c>, <c>epcMfaRegenBackup</c>, <c>epcMfaDisable</c>.
/// </summary>
public static class PhpPlanQ1Drift
{
    public const string MfaUiPath = "content/general_pages/epc_mfa_ui.php";

    public static Func<string, string>? QrDataUri { get; set; }

    public static void Reset()
    {
        QrDataUri = null;
    }

    private static bool PhpEmpty(object? value)
        => value switch
        {
            null => true,
            false => true,
            0 => true,
            0L => true,
            0d => true,
            0f => true,
            "" => true,
            "0" => true,
            JsonElement je when je.ValueKind is JsonValueKind.Null or JsonValueKind.False => true,
            JsonElement je when je.ValueKind == JsonValueKind.Number && je.GetDouble() == 0 => true,
            JsonElement je when je.ValueKind == JsonValueKind.String && (je.GetString() is "" or "0") => true,
            System.Collections.ICollection c => c.Count == 0,
            _ => false
        };

    private static bool PhpTruthy(object? value) => !PhpEmpty(value);

    private static string H(object? value)
        => ErpDocumentControlRender.H(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");

    private static object? Field(Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    private static int PhpInt(object? value)
    {
        switch (value)
        {
            case null:
                return 0;
            case bool b:
                return b ? 1 : 0;
            case int n:
                return n;
            case long l:
                return (int)l;
            case double d:
                return (int)d;
            default:
                var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                _ = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed);
                return parsed;
        }
    }

    private static string ResolveQr(string uri)
        => QrDataUri?.Invoke(uri) ?? PhpPlanQ1Wave.EpcMfaQrDataUri(uri);

    private const string EnrollScript = """
<script>
document.getElementById('epc-mfa-confirm-form').addEventListener('submit', function(e) {
	e.preventDefault();
	var code = document.getElementById('epc-mfa-code').value.trim();
	if (code.length !== 6) { return; }
	var xhr = new XMLHttpRequest();
	xhr.open('POST', window.location.pathname + '?epc_mfa_ajax=1');
	xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
	xhr.onload = function() {
		var result = document.getElementById('epc-mfa-confirm-result');
		try {
			var data = JSON.parse(xhr.responseText);
			if (data.ok) {
				result.innerHTML = '<div style="color:#198754;font-weight:600;padding:10px;background:#d1e7dd;border-radius:6px;">MFA enabled successfully! Redirecting...</div>';
				setTimeout(function() {
					var redirect = new URLSearchParams(window.location.search).get('redirect');
					window.location.href = redirect || '/cp/';
				}, 1500);
			} else {
				result.innerHTML = '<div style="color:#dc3545;padding:10px;background:#f8d7da;border-radius:6px;">' + (data.error || 'Invalid code') + '</div>';
			}
		} catch (ex) {
			result.innerHTML = '<div style="color:#dc3545;">Error processing response</div>';
		}
	};
	xhr.send('mfa_action=confirm&code=' + encodeURIComponent(code));
});
</script>
""";

    private const string VerifyPage = """
<div class="epc-mfa-verify" style="max-width:420px;margin:60px auto;font-family:system-ui,-apple-system,sans-serif;text-align:center;">
	<div style="font-size:48px;margin-bottom:12px;">&#128274;</div>
	<h3 style="margin-bottom:6px;color:#1a1a2e;">Two-Factor Verification</h3>
	<p style="color:#555;margin-bottom:24px;">Enter the 6-digit code from your authenticator app, or a backup code.</p>

	<form id="epc-mfa-verify-form">
		<input type="text" id="epc-mfa-verify-code" name="code" maxlength="10" placeholder="000000"
			style="font-size:28px;text-align:center;letter-spacing:8px;padding:14px;border:2px solid #ddd;border-radius:10px;width:100%;box-sizing:border-box;font-family:monospace;"
			autocomplete="one-time-code" inputmode="numeric" autofocus />
		<button type="submit" style="margin-top:16px;width:100%;padding:14px;background:#0d6efd;color:#fff;border:none;border-radius:10px;font-size:17px;font-weight:600;cursor:pointer;">Verify</button>
		<div id="epc-mfa-verify-result" style="margin-top:12px;"></div>
	</form>

	<p style="margin-top:20px;font-size:13px;color:#888;">Lost your device? Enter a 10-character backup code instead.</p>
</div>

<script>
document.getElementById('epc-mfa-verify-form').addEventListener('submit', function(e) {
	e.preventDefault();
	var code = document.getElementById('epc-mfa-verify-code').value.trim();
	if (code.length < 6) { return; }
	var xhr = new XMLHttpRequest();
	xhr.open('POST', window.location.pathname + '?epc_mfa_ajax=1');
	xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
	xhr.onload = function() {
		var result = document.getElementById('epc-mfa-verify-result');
		try {
			var data = JSON.parse(xhr.responseText);
			if (data.ok) {
				result.innerHTML = '<div style="color:#198754;font-weight:600;padding:10px;background:#d1e7dd;border-radius:6px;">Verified! Redirecting...</div>';
				setTimeout(function() {
					var redirect = new URLSearchParams(window.location.search).get('redirect');
					window.location.href = redirect || '/cp/';
				}, 1000);
			} else {
				result.innerHTML = '<div style="color:#dc3545;padding:10px;background:#f8d7da;border-radius:6px;">' + (data.error || 'Invalid code') + '</div>';
			}
		} catch (ex) {
			result.innerHTML = '<div style="color:#dc3545;">Error processing response</div>';
		}
	};
	xhr.send('mfa_action=verify&code=' + encodeURIComponent(code));
});
</script>
""";

    private const string SettingsScript = """
<script>
function epcMfaAjax(params, cb) {
	var xhr = new XMLHttpRequest();
	xhr.open('POST', window.location.pathname + '?epc_mfa_ajax=1');
	xhr.setRequestHeader('Content-Type', 'application/x-www-form-urlencoded');
	xhr.onload = function() { cb(JSON.parse(xhr.responseText)); };
	xhr.send(params);
}
function epcMfaStartEnroll() {
	epcMfaAjax('mfa_action=enroll', function(data) {
		if (data.ok) { window.location.href = '?epc_mfa=enroll'; }
		else { document.getElementById('epc-mfa-settings-result').innerHTML = '<div style="color:#dc3545;">' + data.error + '</div>'; }
	});
}
function epcMfaRegenBackup() {
	epcMfaAjax('mfa_action=regenerate_backup', function(data) {
		if (data.ok && data.backup_codes) {
			var html = '<div style="background:#fff3cd;padding:16px;border-radius:8px;border:1px solid #ffc107;"><h4 style="color:#856404;">New Backup Codes</h4><div style="display:grid;grid-template-columns:1fr 1fr;gap:6px;font-family:monospace;">';
			data.backup_codes.forEach(function(c) { html += '<div style="background:#fff;padding:6px 10px;border-radius:4px;border:1px solid #e0e0e0;">' + c + '</div>'; });
			html += '</div><p style="font-size:13px;color:#856404;margin-top:8px;">Save these codes. Old codes are invalidated.</p></div>';
			document.getElementById('epc-mfa-settings-result').innerHTML = html;
		}
	});
}
function epcMfaDisable() {
	epcMfaAjax('mfa_action=disable', function(data) {
		if (data.ok) { window.location.reload(); }
	});
}
</script>
""";

    /// <summary>PHP <c>epc_mfa_render_enroll_page</c>.</summary>
    public static string EpcMfaRenderEnrollPage(Dictionary<string, object?> enrollment)
    {
        var secret = H(Field(enrollment, "secret") ?? "");
        var qrUri = Convert.ToString(Field(enrollment, "qr_uri") ?? "", CultureInfo.InvariantCulture) ?? "";
        var qrImg = H(ResolveQr(qrUri));
        var backupCodes = Field(enrollment, "backup_codes");
        var sb = new StringBuilder();
        sb.Append("<div class=\"epc-mfa-enroll\" style=\"max-width:520px;margin:30px auto;font-family:system-ui,-apple-system,sans-serif;\">\n");
        sb.Append("\t<h3 style=\"margin-bottom:6px;color:#1a1a2e;\">Set Up Two-Factor Authentication</h3>\n");
        sb.Append("\t<p style=\"color:#555;margin-bottom:20px;\">Scan the QR code below with your authenticator app (Google Authenticator, Authy, Microsoft Authenticator).</p>\n\n");
        sb.Append("\t<div style=\"text-align:center;margin:20px 0;\">\n");
        sb.Append("\t\t<img src=\"").Append(qrImg).Append("\" alt=\"TOTP QR Code\" width=\"200\" height=\"200\" style=\"border:1px solid #e0e0e0;border-radius:8px;padding:8px;background:#fff;\" />\n");
        sb.Append("\t</div>\n\n");
        sb.Append("\t<div style=\"background:#f8f9fa;border:1px solid #e0e0e0;border-radius:6px;padding:12px;margin:16px 0;font-family:monospace;font-size:13px;word-break:break-all;text-align:center;\">\n");
        sb.Append("\t\t<small style=\"color:#888;display:block;margin-bottom:4px;font-family:system-ui;\">Manual entry key:</small>\n");
        sb.Append("\t\t").Append(secret).Append("\t</div>\n\n");
        sb.Append("\t<form id=\"epc-mfa-confirm-form\" style=\"margin-top:20px;\">\n");
        sb.Append("\t\t<label for=\"epc-mfa-code\" style=\"font-weight:600;color:#333;display:block;margin-bottom:6px;\">Enter the 6-digit code from your app:</label>\n");
        sb.Append("\t\t<div style=\"display:flex;gap:10px;\">\n");
        sb.Append("\t\t\t<input type=\"text\" id=\"epc-mfa-code\" name=\"code\" maxlength=\"6\" pattern=\"[0-9]{6}\" placeholder=\"000000\"\n");
        sb.Append("\t\t\t\tstyle=\"flex:1;font-size:24px;text-align:center;letter-spacing:8px;padding:12px;border:2px solid #ddd;border-radius:8px;font-family:monospace;\"\n");
        sb.Append("\t\t\t\tautocomplete=\"one-time-code\" inputmode=\"numeric\" autofocus />\n");
        sb.Append("\t\t\t<button type=\"submit\" style=\"padding:12px 24px;background:#0d6efd;color:#fff;border:none;border-radius:8px;font-size:16px;font-weight:600;cursor:pointer;\">Verify</button>\n");
        sb.Append("\t\t</div>\n");
        sb.Append("\t\t<div id=\"epc-mfa-confirm-result\" style=\"margin-top:12px;\"></div>\n");
        sb.Append("\t</form>\n\n");
        if (!PhpEmpty(backupCodes))
        {
            sb.Append("\t\t<div style=\"margin-top:24px;background:#fff3cd;border:1px solid #ffc107;border-radius:8px;padding:16px;\">\n");
            sb.Append("\t\t<h4 style=\"margin:0 0 8px;color:#856404;\">Backup Codes</h4>\n");
            sb.Append("\t\t<p style=\"font-size:13px;color:#856404;margin-bottom:12px;\">Save these codes somewhere safe. Each can be used once if you lose your authenticator.</p>\n");
            sb.Append("\t\t<div style=\"display:grid;grid-template-columns:1fr 1fr;gap:6px;font-family:monospace;font-size:14px;\">\n");
            foreach (var bc in AsList(backupCodes))
            {
                sb.Append("\t\t\t\t\t\t<div style=\"background:#fff;padding:6px 10px;border-radius:4px;border:1px solid #e0e0e0;\">")
                    .Append(H(bc)).Append("</div>\n");
            }

            sb.Append("\t\t\t\t\t</div>\n\t</div>\n\t</div>\n\n");
        }
        else
        {
            sb.Append("\t</div>\n\n");
        }

        sb.Append(EnrollScript).Append("\n\t");
        return sb.ToString();
    }

    /// <summary>PHP <c>epc_mfa_render_verify_page</c>.</summary>
    public static string EpcMfaRenderVerifyPage() => VerifyPage + "\n\t";

    /// <summary>PHP <c>epc_mfa_render_settings_panel</c>.</summary>
    public static string EpcMfaRenderSettingsPanel(Dictionary<string, object?> status)
    {
        var enrolled = PhpTruthy(Field(status, "enrolled"));
        var backupLeft = PhpInt(Field(status, "backup_codes_left"));
        _ = Field(status, "session_verified");
        var bg = enrolled ? "#d1e7dd" : "#fff3cd";
        var bd = enrolled ? "#198754" : "#ffc107";
        var sc = enrolled ? "#198754" : "#856404";
        var label = enrolled ? "Enabled" : "Not Enabled";
        var sb = new StringBuilder();
        sb.Append("<div class=\"epc-mfa-settings\" style=\"max-width:600px;font-family:system-ui,-apple-system,sans-serif;\">\n");
        sb.Append("\t<h3 style=\"margin-bottom:16px;color:#1a1a2e;\">\n");
        sb.Append("\t\t<span style=\"font-size:20px;margin-right:8px;\">&#128274;</span>\n");
        sb.Append("\t\tTwo-Factor Authentication (MFA)\n");
        sb.Append("\t</h3>\n\n");
        sb.Append("\t<div style=\"background:").Append(bg).Append(";border:1px solid ").Append(bd).Append(";border-radius:8px;padding:16px;margin-bottom:20px;\">\n");
        sb.Append("\t\t<strong style=\"color:").Append(sc).Append(";\">\n");
        sb.Append("\t\t\tStatus: ").Append(label).Append("\t\t</strong>\n");
        if (enrolled)
        {
            sb.Append("\t\t\t\t\t<span style=\"margin-left:12px;color:#198754;\">&#10003; TOTP active</span>\n");
            if (backupLeft > 0)
            {
                sb.Append("\t\t\t\t\t\t\t<span style=\"margin-left:12px;color:#666;\">").Append(backupLeft).Append(" backup codes remaining</span>\n");
            }
            else
            {
                sb.Append("\t\t\t\t\t\t\t<span style=\"margin-left:12px;color:#dc3545;\">No backup codes! Generate new ones below.</span>\n");
            }

            sb.Append("\t\t\t\t\t\t</div>\n\n");
            sb.Append("\t\t<div style=\"display:flex;gap:12px;flex-wrap:wrap;\">\n");
            sb.Append("\t\t<button onclick=\"epcMfaRegenBackup()\" style=\"padding:10px 18px;background:#ffc107;color:#333;border:none;border-radius:6px;font-weight:600;cursor:pointer;\">\n");
            sb.Append("\t\t\tRegenerate Backup Codes\n");
            sb.Append("\t\t</button>\n");
            sb.Append("\t\t<button onclick=\"if(confirm('Disable MFA? Finance routes will require re-enrollment.')){epcMfaDisable();}\" style=\"padding:10px 18px;background:#dc3545;color:#fff;border:none;border-radius:6px;font-weight:600;cursor:pointer;\">\n");
            sb.Append("\t\t\tDisable MFA\n");
            sb.Append("\t\t</button>\n");
            sb.Append("\t</div>\n");
        }
        else
        {
            sb.Append("\t\t\t\t\t<p style=\"margin:8px 0 0;color:#856404;font-size:14px;\">\n");
            sb.Append("\t\t\t\tMFA is required for finance roles and Super CP access. Enable it now using your authenticator app.\n");
            sb.Append("\t\t\t</p>\n");
            sb.Append("\t\t\t</div>\n\n");
            sb.Append("\t\t<button onclick=\"epcMfaStartEnroll()\" style=\"padding:12px 24px;background:#0d6efd;color:#fff;border:none;border-radius:8px;font-size:15px;font-weight:600;cursor:pointer;\">\n");
            sb.Append("\t\tEnable Two-Factor Authentication\n");
            sb.Append("\t</button>\n");
        }

        sb.Append("\t\n");
        sb.Append("\t<div id=\"epc-mfa-settings-result\" style=\"margin-top:16px;\"></div>\n\n");
        var methods = Field(status, "methods");
        if (!PhpEmpty(methods))
        {
            sb.Append("\t\t<div style=\"margin-top:24px;\">\n");
            sb.Append("\t\t<h4 style=\"color:#555;margin-bottom:8px;\">Enrolled Methods</h4>\n");
            sb.Append("\t\t<table style=\"width:100%;border-collapse:collapse;font-size:14px;\">\n");
            sb.Append("\t\t\t<tr style=\"border-bottom:1px solid #e0e0e0;\">\n");
            sb.Append("\t\t\t\t<th style=\"text-align:left;padding:8px;color:#666;\">Method</th>\n");
            sb.Append("\t\t\t\t<th style=\"text-align:left;padding:8px;color:#666;\">Label</th>\n");
            sb.Append("\t\t\t\t<th style=\"text-align:left;padding:8px;color:#666;\">Status</th>\n");
            sb.Append("\t\t\t\t<th style=\"text-align:left;padding:8px;color:#666;\">Last Used</th>\n");
            sb.Append("\t\t\t</tr>\n");
            foreach (var raw in AsList(methods))
            {
                var row = raw as Dictionary<string, object?> ?? [];
                var method = (Convert.ToString(Field(row, "method"), CultureInfo.InvariantCulture) ?? "").ToUpperInvariant();
                var last = Field(row, "last_used_at") ?? "Never";
                var confirmed = PhpInt(Field(row, "confirmed")) == 1
                    ? "<span style=\"color:#198754;\">Active</span>"
                    : "<span style=\"color:#ffc107;\">Pending</span>";
                sb.Append("\t\t\t\t\t\t<tr style=\"border-bottom:1px solid #f0f0f0;\">\n");
                sb.Append("\t\t\t\t<td style=\"padding:8px;\">").Append(H(method)).Append("</td>\n");
                sb.Append("\t\t\t\t<td style=\"padding:8px;\">").Append(H(Field(row, "label"))).Append("</td>\n");
                sb.Append("\t\t\t\t<td style=\"padding:8px;\">").Append(confirmed).Append("</td>\n");
                sb.Append("\t\t\t\t<td style=\"padding:8px;\">").Append(H(last)).Append("</td>\n");
                sb.Append("\t\t\t</tr>\n");
            }

            sb.Append("\t\t\t\t\t</table>\n\t</div>\n");
        }

        sb.Append("\t</div>\n\n");
        sb.Append(SettingsScript).Append("\n\t");
        return sb.ToString();
    }

    /// <summary>PHP <c>epcMfaAjax</c> identifier surface.</summary>
    public static string EpcMfaAjax() => "epcMfaAjax";

    /// <summary>PHP <c>epcMfaStartEnroll</c> identifier surface.</summary>
    public static string EpcMfaStartEnroll() => "epcMfaStartEnroll";

    /// <summary>PHP <c>epcMfaRegenBackup</c> identifier surface.</summary>
    public static string EpcMfaRegenBackup() => "epcMfaRegenBackup";

    /// <summary>PHP <c>epcMfaDisable</c> identifier surface.</summary>
    public static string EpcMfaDisable() => "epcMfaDisable";

    private static IEnumerable<object?> AsList(object? value)
    {
        if (value is IEnumerable<object?> list)
        {
            return list;
        }

        if (value is System.Collections.IEnumerable e and not string)
        {
            return e.Cast<object?>();
        }

        return [];
    }
}
