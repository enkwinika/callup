# Oncall Platform Persistent Memory

This file serves as the core technical context for the Oncall Home Services Platform. It should be referenced in all future development sessions to ensure architectural consistency.

## Project Vision
Oncall is an on-demand marketplace for South African homeowners to find verified service providers (plumbers, electricians, etc.) via a competitive bidding system.

## 1. UI Architecture & Design System
- **Stack**: ASP.NET MVC 5, C#, Entity Framework 6, MSSQL.
- **Aesthetic**: Premium "Glassmorphism" with a dark sidebar and soft background gradients.
- **Font**: 'Outfit' (Google Fonts).
- **Core Files**:
    - `Views/Home/Index.cshtml`: Main landing with interactive service request modal.
    - `Views/Account/Login.cshtml` / `Register.cshtml`: Identity-based multi-role authentication.
    - `Content/Site.css`: Centralized design tokens and dashboard layout system.
    - `html/app.js`: Client-side interactivity and animations.
    - `Models/OnCallModels.cs`: EF Data Models and DbContext.

## 2. Dashboard Structure (Multi-page)
Each role (Customer, Provider, Admin) has a dedicated home dashboard and sub-pages for functional sections:
- **Customer Pages**: `dashboard-customer.html`, `customer-messages.html`, `customer-history.html`, `customer-profile.html`.
- **Provider Pages**: `dashboard-provider.html`, `provider-jobs.html`, `provider-earnings.html`, `provider-settings.html`.
- **Admin Pages**: `dashboard-admin.html`, `admin-users.html`, `admin-disputes.html`, `admin-analytics.html`.

## 3. Immediate Technical Roadmap
- [x] **Phase 1: Backend Integration**: ASP.NET MVC 5 with C#.
- [x] **Phase 2: Database**: MSSQL with EF6 (Identity-extended Schema).
- [x] **Phase 3: Service Request & Bidding Lifecycle**: End-to-end flow from request creation to quote generation.
- [ ] **Phase 4: Real-time**: SignalR for live bidding updates.
- [ ] **Phase 5: SA Compliance**: POPIA data encryption and PayFast integration.

## 4. Technical Architecture: Service Request Flow
- **ServiceRequests**: Extended with `Title, ServiceDate, PriceRange, SpecialNotes, SelectedProviderId`.
- **Bids**: Linked to `ServiceRequests` and `Users` (Providers). Supports multi-provider bidding and bid editing (upsert).
- **Quotes**: Automatically generated when a customer selects a bid.
- **Image Handling**: Up to 4 images per request, stored in `~/Uploads/ServiceRequests/` and indexed in `ServiceRequestImages`.
- **Competitive Bidding**: Providers see the lowest current bid to encourage competitive market rates.

## 5. Design Decisions
- **MVC Architecture**: Fully migrated from static HTML to a dynamic MVC 5 backend.
- **Data Persistence**: Uses a central `OnCallContext` connected to MSSQL.
- **User Session**: `UserId` is stored in `Session["UserId"]` for consistent cross-controller identification.
- **UI Logic**: AJAX-based bid selection and live search for providers.
- **Glassmorphism**: Maintained as the core design principle for a premium feel.
