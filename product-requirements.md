# Dastarkhwan Product Requirements

Authoritative product requirements baseline. See CLAUDE.md, roadmap.md, current-state.md, decisions.md, architecture.md.

## 1. Product definition

Dastarkhwan — Affordable, mobile-first restaurant and food-business management SaaS.
Initial market: Pakistan, AJK. Future: UK and others via configuration.

Target businesses: small/medium restaurants, fast-food/takeaway, home-based food, bakeries, cafes, independent food outlets, local delivery.

Promise: "Your business. Your customers. One simple system."

Not a public multi-merchant marketplace. One business, one menu, one order-management system, multiple ordering channels.

## 2. Ordering channels

### 2.1 Public online store
Mobile-friendly store per business: branding, menu, availability, hours, cart, guest checkout, delivery/takeaway/dine-in where enabled, shareable URL, Urdu/English, responsive.

### 2.2 Store QR
Permanent QR per store/branch, stable Dastarkhwan-controlled URL/redirect; downloadable/printable; disable/revoke/regenerate; no dependency on custom domain.

### 2.3 WhatsApp ordering
Browse menu, cart, select WhatsApp, structured message prepared, redirect to business WhatsApp, customer sends, staff verifies, confirms/rejects. Distinguish prepared message, submitted request, reviewed request, confirmed order. Future WhatsApp Business Platform optional.

### 2.4 Dine-in
Per-table QR, table management, waiter activates session, customer scans/orders, multiple orders per session, running bill, customer requests waiter/bill, waiter updates service, manager oversight, close after settlement. Document QR token lifecycle, expiry, revocation, session validation.

### 2.5 Takeaway
Pickup, contact details, staff confirm, kitchen, ready-for-pickup, complete.

### 2.6 Delivery
Address/phone, service area validation, server-side delivery charges, staff assigns delivery, dispatch/delivery, COD where enabled, completion recorded. Advanced rider GPS deferred.

### 2.7 Staff-entered orders
Telephone/walk-in/counter orders enter same central workflow; record source, responsible staff, customer info.

## 3. Menu and catalog
Easy menu creation: names/prices first, details later. Onboarding: photo/PDF OCR draft with review; Excel/CSV import with mapping/validation; bulk manual entry; business templates. Product fields: Urdu/English names, description, category, price/currency, image, availability, sizes/variants, add-ons, prep notes, display order, tax. Operations: add/edit/copy/archive/delete, bulk price/availability, move, import/export, duplicate detection, reuse/branch pricing, historical price snapshots. OCR/AI optional.

## 4. Central order management
All channels use consistent order model: unique number, tenant/business/store/branch, source, customer/contact, type, products/quantities/options, notes, price snapshots, discounts/taxes/fees, currency, status, payment status, timestamps, responsible staff. Lifecycle: draft/awaiting confirmation, confirmed, accepted by kitchen, preparing, ready, awaiting pickup/dispatched, completed, cancelled. Server recalculates totals, validates availability/pricing, idempotency, separate fulfillment/payment states, preserve historical pricing, audit status changes. Unconfirmed WhatsApp request is not an order.

## 5. Tables, waiters, kitchen
Table management, QR generation, enable/disable/revoke/regenerate, open/close sessions, status display, multiple orders/session. Waiter: account, assignments, view orders, add items, service status, respond requests, cash only authorized. Manager: all tables/sessions, assign/reassign, review, approve discounts/cancellations. Kitchen: view confirmed orders, products/quantities/options/instructions, update status, ready; browser on existing device, no dedicated hardware.

## 6. Table QR security and suspicious-order controls
Manager-configured branch location, configurable geofence, location check only when relevant/permitted with accuracy/freshness, waiter verification fallback, unguessable tokens, active waiter-created sessions, expiry/closure, revocation/regeneration, rate limiting, duplicate detection, excessive quantity/total detection, invalid/closed session detection, repeated failed QR, suspicious device/IP. Flag for review not auto-fraud. Record reason and staff action. GPS supporting only.

## 7. Customer management
Guest checkout, optional account, name/phone, addresses, order history, references, saved addresses, notes/preferences. Minimal personal data, retention/privacy controls.

## 8. Billing, invoices, payments
Running dine-in bill, itemized invoice, subtotal, discounts, taxes, service charges, delivery charges, total, digital receipt, printable receipt, historical snapshots. Payment config: cash counter, authorized waiter cash, COD, selected online providers, bank transfer/Raast where supported/verified. Separate orders/invoices/payments. Cash complete only after authorized staff confirms. Online complete only after server-side callback verification (signature, reference, amount, currency, status, idempotency). Never trust browser success. Partial payments/split bills/refunds deferred.

## 9. Staff and permissions
Roles: Owner, Manager, Waiter, Kitchen, Cashier. Individual accounts, activation/deactivation, branch/table assignments, subscription staff limits, server-side authorization, audit, protect owner/last admin, no unauthorized payment confirmation/discounts/cancellations. Identity owns accounts/credentials/auth/sessions; Access owns authorization/roles/permissions/assignments. Email immutable. No mandatory MFA for end users.

## 10. Business, store, branch
Distinct Tenant/Business/Store/Branch. Business profile, logo/branding, description, contact, branch address/location/contact, hours/working days, enabled order types, delivery areas/charges, tables/staff, menu reuse/branch settings, open/closed/temporarily closed. Limits via subscription entitlements.

## 11. Store URLs, custom domains, branding
Default subdomain e.g. alnoor.dastarkhwan.pk. Merchant custom domain connect/disconnect, DNS verification, instructions, www/apex support, auto HTTPS cert provisioning/renewal, status, change/reconnection, redirect alternate hostnames, tenant/store/branch routing, prevent duplicate claims/cross-tenant takeover, handle expired/disconnected/ineligible domains, audit/monitor. Merchants own/renew domains. Domain limits as configurable entitlements (proposal: Trial/Basic subdomain, Standard one, Business multiple) pending commercial approval. QR uses permanent Dastarkhwan-controlled identifier/redirect and survives domain changes. Custom domains integrate with stores/menus/ordering/WhatsApp/branding/routing.

## 12. Reports and insights
Daily sales, date-range sales, order count by status, orders by channel, dine-in/takeaway/delivery totals, WhatsApp count, cash/digital totals, unpaid bills, popular products, staff/waiter activity, branch comparisons. Respect tenant/branch/role permissions. Exports where useful.

## 13. Subscription and SaaS
Multi-tenant subscription SaaS: configurable trial/duration, start/renewal/expiry/cancellation, Basic/Standard/Business as configurable examples, staff/branch/product/menu/table/custom-domain limits, feature entitlements, history/billing, grace period, expired behavior, platform admin, tenant self-service. No hard-coded pricing/entitlements. Subscription billing separate from order payments. Define expired subscription behavior without deleting data.

## 14. Notifications
Initial: in-app order indicators/status updates, customer contact via WhatsApp/phone, staff notification of new orders. Future: WhatsApp Business automation, email/SMS, preferences, retries/failure handling. External side effects must not compromise core transactions; durable async where justified.

## 15. Later capabilities
Basic inventory/stock deduction, purchases, expenses, low-stock alerts, advanced reports, loyalty/promotions, automated WhatsApp, advanced multi-branch, parcel pickup/delivery as separate future workflow reusing SaaS foundation. No hotel/accommodation management. No separate hotel/fast-food modules. No full accounting/ERP.

## 16. Initial exclusions
Full marketplace/commissions, hotel/accommodation, full accounting/ERP, payroll/HR, advanced warehouse, AI business assistant, advanced rider GPS, complex loyalty, franchise management, international tax automation, compulsory POS hardware, compulsory printer/tablet/scanner/kitchen hardware, compulsory WhatsApp Business API, full offline ordering without approved architecture. AI/OCR assists menu onboarding only, not required for operations.

## 17. Usability, accessibility, performance
Nontechnical owners; Android-first responsive; optional PWA; Urdu/English; large text/touch targets; simple language; icons with labels; minimal typing; confirm destructive/financial; clear errors/recovery; separate interfaces; lightweight pages/compressed images; efficient APIs/pagination; avoid reloads/animations; slow mobile internet; safe retries/idempotency; clear offline/network states; never show success unsaved. Performance: async server I/O, bounded queries/pagination, indexing by access patterns, short transactions, no DB connection/transaction held during external/idle, background processing, safe retry/idempotency, tenant-safe caching where justified, backups/recovery/monitoring/visibility.

## 18. Security and technical architecture
Modular monolith first; .NET 10; EF Core 10; PostgreSQL; React/TS/Vite/Tailwind/shadcn; provider-specific integrations behind replaceable adapters; country/currency/tax/address/payment config. Modules: Identity, Access, Tenant, Dastarkhwan business modules. Communicate via approved contracts/events; no private DbContext/table access; no circular deps; no SharedKernel; no SampleHost; no cross-module adopter/plugin; small module-owned contracts; outbox for durable external side effects where justified. Security: server-side authorization, tenant/store/branch ownership validation, fail-closed, no client-supplied tenant/store IDs trusted, secure auth/token handling, protect callbacks, validate input, rate-limit abuse, avoid sensitive logs, minimize personal data, audit important actions. Requirements are intended, not necessarily implemented.

## 19. Proposed roadmap
Phase 1 SaaS foundation; 2 Business/menu; 3 Public storefront/domain; 4 Cart/central ordering; 5 Takeaway/delivery; 6 Dine-in/service; 7 Billing/payments; 8 Staff/customers/reports; 9 Later capabilities; 10 Automation/expansion. Proposed sequence; inspect dependencies before changing status.

## 20. Decision conflicts
- Dine-in: CLAUDE.md excludes dine-in; new baseline includes dine-in. Existing accepted decision must be preserved unless Project Owner approves amendment; record proposed decision pending.
- Parcel: restaurant/food operations primary; parcel pickup/delivery separate future workflow reusing SaaS foundation; do not distort initial architecture.
- Other conflicts (verticals, backend-first, frontend timing, custom domains, entitlements, module ownership, DB architecture, shared contracts, outbox, WhatsApp, payment providers, security/MFA, multi-branch) classify as accepted/consistent, doc inconsistency, new proposed requirement, pending decision, or requires architecture assessment. Do not silently overwrite accepted decisions.
