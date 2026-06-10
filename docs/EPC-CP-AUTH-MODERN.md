# Modern CP authentication (email OTP + Google OAuth)

Phase 1 MVP for ECOM AE / eParts Cart Control Panel sign-in across Super CP, tenant CP, demo CP, and client ERP shells.

## Features

| Method | Behavior |
|--------|----------|
| **Password** | Existing Docpart CP login (unchanged). |
| **Email code** | 6-digit OTP, 10-minute TTL, max 5 sends/hour/email. Platform table `epc_auth_otp_requests` on `ecomae`. Session created in **tenant** DB after verify. |
| **Google** | OAuth 2.0 with **central callback** on `https://www.ecomae.com/epc-auth-google-callback.php`. `state` carries `tenant_key`, return host/path. Cross-domain tenants use `epc-auth-handoff.php`. |

## Endpoints

| URL | Method | Purpose |
|-----|--------|---------|
| `/epc-auth-send-code.php` | POST JSON | Send OTP email |
| `/epc-auth-verify-code.php` | POST JSON | Verify OTP → session |
| `/epc-auth-google-start.php` | GET | Redirect to Google (`tenant_key` optional) |
| `/epc-auth-google-callback.php` | GET | **Deploy on www.ecomae.com only** |
| `/epc-auth-handoff.php` | GET | Set CP cookies on tenant hostname |

## Operator setup — Google

1. [Google Cloud Console](https://console.cloud.google.com/apis/credentials) → Create **OAuth 2.0 Client ID** (Web application).
2. **Authorized redirect URIs** (required):
   - `https://www.ecomae.com/epc-auth-google-callback.php`
3. On the server, copy `config.epc-oauth.example.php` → `config.epc-oauth.php` and set `client_id` / `client_secret`.
4. Do **not** commit `config.epc-oauth.php`.

Optional: per-tenant marketing domains do **not** need separate redirect URIs for Phase 1 — callback stays on ecomae; handoff completes login on the tenant host.

## Operator setup — SMTP (email OTP)

Uses existing site mail config (`DP_Config`: `smtp_mode`, `smtp_host`, `from_email`, etc.) via `DocpartMailer`.

Verify in Super CP that test notifications send successfully before relying on email codes.

## Super CP UI

- **Control Panel → portal → Modern auth settings** (`/cp/control/portal/epc_cp_auth_settings`) — status stub when OAuth is missing.
- Login card tabs: **Password | Email code | Continue with Google** on all shells using `login_form/template.php`.

## Security

- HTTPS required for auth APIs (localhost exempt for dev).
- OAuth `state` + `nonce` (HMAC signed, 15-minute window).
- OTP stored as SHA-256 hash only.
- Super CP one-click demo autologin (`epc-demo-cp-autologin.php`) is unchanged and independent.
- Demo CP: Google/OTP may auto-provision backend users (same as “first login”); Super CP does not auto-provision.

## Verify URLs (manual)

| Target | URL |
|--------|-----|
| Demo CP login | `https://www.ecomae.com/cp/demo/demo_260602_ap_13/` |
| Email OTP API | POST `https://www.ecomae.com/epc-auth-send-code.php` `{"email":"…","tenant_key":"demo_260602_ap_13"}` |
| Super CP settings | `https://www.ecomae.com/cp/control/portal/epc_cp_auth_settings` |
| Tenant (example) | `https://www.epartscart.com/cp/` → Google uses handoff after callback |

## Limitations (Phase 1)

- **Microsoft / Facebook / Apple**: hook only (`epc_auth_social_providers()`); not implemented.
- **Super CP**: email OTP/Google only for existing backend users — no email-only provision on platform operator DB.
- **Production tenant domains**: OAuth app redirect is single URI on ecomae; tenant routing via `state` + handoff (not per-domain OAuth clients).
- **Phone OTP**: not in scope; password form still supports phone+password legacy mode.

## Files (deploy via `tools/push_one.py`)

- `content/general_pages/epc_auth_common.php`
- `content/general_pages/epc_auth_email_otp.php`
- `content/general_pages/epc_auth_social.php`
- `epc-auth-*.php` (root)
- `config.epc-oauth.example.php`
- `cp/plugins/authentication/login_form/template.php`
- `cp/templates/bootstrap_admin/css/epc_cp_professional.css`
- `cp/content/control/portal/epc_cp_auth_settings.php`
- `content/general_pages/epc_cp_professional_shell.php` (CSS version bump)
