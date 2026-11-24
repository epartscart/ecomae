# Stripe payment gateway — CP setup guide

One-time setup per storefront tenant. The Stripe gateway is already part of the
platform — each tenant configures its own Stripe account in its own CP, so the
same integration serves auto247.ae, auto247.pk, auto247.com.pk and any future
storefront without code changes.

## How it works

- Shopper picks **Stripe** at pay time → `go_to_pay.php` creates a Stripe
  **Checkout Session** (hosted card/Apple Pay/Google Pay page) with the order
  amount and currency and redirects the shopper to Stripe.
- On success Stripe returns the shopper to
  `payment_systems/stripe/notification.php?session_id=…`, which re-fetches the
  session from Stripe with the secret key and only credits the
  order/balance when `payment_status == "paid"` and the session's
  `client_reference_id` matches the operation. A forged success URL credits
  nothing.
- Cancel returns the shopper to the balance page unpaid.

## CP configuration (per tenant)

CP → **Payment gateways** → **Stripe**:

| Field | Value |
|---|---|
| Publishable key | `pk_live_…` from Stripe Dashboard → Developers → API keys |
| Secret key | `sk_live_…` (same page — "Reveal live key") |
| Webhook signing secret | optional — only needed if a `checkout.session.completed` webhook is added later |
| Currency | default charge currency, e.g. `AED` (amounts follow the order currency at pay time) |
| Demo mode | **off** for live payments |

Set Stripe **active** (and `anable`) so it appears in the storefront payment
method picker for that tenant's offices.

## Rollout checklist for a new storefront

1. Create/verify the tenant's Stripe account; take `pk_live_` + `sk_live_`.
2. CP → Payment gateways → Stripe → enter keys, currency, demo off, active.
3. Place a real order → pay by Stripe → confirm the order flips to paid and
   the accounting row is `active=1`.
4. Repeat with a small amount in another currency the storefront supports
   (e.g. AED 1 and USD 1) to verify currency handling.

## Notes

- Keys are stored per tenant in `shop_payment_systems.parameters_values` —
  never in code or config.
- Zero-decimal currencies (JPY, KRW, …) are converted automatically; everything
  else is charged in minor units ×100.
- Stripe account auto247: `acct_1Ud6iADy1EEBVWdN` (AE, default AED).
