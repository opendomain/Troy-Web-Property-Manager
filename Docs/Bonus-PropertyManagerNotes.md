# Bonus: Property Manager Notes

Property managers can keep private notes on each rental application. The notes panel sits on the application page, above the status history, and is edited through a modal. Applicants never see the panel or the note text.

## How notes are kept away from applicants

- **Separate table.** Notes live in their own `ManagerNote` table, and an application record has no link to them (`RentalApplication` has no navigation property to `ManagerNote`). No query that loads an application for an applicant can pull notes in by accident. A test checks this.
- **Never in the applicant's page data.** The panel is a view component that loads its own data, like the history panel. Notes never go into `ApplicationEditorViewModel`, which is the data the application page is built from and what applicants get.
- **Role checked twice.** Every notes action is restricted to the Property Manager role, and the service method that reads or saves notes checks the role again. An applicant gets nothing back, even on their own application.
- **Leak test.** One test saves a note, then serializes everything the applicant-facing calls return and checks the note text isn't in it.

## Other behavior

- Notes can be edited in any status, including after an application is approved or denied. They're internal, so they stay useful after a decision.
- Only applications that have been submitted at least once have notes. Managers can't see never-submitted drafts, so those have no notes panel.
- The limit is 2000 characters, line breaks are kept, and saving an empty box clears the notes.
- If two managers edit at once, the second save gets "These notes were changed by someone else. Reload the page and try again." instead of overwriting the first manager's changes.

## Data changes

### New table: `ManagerNote`

One row per application, created the first time a manager saves notes.

| Column | Type | Notes |
|---|---|---|
| `RentalApplicationID` | `int`, primary key | Also the foreign key to `RentalApplications.id`. Sharing the key makes it one row per application. |
| `Notes` | `nvarchar(2000)`, not null | The note text. Empty string when cleared. |
| `UpdatedByUser` | `nvarchar(450)`, not null | Identity user id of the manager who saved last. Shown as their email, or "(deleted user)" if the account is gone. |
| `UpdatedDate` | `datetime`, not null | When it was last saved, in server local time (same as the history table). |
| `Version` | `uniqueidentifier`, not null | Concurrency token. Set to a new Guid on every save. |

- **Foreign key:** `FK_ManagerNote_RentalApplications`, cascade delete. Applications are never deleted by the app, so in practice this only matters for manual cleanup.
- **No navigation from `RentalApplication`.** The relationship is configured with `WithOne()` and no argument, so it can only be followed from the note to the application, never the other way.
- **Concurrency:** `Version` is an EF concurrency token that the app manages. The edit form posts back the version it was loaded with, and the service uses that as the original value, so the `UPDATE` only matches if nobody saved in between. The version is a Guid set in code rather than a SQL Server `rowversion`, so it works the same on SQLite, which the tests use.

### Migration

`Data/Migrations/20260930182537_AddManagerNotes` creates the table. As with the other migrations, it's applied automatically on startup by `Program.CreateDatabase`. Existing data isn't touched.

## Code changes

| File | Change | Why |
|---|---|---|
| `Models/ManagerNote.cs` | **New.** The note entity. | Notes get their own entity and table so they can't come along with an application. |
| `Data/ApplicationDbContext.cs` | Added the `ManagerNotes` DbSet and the `ManagerNote` mapping: table, column sizes, shared primary key, FK with no back-navigation, `Version` as a concurrency token. | Defines the schema code-first, like the rest of the model. |
| `Data/Migrations/20260930182537_AddManagerNotes.cs` (+ `.Designer.cs`), `ApplicationDbContextModelSnapshot.cs` | **New** migration and updated snapshot. | Creates the table on startup. |
| `ViewModels/ManagerNotesViewModel.cs` | **New.** Notes text (max 2000), `Version` (hidden field), plus display-only "last updated by / at". `ApplicationId` and the display fields are `[BindNever]`. | Kept separate from `ApplicationEditorViewModel`, which is what applicants get. The version lets the service detect a stale form. |
| `Services/ApplicationService.cs` | Added `GetManagerNotesAsync` and `SaveManagerNotesAsync`. | Keeps all the rules in the service, like the rest of the app: managers only, only applications the manager can see (`Visible`), editable in any status, concurrent saves return a "changed by someone else" result instead of overwriting. This is the only code that reads the table. |
| `ViewComponents/ManagerNotesViewComponent.cs` | **New.** Renders the notes panel. Returns nothing unless the user is a Property Manager. | Loads its own data, so the notes never pass through the page's view model. Same approach as `ApplicationHistoryViewComponent`. |
| `Views/Shared/Components/ManagerNotes/Default.cshtml` | **New.** The notes panel: text, "last updated" line, Edit button. Root element is `#manager-notes`. | Markup for the view component. The controller also returns it on its own to refresh the panel after a save. |
| `Views/Applications/_ManagerNotesForm.cshtml` | **New.** The edit modal: textarea, hidden `Version`, validation summary. | Follows the existing modal pattern: `site.js` loads and posts it, and errors redraw in place. |
| `Controllers/ApplicationsController.cs` | Added `ManagerNotes` (panel refresh), `EditManagerNotes` GET (form) and `EditManagerNotes` POST (save). All `[Authorize(Roles = AppRoles.PropertyManager)]`. | Thin actions that call the service, like the Review modal. An applicant calling them directly gets 403. |
| `Views/Applications/Edit.cshtml` | Added `<vc:manager-notes>` in the managers-only column, above the history panel. | Shows the panel on the application page for managers only. |
| `wwwroot/css/site.css` | Added `.manager-notes-text` (`white-space: pre-wrap`). | Keeps the line breaks managers type. The text is still HTML-encoded by Razor. |

No changes were needed in `site.js`. The existing modal and refresh handling covers the new form.

## Tests

13 new tests, all passing (155 total).

- **`Services/ApplicationServiceTests.cs`**:
  - A manager can save and read notes, with text trimmed and the last-updated email and time recorded.
  - A second save with the current version works.
  - An applicant can't read or save notes, even on their own application, and the stored note stays unchanged.
  - Nothing returned to the applicant contains the note text: the editor (default section and Summary), the application list and the history.
  - There's no navigation from `RentalApplication` to `ManagerNote`.
  - A save with a stale version, or from a form opened before the first note existed, is rejected and keeps the other manager's notes.
  - Notes stay editable after an approval.
  - Never-submitted drafts and missing applications return not found, and no row is created.
- **`ViewModels/ViewModelValidationTests.cs`**: blank and normal notes are valid, and over 2000 characters is invalid.
