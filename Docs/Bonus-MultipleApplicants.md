# Bonus: Multiple Applicants

An application can now have more than one applicant. Any applicant on it can view and edit it, and every ownership check applies to all of them. When two applicants edit at once, saves to different sections don't interfere with each other. When both save the same section, the second save is rejected as stale with a message to reload, instead of silently overwriting the first. There's no real-time sync: applicants see each other's changes when they reload.

## How it works

### Who's on an application

- **The applicant who starts an application is on it**, and so is anyone added later. They all have the same rights: view, edit both sections, add and remove residences, submit, withdraw, and add or remove applicants.
- **Adding an applicant:** the **Add applicant** button on the application page asks for the email of an existing **applicant** account. The email match ignores case and surrounding spaces. It's refused, with the reason under the email field, when:
  - there's no applicant account with that email (manager accounts count as "no applicant account"),
  - they're already on the application, or
  - they already have an open application for the same unit. This is the same "one open application per applicant and unit" rule that Apply follows.
- **Profiles:** adding someone who has never applied creates their applicant profile (blank, with their login email), the same way their first application would.
- **Removing an applicant:** any applicant on the application can remove another applicant, or **leave** it themselves (the button says "Leave" next to your own name). After leaving, you go back to your application list. The applicant who started the application can't be removed.
- **When it can change:** applicants can only be added or removed while the application can still be edited (Draft or Returned), the same rule as editing the sections. Managers and read-only applications just see the list.
- **Losing access:** a removed applicant gets a 404 on the application from then on, including from a page they still had open.
- **Apply reopens a shared application:** if an applicant presses Apply on a unit where they're already on an open application, even one someone else started, that application opens instead of a new one being created.

### Ownership

Every applicant-side query already starts from one ownership filter, `ApplicationService.Visible`. It used to allow only the applicant who started the application. It now allows any applicant on the application. Because everything goes through that filter (the editor, the section and residence saves, submit, withdraw, the application list and the modals), all the existing ownership checks now apply to every applicant on it, with no per-action changes. Someone who isn't on the application still gets a 404, exactly as before.

### Two applicants editing at once

Each section has its own version, stored on the application:

| Section | Version column | What changes it |
|---|---|---|
| Applicant Information | `ApplicantInformationVersion` | Continue on section 1 |
| Residence History | `ResidenceHistoryVersion` | Adding, editing or removing a residence, and Continue on section 2 |

- **Loading a form:** a form carries the version of its section from when it was loaded. The application page has hidden fields for both. Each residence modal and remove confirmation carries the version from when it opened.
- **Saving:** before saving, the service does a compare-and-swap on **only that section's** version. In the same transaction as the save, it runs `UPDATE RentalApplications SET <Section>Version = @new WHERE id = @id AND <Section>Version = @loaded`.
  - If it matches, the save goes ahead and commits together with the new version.
  - If it matches nothing, someone else saved that section after this page was loaded. Everything rolls back, nothing is written, and the applicant sees: *"Someone else saved this section after you opened it. Reload the page to see their changes, then try again."*
- **Different sections don't interfere.** A section 1 save only swaps the section 1 version, and a section 2 save only swaps the section 2 version. Two applicants saving different sections at the same moment both succeed. At most they briefly queue on the row lock.
- **Same section, second save is stale.** This covers both applicants pressing Continue on the same section, and editing the same residence. It also covers adding or removing a residence while the other applicant's residence modal is open, because any residence change is a save to Residence History.
- **A stale save stays stale until you reload.** After a stale save, the form keeps the old version it posted, so pressing Continue or Save again stays stale. Reloading picks up the other applicant's changes and the current version.
- **Your own consecutive saves don't trip each other.**
  - Residence History's hidden version sits inside the residence list, which redraws after every residence modal save, so it's always current.
  - When a residence modal stays open after a save with errors, it gets the section's new version back, so saving it again works.
- **Submit checks both sections.** The Summary posts both versions. If another applicant changed either section after the Summary was loaded, Submit is rejected: *"This application was changed by someone else after you opened the Summary. Reload the page and check it before submitting."* That way nobody submits changes they haven't seen.
- **Why these aren't EF concurrency tokens:** a token is checked on *every* update of the row. A section 1 save would then trip over a section 2 save that happened a moment earlier, which is exactly the interference this has to avoid. The service checks each section's version itself instead.
- **Other concurrency checks still apply.** The existing `Status` concurrency token still guards every save, so nothing is saved against an application that was just submitted, withdrawn or reviewed.

### The applicant profile

Section 1 is shared by everyone on the application. For a new application it's pre-filled from the **starter's** profile. So saving a valid section 1 now updates the starter's profile only when the starter saves it. Another applicant's save doesn't rewrite anyone's profile. Before this change, the application's own applicant always updated their profile.

## Data changes

### New table: `ApplicationApplicant`

One row per applicant per application. The starter always has a row.

| Column | Type | Notes |
|---|---|---|
| `RentalApplicationID` | `int`, part of the primary key | FK `FK_ApplicationApplicant_RentalApplications` to `RentalApplications.id`, cascade delete. Applications are never deleted by the app. |
| `ApplicantID` | `int`, part of the primary key | FK `FK_ApplicationApplicant_Applicant` to `Applicant.id`, no action. An applicant profile that's still on an application can't be deleted. |
| `Added` | `datetime`, not null | When they were added, in server local time. For the starter, when the application was created. |
| `AddedByUser` | `nvarchar(450)`, not null | Identity user id of whoever added them. For the starter, themselves. No FK, the same as the other "by user" columns. |

- **Primary key** `(RentalApplicationID, ApplicantID)`: an applicant can't be on the same application twice.
- **Index** `IX_ApplicationApplicant_ApplicantID`: the ownership filter looks up "applications this user is on" on every request.
- **Why a real entity:** it's a full entity rather than a hidden many-to-many join, so it can record who added whom and when.

### New columns on `RentalApplications`

| Column | Type | Notes |
|---|---|---|
| `ApplicantInformationVersion` | `uniqueidentifier`, not null | Section 1's version, as described above. New applications start with a random Guid. Existing rows get `00000000-…`, which works just as well since only changes matter. |
| `ResidenceHistoryVersion` | `uniqueidentifier`, not null | Section 2's version. Same as above. |

`RentalApplications.ApplicantID` is unchanged. It now means "the applicant who started it". The filtered unique index `IX_RentalApplications_OpenPerApplicantUnit` (one open application per applicant and unit) is still on that column, so the database enforces it for starters. For applicants who were added, the service enforces the same rule when adding them, and Apply reopens their shared application instead of creating another one.

### Migration

`Data/Migrations/20260930201707_MultipleApplicants` creates the table and index and adds the two version columns. It then **backfills** a row for every existing application's starter, so nobody loses access when ownership starts going through the new table:

```sql
INSERT INTO [ApplicationApplicant] ([RentalApplicationID], [ApplicantID], [Added], [AddedByUser])
SELECT r.[id], r.[ApplicantID], r.[Created], COALESCE(p.[UserId], N'')
FROM [RentalApplications] r JOIN [Applicant] p ON p.[id] = r.[ApplicantID]
```

A profile whose login was deleted has no `UserId`, so its `AddedByUser` is stored as `""`. As with the other migrations, `Program.CreateDatabase` applies it automatically on startup. `Down` drops the table and the columns, so added applicants are lost on a rollback. Starters keep their access through `ApplicantID`.

### Demo data

- Every seeded application has its starter in `ApplicationApplicant`, and random section versions.
- About one in five gets a second applicant, added by the starter an hour after it was created.
- On open applications, a second applicant is skipped if they already have an open application for that unit, the same rule the app enforces.

## Code changes

| File | Change | Why |
|---|---|---|
| `Models/ApplicationApplicant.cs` | **New** entity. | Who's on each application. |
| `Models/RentalApplication.cs` | Added `ApplicationApplicants`, `ApplicantInformationVersion` and `ResidenceHistoryVersion`. `ApplicantId` is now documented as the starter. The remarks explain why the versions aren't concurrency tokens. | Membership, plus a version per section. |
| `Models/Applicant.cs` | Added `ApplicationApplicants` (every application they're on). `RentalApplications` is now documented as the ones they started. | Navigation from the applicant's side. |
| `Data/ApplicationDbContext.cs` | Added the `ApplicationApplicants` DbSet and mapping: table, composite key, index, FKs. | Code-first schema. |
| `Data/Migrations/20260930201707_MultipleApplicants.cs` (+ `.Designer.cs`), `ApplicationDbContextModelSnapshot.cs` | **New** migration and updated snapshot. The backfill `INSERT` was added by hand. | Applies the schema change on startup and keeps existing access. |
| `Services/ApplicationService.cs` | **Ownership:** `Visible` filters on membership. **Start:** `StartAsync` reopens any open application for the unit that you're on, puts the starter on new applications, and gives new applications random versions. **Profiles:** new `GetOrCreateProfileAsync`, shared by Start and adding an applicant. **Editor:** `GetEditorAsync` returns the applicants (starter first, with emails) and both versions. **Section saves:** `SaveApplicantInformationAsync`, `SaveResidenceHistoryAsync`, `SaveResidenceAsync` (via `ResidenceViewModel.SectionVersion`) and `DeleteResidenceAsync` take the loaded version and save through the new `SaveSectionsAsync`, which does the per-section compare-and-swap and the save in one transaction. `SaveResidenceAsync` returns the new version. `GetResidenceAsync` includes the current version. **Submit:** `SubmitAsync` checks both versions. **Profile update:** only the starter's section 1 saves update their profile. **Applicants:** new `AddApplicantAsync` and `RemoveApplicantAsync`. Added the `SectionChangedMessage` and `ChangedBeforeSubmitMessage` constants and the `OpenStatuses` list. | Every rule stays in the service, and the one ownership filter covers all applicants. |
| `Services/ServiceResult.cs` | Added `Version`, and `Saved(..., version)`. | Lets the residence modal keep saving after its own save. |
| `ViewModels/ApplicationEditorViewModel.cs` | Added `ApplicantInformationVersion` and `ResidenceHistoryVersion` (bound from hidden fields) and `Applicants` (`[BindNever]`). | Carries the versions round trip, and shows the applicants. The versions are only concurrency checks and grant nothing, so binding them is safe. |
| `ViewModels/ResidenceViewModel.cs` | Added `SectionVersion` (hidden field). | The residence modal's version. |
| `ViewModels/ApplicationApplicantViewModel.cs` | **New.** `ApplicationApplicantViewModel` (a row in the panel) and `AddApplicantViewModel` (the add form: required email). | The applicants panel and the add modal. |
| `Controllers/ApplicationsController.cs` | **Section saves:** `Edit` POST passes the posted versions to Continue and Submit. `Residence` GET/POST and `DeleteResidence` carry the Residence History version; the remove confirmation posts it in its URL. `EditableAsync` replaces `CanEditAsync` and also supplies the version. **Applicants:** new `Applicants` (panel refresh), `AddApplicant` GET/POST, and `RemoveApplicant` GET/POST. Removing yourself redirects to your application list. | Thin actions over the service, following the existing modal pattern. |
| `Controllers/AppController.cs` | Added `ModalRedirect(url)`. | After leaving an application, the page can't be refreshed, so the modal sends you to your list instead. |
| `Views/Applications/Edit.cshtml` | Added a hidden `ApplicantInformationVersion` field (outside the disabled fieldset, so it's posted from the Summary too) and the `_Applicants` panel. | Version round trip, and the applicants list. |
| `Views/Applications/_ResidenceHistory.cshtml` | Added a hidden `ResidenceHistoryVersion` field inside `#residence-history`. | It refreshes with the list, so your own residence saves don't make your Continue or Submit look stale. |
| `Views/Applications/_ResidenceForm.cshtml` | Added a hidden `SectionVersion` field. | The modal's version. |
| `Views/Applications/_Applicants.cshtml` | **New.** The applicants panel (`#application-applicants`): each applicant with "(started it)" and "(you)" tags, Remove or Leave buttons, and Add applicant. | Shows and manages who's on the application. |
| `Views/Applications/_AddApplicantForm.cshtml` | **New.** The add-applicant modal. | Uses the existing `site.js` modal handling. |
| `wwwroot/js/site.js` | A JSON result with `redirectUrl` now navigates to that page. | Supports `ModalRedirect`. |
| `Data/DemoDataSeeder.cs` | Starters are on their applications, versions are set, and some applications get a second applicant. | Demo data the app could actually produce, and something to see in the UI. |

## Tests

232 tests, all passing, up from 205.

- **`Services/MultipleApplicantsTests.cs`** (**new**, 26 tests):
  - **Who's on it:**
    - The starter is on a new application.
    - An added applicant can view, edit, submit and withdraw it.
    - Someone not on it still gets not-found everywhere (view, list, saves, withdraw, adding applicants).
    - The editor lists the applicants, starter first, with the "you" flag.
    - Apply reopens a shared application.
  - **Adding and removing:**
    - Unknown and manager emails are refused, as is someone already on the application or someone with their own open application for the unit.
    - The email match ignores case and spaces.
    - A missing profile is created.
    - Nobody can be added once the application is submitted.
    - A removed applicant loses access, even from a page they still have open.
    - You can leave an application, but the starter can't be removed.
    - Only the starter's saves update the pre-fill profile.
  - **Two applicants saving:**
    - Different sections both save, one after the other and at the same instant (the second case fakes the other save landing mid-transaction).
    - For the same section, the second save is stale: Applicant Information, editing a residence, adding a residence, removing a residence, and Continue on Residence History.
    - Your own consecutive residence saves work using the returned version.
    - A stale save changes nothing and works after reloading.
    - Submit is stale after another applicant changes a section.
- **`Services/ApplicationServiceTests.cs`**: the existing calls go through small helpers that save at the current version, like a freshly loaded page. The interceptor tests pass versions explicitly. The queue test uses the new shared third applicant.
- **`Data/DemoDataSeederTests.cs`**: every application has its starter on it, some have a second applicant, and nobody is on two open applications for the same unit.
- **`TestDatabase.cs`**: added `ThirdApplicantUser`, real roles for every test user, and normalized emails, so looking up an applicant by email works the same as in the app.

## Limits

- **Adding by email reveals whether an applicant account exists** for that email. That's unavoidable when inviting by email into existing accounts; the message doesn't say anything more than that.
- **For added applicants, the one-open-application-per-unit rule is checked in code, not by a database index.** The existing unique index only covers the starter. Two simultaneous requests could, in theory, both add the same person to two different open applications for one unit.
- **Only tested on SQLite.** The concurrency tests run on SQLite. The SQL Server behavior (row locks on the compare-and-swap) is the standard `UPDATE ... WHERE` pattern, but it hasn't been exercised against the real database here.
- People can be added to an application without agreeing to it (AddApplicantAsync). Applicant A can put B on A's draft. After that:
   - B sees A's details.
   - If B clicks Apply on that unit, StartOnceAsync (line 137) sends B back to A's application instead of creating their own, until B leaves it.
Race conditions not backed by the database (low)
- Only the starter is covered by the one-open-application-per-unit rule (ApplicationDbContext.cs:238). The unique index is on RentalApplications(ApplicantId, UnitId), which records only the starter. For added applicants the rule is a check in code (AddApplicantAsync, line 490). If two applications add the same person for the same unit at the same moment, both can succeed.