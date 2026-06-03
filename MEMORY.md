# Project Memory - CallUp Platform

This document maintains a persistent history of development, architectural decisions, and milestones reached.

## 1. Project Overview
- **Core Stack**: ASP.NET MVC 5 (.NET 4.7.2), SQL Server, Vanilla CSS (Premium Aethestics).
- **Branding**: Rebranded from "OnCall" to **CallUp**.
- **Roles**: Customer, Service Provider (Supplier), Administrator.

## 2. Completed Milestones

### Architectural Foundation
- **Identity & Auth**: Custom SQL-based authentication with bcrypt hashing. Role-based authorization (`[AuthorizeRole]`).
- **Layout System**: Unified premium dashboard shells with sidebar navigation and glassmorphism styling.
- **Service Request Lifecycle**: `Moderation` -> `Open` -> `Bidding` -> `Accepted` -> `PendingPayment` -> `InProgress` -> `Completed`.

### Feature Stabilizations & Security
- **Admin Moderation**: Full request audit trail and user verification queues.
- **Provider Marketplace**: Real-time leads feed with Haversine distance calculations and bid competition analytics.
- **Bidding System**: ETA-based bidding with secure anti-forgery protection.
- **Stability**: Transitioned to strongly-typed ViewModels (`PendingRequestViewModel`) to prevent runtime binding errors.

### Visual Identity & UI Modernization (April 2026)
- **Branding Upgrade**: Official transition to **CallUp** name and domain (`callup.co.za`).
- **Design System**: Migrated to a "White and System Blue" minimalist theme. Removed all gradients, meshes, and blurs for a clean, professional aesthetic.
- **Logo Overhaul**: Implemented high-resolution, tightly-cropped branding assets with teal house icon and bold blue typography.
- **User Navigation**: Simplified headers and removed redundant marketing links to focus on core platform access.

### Marketing & Final Brand Assets (April 2026)
- **Logo Corrections**: Programmatically fixed legacy logo artifacts to clearly display the open "C" for CallUp without breaking existing site layouts.
- **Favicon & UI Integration**: Extracted the localized 'C' mark as a globally accessible favicon and surfaced the primary app logo within the `Index.cshtml` landing hero.
- **Ad Graphic Generation**: Consolidated and updated the master Facebook marketing directory (`MarketingBanners\`). Overwrote historical visual traces of "OnCall" / "CnCall" using C# compositing to inject the new CallUp brand correctly inverted over dark graphics.
- **Social Media Pack**: Deployed a fully-featured, ultra-realistic "3 Simple Steps" acquisition ad and authored multi-variant social copy (Direct, Trust Protocol, Provider Recruiting) to accompany it.

## 3. Configuration & Secrets
- **Database**: `CallUpContext` (LocalDB).
- **Communication**: Integrated `EmailService` for customer/supplier notifications.
- **Branding Assets**: Located in `/Content/img/brand/`.
- **Favicon**: Minimalist bold "C" for CallUp.

---
---
*Last Updated: 2026-05-01*

## 4. Platform Stabilization & Security Reversion (May 2026)
- **Hashed Passwords**: Reverted from plain-text back to `PBKDF2` hashing using `System.Web.Helpers.Crypto`.
    - **Security**: All passwords are now salted and hashed. Plain-text troubleshooting mode is disabled.
    - **User Experience**: Users with plain-text passwords will be automatically upgraded to hashes upon their next successful login.
- **Admin Notifications**: Implemented email notifications for new user signups. Admin is notified of Name, Email, Role, and Phone number to facilitate manual approval.
- **Approval Workflow**: Fixed Admin Moderation queue to correctly show users where `IsApproved = 0`.
- **Notification Fix**: Maintained `<div id="notificationStack">` in `_Layout.cshtml` to ensure system toasts are visible.
- **Registration UX Overhaul**: Redesigned the registration flow to use a step-based card selection system.
    - **Clarity**: Users must now explicitly choose "Customer" or "Service Provider" via large interactive cards before the form is revealed.
    - **Visual Cues**: Each role has distinct icons and colored badges (Blue for Customers, Green for Providers) to prevent registration errors.
