# Bonus: Review Queue

Submitted applications now wait in a review queue. A property manager claims one before reviewing it, which moves it to a new **Under Review** status so no other manager picks it up. The manager then either completes the review (Approve, Return or Deny, as before) or releases it back to the queue without a decision.

## How it works

- **New status: Under Review.** Submitted now means "waiting in the queue". Claiming moves an application from Submitted to Under Review, and the Review button only appears once it's claimed.
- **Claim first, then review.** The state machine no longer allows Submitted → Approved/Returned/Denied. Every review goes Submitted → Under Review → outcome.
- **Only the claimer can complete it.** Another manager can see who has it and when they claimed it, but gets "This application is claimed by another property manager." if they try to review it.
- **Any manager can release.** Releasing puts it back to Submitted with no outcome and no comment. The manager who claimed it can release it, and so can any other manager, so a claim never gets stuck if its manager is away or leaves. The confirm dialog names the other manager when it isn't your claim, and the history row shows who released it.
- **Place in the queue is kept.** The queue is ordered by `Submitted` (the applicant's submit time), and claiming or releasing doesn't change it. A released application goes back to where it was, not to the end.
- **Applicants see the status, not the reviewer.** An applicant sees "Under Review" on their application and in their list. Who claimed it is only filled in for managers.
- **Applicants can still withdraw** while it's Under Review, the same as while it's Submitted. Withdrawing clears the claim.
- **Returned and resubmitted** applications come back into the queue unclaimed.

### The review queue page

A new **Review queue** link in the managers' menu (`/Applications/Queue`) has three lists:

| Section | What's in it | Actions |
|---|---|---|
| My claims | Under Review, claimed by you | Open, Release |
| Waiting for review | Submitted, oldest submission first | Claim (then opens the application) |
| Claimed by other managers | Under Review by someone else, with who and when | Release |

The application page also has **Claim for review** (when Submitted), **Release** (when Under Review) and **Review** (only when you hold the claim), plus a "Claimed by you / by *email* · *time*" line for managers.

### Two managers at once

- **Both claim the same application.** `RentalApplication.Status` is already a concurrency token, so both `UPDATE`s say `WHERE Status = 2`. The first wins. The second gets "This application was changed by someone else. Reload the page and try again." and nothing is written for it.
- **Release and re-claim in between (the ABA case).** Manager A has the review modal open. Meanwhile B releases it and C claims it. The status is Under Review both times, so checking the status alone would let A's approval through on C's claim. To prevent that, `ReviewerUser` is now a concurrency token too. A's `UPDATE` includes `WHERE ReviewerUser = 'A'`, matches nothing, and the whole review transaction (lease included) rolls back.
- **Review and release at the same time.** Both change the status from Under Review, so whichever saves second fails the concurrency check.

## Data changes

### New status: `UnderReview = 7`

- Added to the `ApplicationStatus` enum with `[Display(Name = "Under Review")]`. It goes on the end, as the enum's comment requires, so no existing values change.
- **No manual data step is needed for the `Status` lookup table.** `Program.SeedLookupsAsync` adds any enum value that's missing on startup, so row `7, "UnderReview"` is inserted the first time the app runs after the upgrade.

### New columns on `RentalApplications`

| Column | Type | Notes |
|---|---|---|
| `ReviewerUser` | `nvarchar(450)`, null | Identity user id of the manager who claimed it. Set only while Under Review. **EF concurrency token.** Shown as their email, or "(deleted user)" if the account is gone. |
| `ReviewClaimed` | `datetime`, null | When it was claimed, in UTC (same as the other timestamps). Set and cleared together with `ReviewerUser`. |

- **No foreign key to `AspNetUsers`**, the same as `ApplicationStatusHistory.ChangedByUser` and `ManagerNote.UpdatedByUser`. A `SET NULL` foreign key would break the check constraint below, and `NO ACTION` would block deleting a manager who holds a claim. Since any manager can release a claim, a deleted manager's claim can simply be released.
- Both columns are only written by `ApplicationService.ChangeStatus`, which sets them when moving to Under Review and clears them on any move out of it (review, release, withdraw). No other code touches them, so they can't drift from the status.

### New check constraint: `CK_RentalApplications_ReviewClaim`

```sql
([Status] = 7 AND [ReviewerUser] IS NOT NULL AND [ReviewClaimed] IS NOT NULL)
OR ([Status] <> 7 AND [ReviewerUser] IS NULL AND [ReviewClaimed] IS NULL)
```

A claim exists exactly while the application is Under Review. This backs up `ChangeStatus` in the database, the same way the other check constraints back up the view model rules.

### Index changes

| Index | Change | Why |
|---|---|---|
| `IX_RentalApplications_OpenPerApplicantUnit` | Filter changed from `[Status] IN (1, 2, 3)` to `[Status] IN (1, 2, 3, 7)` | Under Review is still an open application, so an applicant still can't have two open applications for the same unit. `StartAsync`'s "reopen the existing one" lookup includes Under Review too. |
| `IX_RentalApplications_Status_Submitted` | **New**, on `(Status, Submitted)` | Serves the queue query (by status, oldest submission first). |
| `IX_RentalApplications_Status` | **Dropped** by EF | The new index starts with `Status`, so it already covers the `FK_RentApplications_Status` lookups. |

### Migration

`Data/Migrations/20260930185846_ReviewQueue` adds the columns, the check constraint and the index changes. As with the other migrations, it's applied automatically on startup by `Program.CreateDatabase`. Existing rows all have a status other than 7 and null claim columns, so they already satisfy the constraint. Any applications that are Submitted at upgrade time just show up in the queue.

`Down` first moves any Under Review applications back to Submitted (`UPDATE ... SET Status = 2 WHERE Status = 7`) and then drops the columns and restores the old indexes, so a rollback doesn't leave applications in a status the old code doesn't know. History rows that mention status 7 and the `Status` lookup row are left alone.

### Demo data

`DemoDataSeeder` now sends every review through a claim (`Submitted → UnderReview → outcome`, with the claim and the outcome by the same manager). It adds paths that end Under Review (with `ReviewerUser`/`ReviewClaimed` set to match), that were claimed and released, and that were withdrawn while under review. Since a release also lands on Submitted, the seeder's "submit times" (used for `Submitted` and for the "never submit a leased unit" rule) now skip releases. This only affects new demo databases, because the seeder skips a database that already has properties.

## Code changes

| File | Change | Why |
|---|---|---|
| `Models/ApplicationStatus.cs` | Added `UnderReview = 7` with a display name. | The new status. |
| `Models/RentalApplication.cs` | Added `ReviewerUser` and `ReviewClaimed`. Updated the remarks about concurrency tokens. | Records who holds the claim and when. |
| `Data/ApplicationDbContext.cs` | Mapped the new columns, made `ReviewerUser` a concurrency token, added `CK_RentalApplications_ReviewClaim`, widened the open-application index filter, added `IX_RentalApplications_Status_Submitted`. | Code-first schema, like the rest of the model. |
| `Data/Migrations/20260930185846_ReviewQueue.cs` (+ `.Designer.cs`), `ApplicationDbContextModelSnapshot.cs` | **New** migration and updated snapshot. `Down` hand-edited to move Under Review back to Submitted. | Applies the schema change on startup. |
| `Rules/ApplicationWorkflow.cs` | Submitted now goes only to Under Review or Withdrawn. Under Review goes to Approved, Returned, Denied, Submitted (release) or Withdrawn. `CanReview` is now Under Review only. Added `CanClaim` and `CanRelease`. | The state machine is still the one source of truth for what's allowed, used by the service, the view model and the seeder. |
| `Services/ApplicationService.cs` | Added `ClaimAsync`, `ReleaseAsync`, `CanReleaseAsync` and `GetQueueAsync`. `CanReviewAsync` and `ReviewAsync` now require the caller to hold the claim. `ChangeStatus` sets and clears the claim with the status. `StartAsync` treats Under Review as open. `GetEditorAsync` fills in the reviewer for managers only. | All rules stay in the service: managers only, `Visible` for access, the workflow for status, and concurrency conflicts come back as a "changed by someone else" result. |
| `ViewModels/ApplicationEditorViewModel.cs` | Added `Reviewer`, `ReviewClaimed`, `ClaimedByMe` (all `[BindNever]`) and the computed `CanClaim` / `CanRelease`. `CanReview` now also needs `ClaimedByMe`. | Drives the buttons. The flags are worked out from server data, never posted. |
| `ViewModels/ReviewQueueViewModel.cs` | **New.** `ReviewQueueViewModel` (Mine / Waiting / ClaimedByOthers) and `ReviewQueueItemViewModel`. | The queue page's model. It's separate from the application list's items, so reviewer details never go into the applicant-facing list. |
| `Controllers/ApplicationsController.cs` | Added `Queue` (GET), `Claim` (POST), `Release` (GET confirm modal) and `Release` POST (`ReleaseConfirmed`). All `[Authorize(Roles = AppRoles.PropertyManager)]`. | Thin actions over the service. Claim is a plain post that redirects to the application page. Release uses the shared `_Confirm` modal, like Withdraw. |
| `Views/Applications/Queue.cshtml` | **New.** The three-section queue page. | The review queue itself. |
| `Views/Applications/Edit.cshtml` | Added the Claim for review / Release buttons and the "Claimed by" line. The status shows its display name. | Claim and release from the application page. |
| `Views/Applications/Index.cshtml` | The status column shows the display name. | Shows "Under Review" rather than "UnderReview". The filter dropdown already used display names. |
| `Views/Shared/Components/ApplicationHistory/Default.cshtml` | Shows status display names. | Same as above, in the history panel. |
| `Pages/Shared/_Layout.cshtml` | Added the **Review queue** menu link for managers. | Navigation. |
| `Data/DemoDataSeeder.cs` | Review paths go through Under Review, with new claimed, released and withdrawn-under-review paths. Claims and releases are by the claiming manager. Adds a `SubmitTimes` helper that skips releases. | Keeps demo data to paths the app could actually produce, and covers the new status. |

No changes were needed in `site.js`. The existing modal handling and `data-modal-url` cover the release confirmation.

## Tests

186 tests, all passing, up from 155. The 31 extra include new theory cases. Existing tests that reviewed an application now claim it first.

- **`Services/ApplicationServiceTests.cs`**:
  - Claiming moves the application to Under Review, records the manager and the time, writes a history row, and keeps the submit time.
  - An applicant can't claim. An already claimed application can't be claimed again, and the first claim is kept. Only Submitted applications can be claimed (drafts are not found, and Returned ones are rejected).
  - When two managers claim at the same time, only one wins. The other gets a conflict result.
  - A manager can't review another manager's claim. `CanReviewAsync` is only true for the manager who claimed it.
  - The ABA case: if the application is released and re-claimed by someone else before the review saves, the review conflicts and no lease is created.
  - Releasing puts it back to Submitted, clears the claim, keeps the queue position, writes a history row with no outcome, and lets it be claimed again. Another manager can release someone else's claim. Only managers can release, and only while it's Under Review.
  - Withdrawing while Under Review works and clears the claim. Starting an application for the same unit again reopens the one that's under review.
  - The editor shows the claim to managers (`ClaimedByMe`, `CanReview` and `CanRelease` are correct for the claimer and for another manager). The applicant sees Under Review, but the reviewer never appears in anything returned to them.
  - The queue splits applications into mine, waiting (oldest first) and others', leaves out decided ones, looks right from both managers' side, and is empty for applicants.
  - A resubmitted Returned application goes back into the queue unclaimed. The history test includes the Submitted → Under Review step.
- **`Rules/ApplicationWorkflowTests.cs`**: the new transitions, the removed direct Submitted → outcome transitions, `CanReview`, `CanClaim` and `CanRelease`, Under Review isn't editable and isn't terminal.
- **`Data/CheckConstraintTests.cs`**: Under Review without a reviewer is rejected, and a reviewer on a status other than Under Review is rejected.
- **`Data/DemoDataSeederTests.cs`**: claims and releases are by managers, every review follows a claim by the same manager, open claims match the history, and nothing else has a claim. The submit checks skip releases.
- **`TestDatabase.cs`**: added a second manager (`OtherManagerUser`) for the multi-manager tests.
