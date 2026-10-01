# Bonus: Save Sections With Errors

Applicants can now save a section even when it fails validation. The errors show under the fields they belong to, the Summary lists everything still blocking submission, and Submit stays blocked while any error remains. Each section's rules are defined once and used everywhere.

## How it works

- **Continue always saves.** If the section is clean, it moves on to the next section as before. If not, it stays on the section with each error under its field, a "Saved, but some fields still need fixing before you can submit." message, and a **Next** button to move on anyway.
- **Errors come from what's saved.** Every time the page loads, the rules run against the stored data. So the errors are still there after a refresh or a later visit, and they go away as soon as the saved data is fixed.
- **The residence modal saves anything.** Blank fields, a bad phone number and move-out before move-in all save. If errors remain, the modal stays open on the saved residence with the errors under their fields, and the residence list behind it updates. Saving again edits the same residence instead of adding a second one. If the residence is clean, the modal closes as before.
- **Residence errors show on the row.** Each residence with errors has a row of messages under it in the residence table, on section 2 and on the Summary. Opening its Edit modal shows the errors under the fields straight away.
- **Section 2 can be saved with no residences.** It saves with the error "Add at least one prior residence.", shown above the table. Removing the last residence leaves the section saved with that same error, instead of marking the section unsaved.
- **The Summary lists every blocker.** In section order: sections not saved yet, every error on a saved section (for example "Applicant information: The Phone field is not a valid phone number." or "Residence history: 5 Elm St - The Landlord phone field is required."), and the unit having an active lease. On the Summary, section 1's errors also show under its read-only fields.
- **Submit uses the same list.** `SubmitAsync` builds the blockers with the same code as the Summary, so the page and the server can't disagree. The Submit button stays disabled while the list isn't empty, and a hand-crafted post is refused with the same messages.

### When errors count

- **Section 1 (Applicant Information):** its field errors count once the section has been saved. Before that, its only blocker is "Applicant information hasn't been saved yet." The pre-filled values aren't flagged, because they haven't been saved yet.
- **Residences:** each residence is saved on its own from the modal, so its errors count as soon as it's saved, even before section 2 has been saved with Continue.
- **Managers never see field errors.** Errors and blockers are only worked out while the applicant can still edit (Draft or Returned). A submitted application can't have any, since errors block Submit.

### The one thing that still stops a save

Text longer than 50 characters isn't saved, because the database columns (`nvarchar(50)`) can't hold it. The form comes back with what was typed and the error under the field. In practice only a hand-crafted post can reach this, because the inputs already stop typing at 50.

### The applicant profile

Saving section 1 used to copy the details into the applicant's profile, which pre-fills their next application. That copy now only happens when the section has no errors, so a half-finished section isn't carried into the next application.

## Rules defined once

Each section's rules live in one place, on its view model:

| Section | Where the rules are |
|---|---|
| Applicant Information | The DataAnnotations on `ApplicantInformationViewModel` (required, phone, email, length). |
| Each residence | The DataAnnotations on `ResidenceViewModel`, plus its `IValidatableObject` rule (move-out on or after move-in, reported on `MoveOutDate`). |
| Residence History | "Add at least one prior residence" (`SubmissionRules.NoResidences`). |

The same rules drive:

1. **The browser messages.** jQuery unobtrusive validation still shows messages as you type. It no longer stops the post: Continue has `formnovalidate`, and the residence form has `data-save-invalid`.
2. **The save.** The service runs the rules on what it saved and returns what's still wrong.
3. **The fields on the page, the residence rows and the modal.**
4. **The Summary's blocker list and Submit.**

`Rules/SectionValidator.cs` is the one piece of code that runs them. It returns a `FieldError(Field, Message, PreventsSave)` for each problem, keyed by the field's ModelState name (`ApplicantInformation.Phone`, `MoveOutDate`, and so on), so each message goes to the right input. It differs from .NET's `Validator.TryValidateObject` in two ways:

- It **always runs the `IValidatableObject` rule.** `TryValidateObject` skips that rule whenever any field has an error, which would hide "move-out before move-in" until an unrelated blank field was filled in.
- It **reports the first failing rule per field, with Required first.** That matches what MVC shows under a field.

`PreventsSave` is true only for a length rule (`StringLength`/`MaxLength`), which is the "too long for the column" case above.

## Data changes

### `Residence`

| Column | Before | After | Why |
|---|---|---|---|
| `MoveInDate` | `date NOT NULL` | `date NULL` | A residence can be saved without its dates. |
| `MoveOutDate` | `date NOT NULL` | `date NULL` | Same. |

- **Dropped check constraint `CK_Residence_Dates`** (`[MoveOutDate] >= [MoveInDate]`). A residence with move-out before move-in can now be saved and fixed later. The rule still applies: it's `ResidenceViewModel`'s `IValidatableObject` rule, and it blocks Submit, so no submitted application can break it.
- **Text columns are unchanged.** `Address`, `LandlordName` and `LandlordPhone` are still `NOT NULL`. A blank value is stored as `""`, and the Required rule catches it.

### `ApplicantInformation`

No schema change. Blank fields are stored as `""` (the columns stay `NOT NULL`), and the Required rules catch them.

### `RentalApplications`

No schema change, but `ApplicantInformationSaved` and `ResidenceHistorySaved` now mean "saved at least once", not "saved and valid". Whether a section is clean is always worked out from the saved data, and never stored.

### Migration

`Data/Migrations/20260930194904_AllowSectionsSavedWithErrors` drops `CK_Residence_Dates` and makes both date columns nullable. As with the other migrations, `Program.CreateDatabase` applies it automatically on startup. Existing rows already satisfy the old rules, so they keep working and show no errors.

`Down` was hand-edited to fix rows that would break the old schema before restoring it:

1. A missing date takes the other date, or today if both are missing.
2. A move-out date before the move-in date is set to the move-in date.

It then makes the columns `NOT NULL` again and re-adds `CK_Residence_Dates`. Only unsubmitted drafts can have rows like these.

## Code changes

| File | Change | Why |
|---|---|---|
| `Rules/SectionValidator.cs` | **New.** `FieldError` record and `SectionValidator.Validate(model, prefix)`. | The one runner for each section's rules, returning errors keyed by field. |
| `Rules/SubmissionRules.cs` | `GetBlockers` now also takes the Applicant Information errors and the residence errors, and lists each one under its section. The "not saved", "no residences" and "unit leased" blockers are unchanged. | One list of everything blocking Submit, used by the Summary and by `SubmitAsync`. |
| `Services/ServiceResult.cs` | Added `Unresolved` (errors left on a successful save), `Saved(unresolved, id)` and `Invalid(errors)` (not saved, one error per field). | Lets a save succeed and still report its errors, separately from a real failure. |
| `Services/ApplicationService.cs` | **`CheckSections`** runs every section's rules on the saved application and returns the section 1 field errors, the residences (each with its errors), the section 2 error and the blockers. `GetEditorAsync` and `SubmitAsync` both use it. **`SaveApplicantInformationAsync`** saves whatever was posted (trimmed, blank as `""`), returns the remaining errors, and only updates the profile when the section is clean. **`SaveResidenceHistoryAsync`** no longer rejects zero residences and returns the section's errors. **`SaveResidenceAsync`** saves anything and returns the residence's id and errors. **`GetResidenceAsync`** returns the residence with its errors. **`DeleteResidenceAsync`** no longer marks the section unsaved. Added the `TooLongToSave`, `Clean` and `ToViewModel` helpers, and the `ApplicantInformationPrefix` and `ResidencesKey` constants. | Keeps every rule decision in the service, and makes the page and Submit use the same checks. |
| `Models/Residence.cs` | `MoveInDate`/`MoveOutDate` are now `DateOnly?`. | A residence can be saved without its dates. |
| `Models/ApplicantInformation.cs` | Doc comment only. | Explains that blanks are stored as `""`. |
| `Data/ApplicationDbContext.cs` | Removed `CK_Residence_Dates` from the `Residence` mapping. | Code-first schema. |
| `Data/Migrations/20260930194904_AllowSectionsSavedWithErrors.cs` (+ `.Designer.cs`), `ApplicationDbContextModelSnapshot.cs` | **New** migration and updated snapshot. `Down` hand-edited as described above. | Applies the schema change on startup. |
| `ViewModels/ApplicationEditorViewModel.cs` | Added `ApplicantInformationErrors`, `ResidenceHistoryErrors` (both `[BindNever]`) and `SectionHasErrors(section)`. | Carries the saved errors to the page, and tells it when to offer Next. |
| `ViewModels/ResidenceViewModel.cs` | Added `Errors` and `SavedWithErrors` (both `[BindNever]`). Updated the remarks. | Row errors in the table, and the "saved with errors" state of the modal. |
| `ViewModels/ApplicantInformationViewModel.cs` | Doc comment only. | Describes the new save flow. |
| `Controllers/ApplicationsController.cs` | **`Edit` GET** puts the saved section 1 errors into ModelState. **`Edit` POST "continue"** no longer checks ModelState; it saves, moves on if the section is clean, and otherwise redirects back to the same section with a message. **`Residence` GET** puts the residence's saved errors into ModelState. **`Residence` POST** no longer checks ModelState; a clean save closes the modal, and a save with errors redraws the form on the saved residence (new id, errors, "saved" flag). Removed the `SectionIsValid` helper. | Puts each error on its own field, and keeps Post/Redirect/Get for Continue. |
| `Views/Applications/Edit.cshtml` | Continue has `formnovalidate`. A **Next** link shows when the current section has saved errors. | The browser no longer blocks the save, and there's a way to move on. |
| `Views/Applications/_ApplicantInformation.cshtml` | Comment only. The existing `asp-validation-for` tags show the saved errors. | No markup change was needed. |
| `Views/Applications/_ResidenceHistory.cshtml` | Shows the section's error above the table, and each residence's errors in a row under it. | Errors next to what they belong to. |
| `Views/Applications/_ResidenceForm.cshtml` | Added `data-save-invalid`, the "Saved, but…" notice, and `data-refresh-target`/`data-refresh-url` when saved with errors. | The modal saves invalid input, stays open and refreshes the list behind it. |
| `Views/Applications/_Summary.cshtml` | Comment only. | The blocker list now includes field errors. |
| `wwwroot/js/site.js` | Skips the browser validity check for forms with `data-save-invalid`. After a 200 partial whose form has `data-refresh-*`, it redraws that region behind the open modal. | Supports saving with errors in the modal, and keeps the list in sync. |

## Tests

205 tests, all passing, up from 186.

- **`Rules/SectionValidatorTests.cs`** (**new**):
  - Clean sections have no errors, and errors are keyed with the prefix.
  - Messages use the display name ("Landlord phone"), and Required is reported first.
  - The move-out rule runs even when another field is blank.
  - Only a too-long value sets `PreventsSave`.
- **`Rules/SubmissionRulesTests.cs`** (rewritten for the new signature):
  - The existing cases still hold.
  - Each error is listed under its section.
  - An unsaved section 1 only says "not saved", but residence errors count before section 2 is saved.
- **`Services/ApplicationServiceTests.cs`**:
  - An invalid section 1 saves and returns errors keyed by field, with blanks stored as `""`.
  - The editor puts the saved errors on `ApplicantInformation.*` fields, and the Summary lists them.
  - Submit is blocked while any error remains, then works once it's fixed.
  - Section 1 saved with errors doesn't overwrite the profile used for pre-fill.
  - Text that's too long isn't saved.
  - An invalid residence saves and returns its id and errors. Saving again with that id edits the same residence, and when fixed the errors clear.
  - A residence with no dates saves and reports both dates.
  - Residence errors show on the row and in the modal, and block Submit.
  - Continue on section 2 returns the residences' errors. Section 2 with no residences saves with the "add at least one" error. Deleting the last residence leaves the section saved with that error.
  - Clean sections have no errors, and managers never get field errors.
- **`Data/CheckConstraintTests.cs`**: the "residence can't end before it starts" test is replaced by one showing a residence with bad or missing dates can now be stored.
