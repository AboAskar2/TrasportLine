# Graduation project: private transport lines (monthly subscriptions)

## How to work with me (the student)
- Reply in Iraqi Arabic, short. One small step at a time. Do not over-explain.
- Every piece of code comes with: (1) a test for it, (2) a "للتقرير" part: what to write in the report (section, diagram, screenshot, test case).
- I am a university student: I know C#, ASP.NET, SQL at a decent level, and I must be able to explain every line to my supervisor.
- Write SIMPLE beginner-friendly code: short methods, clear English names, a short comment above each method saying what it does, no advanced patterns (no generic repositories, no MediatR, no heavy abstractions). Prefer plain code I can read over clever code.
- My supervisor wants to see visible progress every week.

## Stack (agreed with the supervisor)
- C#, ASP.NET Core MVC (.NET 8 LTS), MySQL 8.0.16+, EF Core with Pomelo.EntityFrameworkCore.MySql.
- Database-First: the database `line_subscription` ALREADY EXISTS. Do not create a new one and do not use migrations. Generate Models from it (scaffold).
- The old Django project is abandoned; ignore it.

## The idea
Drivers publish a FIXED line (stops, days, departure time). Passengers subscribe monthly (minimum one month) between two stops. Payment is cash outside the system (no payments table).

## Database: 14 tables
Roles, Users, Drivers, VehicleTypes, Vehicles, Areas, Stops, `Lines`, LineDays, LineStops, LinePrices, Subscriptions, Ratings, Notifications.
SQL scripts: 01_create_schema.sql (tables), 02_schema_tests.sql (8 tests, all PASS), 03_sample_data.sql (fake data).
Note: `Lines` is a reserved word in MySQL; the table is always quoted with backticks in SQL.

## Business rules (already enforced in the DB where possible)
1. The driver sets a monthly price per (FromStop, ToStop) pair in LinePrices. Price, stops and days are FIXED while the line is active; to change them the driver closes the line and creates a new one.
2. Subscription statuses: Pending -> Active (driver approves, ApprovedAt set) or Rejected (RejectionReason); Active -> Cancelled, or Expired automatically after EndDate.
3. A Pending request does NOT hold seats. Seats are checked when the driver approves, inside a database transaction that locks the Lines row.
4. Capacity is per SEGMENT between consecutive stops: occupancy of a segment = sum of SeatsCount of Active subscriptions (overlapping dates) whose pickup order <= segment start and dropoff order >= segment end. A subscription A->C needs free seats in both A-B and B-C.
5. A passenger may have only ONE open (Pending/Active) subscription per line (OpenKey generated column + unique key).
6. Pickup and dropoff stops must belong to the same line (composite FK to LineStops) and dropoff must come after pickup (trigger).
7. The driver can broadcast a message to all subscribers of a line (e.g. holiday, car breakdown); stored in Notifications with Type 'LineMessage'.
8. Passwords are stored as bcrypt/Argon2 hashes in Users.PasswordHash, hashed in the application code, never in plain text.
9. Soft delete (IsDeleted / status columns), no hard deletes of users.

## Key table columns (for reference)
- Users(UserID, RoleID, FullName, PhoneNumber UQ, Email UQ null, PasswordHash, Gender, ProfilePhotoURL, IsPhoneVerified, AccountStatus Active/Suspended/Banned, CreatedAt, LastLoginAt, IsDeleted)
- Drivers(DriverID, UserID UQ, LicenseNumber, LicenseExpiryDate, NationalIDNumber, LicensePhotoURL, IDPhotoURL, VerificationStatus Pending/Approved/Rejected, VerifiedByUserID, VerifiedAt, RejectionReason)
- Lines(LineID, DriverID, VehicleID, LineName, DepartureTime, TotalSeats, MinSubscriptionMonths, LineStatus Open/Suspended/Closed, Notes, IsWomenOnly, CreatedAt)
- LineStops(LineStopID, LineID, StopID, StopOrder, EstimatedArrivalTime); LinePrices(LineID, FromStopID, ToStopID, MonthlyPrice)
- Subscriptions(SubscriptionID, LineID, PassengerID, PickupStopID, DropoffStopID, SeatsCount, MonthlyPrice snapshot, StartDate, EndDate, SubscriptionStatus, ApprovedAt, RejectionReason, CreatedAt, CancelledAt, CancelReason)

## Registration rules
- The registration form has a choice: Passenger or Driver. There is NO Admin option; the Admin account is created only by a seed script / manually in the database. The server must also reject RoleName = Admin even if someone edits the form.
- If the user picks Driver, the form shows extra fields (from the Drivers and Vehicles tables): LicenseNumber, LicenseExpiryDate, NationalIDNumber, license photo, ID photo, and the vehicle data (type, plate number, brand, model, color, year, seat capacity, inspection expiry date, registration photo, air conditioning). If Passenger, these fields are hidden.
- Saving a driver creates Users + Drivers (+ Vehicles) rows in ONE database transaction. VerificationStatus starts as 'Pending'; the driver cannot publish a line until the Admin approves.
- Passwords are hashed with bcrypt before saving.

## Folder structure (keep it simple)
TransportLines/
- Controllers/   one controller per feature: AccountController, DriverController, PassengerController, AdminController
- Models/        the entity classes generated from the database (scaffold). Do not edit by hand.
- Data/          AppDbContext
- ViewModels/    classes that match the forms (RegisterViewModel, LoginViewModel, ...)
- Services/      the business logic (AccountService, SubscriptionService, ...), so controllers stay thin
- Views/         one folder per controller, plus Shared/
- wwwroot/       css, js, and uploads/ (license, ID, vehicle photos)
- sql/           01_create_schema.sql, 02_schema_tests.sql, 03_sample_data.sql
- Tests/         tests for each step

## Definition of done for EVERY step (very important)
1. Every feature must come with a working PAGE (Controller + View + ViewModel) so I can open it in the browser and try it myself. No feature is "done" without a page I can test.
2. Strong validation on EVERYTHING, in two layers:
   - Server side (always, cannot be bypassed): Data Annotations on ViewModels + extra checks in Services (phone format, unique phone/email, password strength, dates in the future/past, seat counts, price > 0, file type and size for uploads, role cannot be Admin, a user can only touch their own data).
   - Client side (friendly): jQuery Validation / unobtrusive so the user sees errors before submitting.
3. User-friendly UI: clear labels, placeholders, helpful error messages in plain language next to each field (Arabic or English, consistent), success messages after saving, confirm before dangerous actions (cancel, reject), responsive layout, RTL-friendly if the UI is Arabic, and the form keeps the user's input when there is an error.
4. Before telling me a step is finished, YOU must check it: run `dotnet build` and the tests, fix errors, and list what you tested. Add automated tests (xUnit) for services/validation, plus a short manual test list I can follow in the browser (input -> expected result).
5. End each step with a short "للتقرير" section: what to write, which screenshot to take, which test cases to include.

## Security rules (SQL injection etc.)
- NEVER build SQL by concatenating strings. Use EF Core LINQ queries (they are parameterized automatically). If raw SQL is unavoidable, use FromSqlInterpolated / parameters only, never FromSqlRaw with string concatenation.
- The app connects to MySQL with a LIMITED user (SELECT/INSERT/UPDATE/DELETE on line_subscription only), not root. Connection string goes in user-secrets / appsettings, never in the code or in git.
- Razor encodes output by default (prevents XSS): do not use Html.Raw on user input. Every POST form uses [ValidateAntiForgeryToken] (CSRF).
- Authorization on every action: [Authorize(Roles = ...)] and check that the record belongs to the logged-in user (no changing IDs in the URL to see someone else's data).
- Uploads: allow only image types, limit the size, rename the file with a random name, store outside executable folders.
- Lookup data (Roles, VehicleTypes, Areas, Stops) lives in tables, not hard-coded, so the admin can add new values without changing code.

## Changing the database later
- If a feature needs a new column/table, do NOT edit 01_create_schema.sql and do NOT touch the database silently.
- First STOP and tell me: what is missing, why, and the proposed change. Wait for my approval.
- After approval: write a numbered script in sql/ (04_add_xxx.sql, 05_...) using ALTER TABLE / CREATE TABLE, with a comment on what it does. I run it myself in MySQL Workbench.
- Then re-scaffold the Models (dotnet ef dbcontext scaffold ... --force), run build + tests, and tell me what to update in the report (ERD, data dictionary).
- Never use migrations and never DROP data.

## Roadmap (one small step per week, each with a test and a report section)
1. New MVC project + connect to the database + scaffold Models.
2. Register / login with bcrypt, roles (Passenger, Driver, Admin).
3. Driver profile + vehicle + admin approval of drivers.
4. Create a line (stops, days, prices).
5. Search lines and request a subscription (Pending).
6. Driver approves/rejects with the segment capacity check inside a transaction.
7. Auto-expiry, cancellation, driver broadcast messages, ratings.
8. Testing, documentation, final report.
