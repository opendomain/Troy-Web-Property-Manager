using System.Data;
using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Data;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Services
{
    /// <summary>
    /// Everything you can do with a rental application: start it, edit the sections and residences, submit,
    /// withdraw, review, see the history, and list them.
    /// </summary>
    /// <remarks>
    /// <para>All the business rules from the assessment live here (or in the <c>Rules</c> classes this calls) - not in
    /// the controllers or views. That keeps the rules in one place, keeps the controllers thin, and lets us unit test
    /// the rules against a real database (the tests run these methods on in-memory SQLite).</para>
    /// <para>Each method takes a <see cref="CurrentUser"/> and does its own ownership check (<see cref="Visible"/>),
    /// so a slip in a controller still can't leak someone else's application. Writes re-check the status through
    /// <see cref="LoadEditableAsync"/>, and every status change goes through <see cref="ChangeStatus"/>, so disallowed
    /// posts get rejected (4.d) no matter which action they hit.</para>
    /// <para>On the EF side: reads use <c>AsNoTracking()</c> and <c>Select</c> projections so SQL only returns what
    /// the page needs. Filters are built up on an <see cref="IQueryable{T}"/> so they end up in the SQL WHERE (6.a).
    /// Writes load tracked entities and call <c>SaveChangesAsync</c> once, so each operation is a single atomic save.</para>
    /// <para>Two people can act on the same application at the same time (say the applicant withdraws while a manager
    /// approves). <c>RentalApplication.Status</c> is a concurrency token, so a save based on an out-of-date status fails
    /// instead of overwriting (see <see cref="SaveApplicationAsync"/>). Approvals also run in a serializable
    /// transaction so two of them can't both create a lease on the same unit.</para>
    /// </remarks>
    public class ApplicationService(ApplicationDbContext db)
    {
        /// <summary>
        /// Who can see what, in one spot: managers see every application, applicants only their own. Every query that
        /// touches an application starts here so no method can forget the ownership check. It returns an
        /// <see cref="IQueryable{T}"/>, so the filter ends up in the SQL WHERE clause.
        /// </summary>
        private IQueryable<RentalApplication> Visible(CurrentUser user)
        {
            return user.IsManager ? db.RentalApplications : db.RentalApplications.Where(a => a.Applicant.UserId == user.Id);
        }

        /// <summary>
        /// Is the unit leased today? If so it's not available (2.d). <see cref="LeaseRules.ActiveOn"/> is an
        /// expression so EF can turn it into SQL, and every availability query uses this same one.
        /// </summary>
        private Task<bool> UnitHasActiveLeaseAsync(int unitId)
        {
            return db.Leases.Where(l => l.UnitId == unitId).AnyAsync(LeaseRules.ActiveOn(DateTime.Today));
        }

        /// <summary>
        /// Saves changes to an application. Since Status is a concurrency token, if someone else changed the status
        /// after we loaded it (e.g. the applicant withdrew while a manager approved), nothing gets saved.
        /// </summary>
        private async Task<ServiceResult> SaveApplicationAsync()
        {
            try
            {
                await db.SaveChangesAsync();
                return ServiceResult.Ok();
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                return ServiceResult.Stale();
            }
        }

        // ---------------- Applicant ----------------

        /// <summary>
        /// Starts an application for a unit (Challenge a). Returns the id of either a brand new Draft or the
        /// applicant's existing open application for that unit.
        /// </summary>
        /// <remarks>
        /// The check-then-insert below can race if the same request comes in twice (someone double-clicks Apply).
        /// The unique indexes (one profile per user, one open application per applicant and unit) catch that, so the
        /// loser gets a unique-key error. Rather than show a 500, we retry once, and the retry just finds what the
        /// other request created.
        /// </remarks>
        public async Task<ServiceResult> StartAsync(int unitId, CurrentUser user)
        {
            try
            {
                return await StartOnceAsync(unitId, user);
            }
            catch (DbUpdateException ex) when (SqlErrors.IsUniqueViolation(ex))
            {
                // Another request (probably a double-click on Apply) beat us to creating the profile or application.
                // Try again and we'll just pick up what it made.
                db.ChangeTracker.Clear();
                return await StartOnceAsync(unitId, user);
            }
        }

        private async Task<ServiceResult> StartOnceAsync(int unitId, CurrentUser user)
        {
            if (user.IsManager) return ServiceResult.Forbid("Only applicants can apply for a unit.");
            if (!await db.Units.AnyAsync(u => u.Id == unitId)) return ServiceResult.Missing();

            // Already applied for this unit? Reopen that one instead of making a duplicate.
            var openId = await db.RentalApplications
                .Where(a => a.UnitId == unitId && a.Applicant.UserId == user.Id
                    && (a.Status == (long)ApplicationStatus.Draft || a.Status == (long)ApplicationStatus.Submitted || a.Status == (long)ApplicationStatus.Returned))
                .Select(a => (int?)a.Id)
                .FirstOrDefaultAsync();
            if (openId is int existing) return ServiceResult.Ok(existing);

            if (await UnitHasActiveLeaseAsync(unitId)) return ServiceResult.Error("This unit is not available.");

            // One profile per user (unique index on UserId) - create it on their first application.
            // It holds their latest details, which we only use to pre-fill section 1 on new applications.
            var applicant = await db.Applicants.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (applicant is null)
            {
                var email = await db.Users.Where(u => u.Id == user.Id).Select(u => u.Email).FirstOrDefaultAsync();
                applicant = new Applicant
                {
                    UserId = user.Id,
                    Name = "",
                    Phone = "",
                    Email = email is { Length: <= 50 } ? email : "",
                    CurrentAddress = ""
                };
                db.Applicants.Add(applicant);
            }

            var application = new RentalApplication
            {
                UnitId = unitId,
                Applicant = applicant,
                Status = (long)ApplicationStatus.Draft,
                Created = DateTime.Now
            };

            // First history row: who created it and when (5.c). PreviousStatus stays 0 ("none"), which the history
            // panel shows as "Created".
            application.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                NewStatus = (long)ApplicationStatus.Draft,
                ChangedByUser = user.Id,
                ChangedDate = application.Created
            });

            db.RentalApplications.Add(application);
            await db.SaveChangesAsync();
            return ServiceResult.Ok(application.Id);
        }

        /// <summary>
        /// Builds the view model for the application page (4.b). Returns null if the application doesn't exist or
        /// this user can't see it, and the controller turns that into a 404.
        /// </summary>
        /// <remarks>
        /// This is where the permission flags get decided, on the server (4.d). <c>CanEdit</c> and <c>IsReadOnly</c>
        /// come from the status and the user's role, never the request. The controller goes through here even when
        /// redisplaying a failed post, so posted data can't mess with the flags.
        /// </remarks>
        public async Task<ApplicationEditorViewModel?> GetEditorAsync(int id, ApplicationSection? section, CurrentUser user)
        {
            // Read-only, so AsNoTracking. The Includes pull in the related rows with joins in one trip instead of a
            // query per navigation (the classic N+1 problem).
            var application = await Visible(user).AsNoTracking()
                .Include(a => a.Unit).ThenInclude(u => u.Property)
                .Include(a => a.Applicant)
                .Include(a => a.ApplicantInformation)
                .Include(a => a.Residences)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (application is null) return null;

            // Decided here on the server (4.d): only the applicant can edit, and only while it's Draft or Returned.
            // (Visible() already made sure an applicant only gets here for their own application.)
            var canEdit = !user.IsManager && ApplicationWorkflow.IsEditable((ApplicationStatus)application.Status);
            // What's stopping Submit, so the Summary can list it. Only worth the lease query if they can edit.
            var blockers = canEdit
                ? SubmissionRules.GetBlockers(application.ApplicantInformationSaved, application.ResidenceHistorySaved,
                    application.Residences.Count, await UnitHasActiveLeaseAsync(application.UnitId))
                : [];
            // Editors start at section 1; everyone else lands on the read-only Summary.
            var current = section ?? (canEdit ? ApplicationSection.ApplicantInformation : ApplicationSection.Summary);

            // Use the application's own copy once it's saved; until then, pre-fill from the applicant's profile.
            var info = application.ApplicantInformation is { } saved
                ? (saved.Name, saved.Phone, saved.Email, saved.CurrentAddress)
                : (application.Applicant.Name, application.Applicant.Phone, application.Applicant.Email, application.Applicant.CurrentAddress);

            // If it was returned or denied the applicant needs to know why. The full history is still managers only.
            string? reviewComment = null;
            if (!user.IsManager && application.Status is (long)ApplicationStatus.Returned or (long)ApplicationStatus.Denied)
            {
                reviewComment = await db.ApplicationStatusHistories
                    .Where(h => h.RentalApplicationId == id && h.NewStatus == application.Status)
                    .OrderByDescending(h => h.ChangedDate).ThenByDescending(h => h.Id)
                    .Select(h => h.Comment)
                    .FirstOrDefaultAsync();
            }

            return new ApplicationEditorViewModel
            {
                Id = application.Id,
                Section = current,
                Status = (ApplicationStatus)application.Status,
                UnitLabel = $"{application.Unit.Property.Name}, unit {application.Unit.UnitNumber} ({application.Unit.MonthlyRent:C0}/month)",
                ApplicantInformation = new()
                {
                    Name = info.Name,
                    Phone = info.Phone,
                    Email = info.Email,
                    CurrentAddress = info.CurrentAddress
                },
                Residences = application.Residences.OrderByDescending(r => r.MoveInDate).Select(r => new ResidenceViewModel
                {
                    ApplicationId = application.Id,
                    ResidenceId = r.Id,
                    Address = r.Address,
                    LandlordName = r.LandlordName,
                    LandlordPhone = r.LandlordPhone,
                    MoveInDate = r.MoveInDate,
                    MoveOutDate = r.MoveOutDate
                }).ToList(),
                ApplicantInformationSaved = application.ApplicantInformationSaved,
                ResidenceHistorySaved = application.ResidenceHistorySaved,
                SubmitBlockers = blockers,
                CanEdit = canEdit,
                // The Summary is always read-only (4.a.iii). The other sections are read-only if you can't edit.
                IsReadOnly = !canEdit || current == ApplicationSection.Summary,
                IsManager = user.IsManager,
                ReviewComment = string.IsNullOrWhiteSpace(reviewComment) ? null : reviewComment
            };
        }

        /// <summary>
        /// Continue on section 1 (4.b.i): saves the Applicant Information and marks the section done. The controller
        /// only calls this after validation passes, but we still re-check ownership and status here, so a post
        /// against an already-submitted application gets turned away (4.d).
        /// </summary>
        public async Task<ServiceResult> SaveApplicantInformationAsync(int id, ApplicantInformationViewModel model, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;

            // The caller already validated the model, so the required fields are there.
            // This only saves to this application - other ones (like already submitted ones) aren't touched.
            var info = application!.ApplicantInformation ??= new ApplicantInformation();
            info.Name = model.Name!.Trim();
            info.Phone = model.Phone!.Trim();
            info.Email = model.Email!.Trim();
            info.CurrentAddress = model.CurrentAddress!.Trim();
            application.ApplicantInformationSaved = true;

            // Remember these details to pre-fill their next application.
            var defaults = application.Applicant;
            (defaults.Name, defaults.Phone, defaults.Email, defaults.CurrentAddress) = (info.Name, info.Phone, info.Email, info.CurrentAddress);
            return await SaveApplicationAsync();
        }

        /// <summary>
        /// Continue on Residence History (4.b.i). The modal already saved each residence (4.c), so this just checks
        /// there's at least one and marks the section done.
        /// </summary>
        public async Task<ServiceResult> SaveResidenceHistoryAsync(int id, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            if (application!.Residences.Count == 0) return ServiceResult.Error("Add at least one prior residence.");
            application.ResidenceHistorySaved = true;
            return await SaveApplicationAsync();
        }

        /// <summary>
        /// Gets one residence for the edit modal. We go through <see cref="Visible"/> and then to that application's
        /// residences, so a residence id from someone else's application just comes back empty.
        /// </summary>
        public Task<ResidenceViewModel?> GetResidenceAsync(int id, int residenceId, CurrentUser user)
        {
            return Visible(user).Where(a => a.Id == id).SelectMany(a => a.Residences).Where(r => r.Id == residenceId)
                .Select(r => new ResidenceViewModel
                {
                    ApplicationId = id,
                    ResidenceId = r.Id,
                    Address = r.Address,
                    LandlordName = r.LandlordName,
                    LandlordPhone = r.LandlordPhone,
                    MoveInDate = r.MoveInDate,
                    MoveOutDate = r.MoveOutDate
                }).FirstOrDefaultAsync();
        }

        /// <summary>Adds or edits a residence from the modal (4.c). Only while the application can still be edited.</summary>
        public async Task<ServiceResult> SaveResidenceAsync(int id, ResidenceViewModel model, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            // Look the residence up in this application's residences only, not by id across the whole table,
            // so a faked ResidenceId can't reach into another application.
            var residence = model.ResidenceId is null
                ? new Residence()
                : application!.Residences.FirstOrDefault(r => r.Id == model.ResidenceId);
            if (residence is null) return ServiceResult.Missing();
            residence.Address = model.Address!.Trim();
            residence.LandlordName = model.LandlordName!.Trim();
            residence.LandlordPhone = model.LandlordPhone!.Trim();
            residence.MoveInDate = model.MoveInDate!.Value;
            residence.MoveOutDate = model.MoveOutDate!.Value;
            if (model.ResidenceId is null) application!.Residences.Add(residence);
            GuardStatus(application!);
            return await SaveApplicationAsync();
        }

        /// <summary>Removes a residence from the modal (4.c). Only while the application can still be edited.</summary>
        public async Task<ServiceResult> DeleteResidenceAsync(int id, int residenceId, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            var residence = application!.Residences.FirstOrDefault(r => r.Id == residenceId);
            if (residence is null) return ServiceResult.Missing();
            application.Residences.Remove(residence);
            db.Residences.Remove(residence);
            // No residences left means the section isn't complete anymore, so Submit has to wait until it's saved again.
            if (application.Residences.Count == 0) application.ResidenceHistorySaved = false;
            GuardStatus(application);
            return await SaveApplicationAsync();
        }

        /// <summary>
        /// Submit from the Summary (Challenge a; 4.b.ii). Also handles resubmitting a Returned application once it's
        /// been fixed - Returned is editable, and Returned → Submitted is allowed.
        /// </summary>
        public async Task<ServiceResult> SubmitAsync(int id, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            // 4.b.ii / 4.e: both sections saved, and the unit not leased. The Summary shows the same list and the page
            // disables the button, but this is the check that actually matters. We leave any other open
            // applications for the unit alone.
            var blockers = SubmissionRules.GetBlockers(application!.ApplicantInformationSaved, application.ResidenceHistorySaved,
                application.Residences.Count, await UnitHasActiveLeaseAsync(application.UnitId));
            if (blockers.Count > 0)
            {
                return ServiceResult.Error(string.Join(" ", blockers));
            }
            ChangeStatus(application, ApplicationStatus.Submitted, user);
            application.Submitted = DateTime.Now;
            return await SaveApplicationAsync();
        }

        /// <summary>
        /// Withdraw (Challenge a). Works from Draft, Submitted or Returned - the state machine decides, so you can't
        /// withdraw something that's already final. If a manager changes the status at the same moment, the
        /// concurrency token makes this fail instead of overwriting (we don't want an approval with a lease quietly
        /// turning into Withdrawn).
        /// </summary>
        public async Task<ServiceResult> WithdrawAsync(int id, CurrentUser user)
        {
            var application = await Visible(user).FirstOrDefaultAsync(a => a.Id == id);
            if (application is null) return ServiceResult.Missing();
            if (user.IsManager || !ApplicationWorkflow.CanTransition((ApplicationStatus)application.Status, ApplicationStatus.Withdrawn))
            {
                return ServiceResult.Error("This application can't be withdrawn.");
            }
            ChangeStatus(application, ApplicationStatus.Withdrawn, user);
            return await SaveApplicationAsync();
        }

        // ---------------- Property manager ----------------
        /// <summary>True if the application exists and is waiting for review. Only reads the status.</summary>
        public async Task<bool> CanReviewAsync(int id, CurrentUser user)
        {
            if (!user.IsManager) return false;
            var status = await db.RentalApplications.Where(a => a.Id == id).Select(a => (long?)a.Status).FirstOrDefaultAsync();
            return status is long s && ApplicationWorkflow.CanReview((ApplicationStatus)s);
        }

        /// <summary>
        /// Finishes a review (5.a): Approve (creates a 12-month lease, 2.d), Return (sends it back to the applicant to
        /// fix) or Deny. Return and Deny need a comment. If we hit either concurrency problem, the user gets a
        /// friendly message instead of an exception.
        /// </summary>
        public async Task<ServiceResult> ReviewAsync(int id, ReviewViewModel model, CurrentUser user)
        {
            if (!user.IsManager) return ServiceResult.Forbid("Only property managers can review applications.");

            try
            {
                return await ReviewInTransactionAsync(id, model, user);
            }
            catch (Exception ex) when (SqlErrors.IsDeadlock(ex))
            {
                // Another review on the same unit or application got there first, and this one was rolled back.
                db.ChangeTracker.Clear();
                return ServiceResult.Stale("Another review of this unit was saved at the same time. Reload the page and try again.");
            }
            catch (DbUpdateConcurrencyException)
            {
                // The status changed after we read it (e.g. the applicant withdrew), so the transaction was rolled back.
                db.ChangeTracker.Clear();
                return ServiceResult.Stale();
            }
        }

        private async Task<ServiceResult> ReviewInTransactionAsync(int id, ReviewViewModel model, CurrentUser user)
        {
            // Serializable so two approvals for the same unit can't both get past the active-lease check.
            // If they collide, SQL Server kills one as a deadlock and ReviewAsync tells the user.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var application = await db.RentalApplications.FirstOrDefaultAsync(a => a.Id == id);
            if (application is null) return ServiceResult.Missing();
            if (!ApplicationWorkflow.CanReview((ApplicationStatus)application.Status))
            {
                return ServiceResult.Error("Only submitted applications can be reviewed.");
            }
            var outcome = model.Outcome!.Value;
            // Same rule as ReviewViewModel.Validate. We check it again here because the service shouldn't trust whoever called it.
            if (ApplicationWorkflow.RequiresComment(outcome) && string.IsNullOrWhiteSpace(model.Comment))
            {
                return ServiceResult.Error("A comment is required to return or deny an application.", nameof(model.Comment));
            }
            if (outcome == ReviewOutcome.Approve)
            {
                // 4.e: check again at approval so the unit can't get a second lease. (Other open applications for
                // the unit are left alone - they'll get rejected if they try to submit or get approved.)
                if (await UnitHasActiveLeaseAsync(application.UnitId))
                {
                    return ServiceResult.Error("This unit already has an active lease.");
                }
                var start = DateTime.Today;
                db.Leases.Add(new Lease
                {
                    UnitId = application.UnitId,
                    RentalApplicationId = application.Id,
                    StartDate = start,
                    EndDate = LeaseRules.EndDateFor(start)
                });
            }
            // The lease and the status change (plus its history row) are saved and committed together - either it's
            // approved and has a lease, or neither happened.
            ChangeStatus(application, ApplicationWorkflow.StatusFor(outcome), user, outcome, model.Comment?.Trim());
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult.Ok();
        }

        /// <summary>
        /// Status changes and reviews (who, when, comment) for the history panel (5.c). Managers only - and the role
        /// check is here, not just in the view component, so nothing else can accidentally leak it.
        /// </summary>
        public async Task<List<HistoryItemViewModel>> GetHistoryAsync(int id, CurrentUser user)
        {
            if (!user.IsManager) return [];

            // Left join to AspNetUsers to get the email of whoever made the change. If that user's been deleted we
            // still show the row, just marked as a deleted user.
            var rows = await (
                from h in db.ApplicationStatusHistories.AsNoTracking()
                where h.RentalApplicationId == id
                join u in db.Users on h.ChangedByUser equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby h.ChangedDate, h.Id
                select new { h.ChangedDate, ChangedBy = u != null ? u.Email : null, h.PreviousStatus, h.NewStatus, h.Outcome, h.Comment })
                .ToListAsync();

            return rows.Select(h => new HistoryItemViewModel
            {
                ChangedAt = h.ChangedDate,
                ChangedBy = h.ChangedBy ?? "(deleted user)",
                FromStatus = h.PreviousStatus == 0 ? null : (ApplicationStatus)h.PreviousStatus,
                ToStatus = (ApplicationStatus)h.NewStatus,
                Outcome = h.Outcome,
                Comment = h.Comment
            }).ToList();
        }

        // ---------------- List ----------------
        /// <summary>
        /// The application list (6.a). The filters are stacked onto the IQueryable so SQL does the work - each
        /// <c>Where</c> only gets added if that filter is set, and nothing runs until <c>ToListAsync</c>. Starting from
        /// <see cref="Visible"/> is what limits applicants to their own.
        /// </summary>
        public Task<List<ApplicationListItemViewModel>> ListAsync(ApplicationStatus? status, int? propertyId, CurrentUser user)
        {
            var query = Visible(user).AsNoTracking();
            if (status is not null) query = query.Where(a => a.Status == (long)status.Value);
            if (propertyId is not null) query = query.Where(a => a.Unit.PropertyId == propertyId);
            return query.OrderByDescending(a => a.Id).Select(a => new ApplicationListItemViewModel
            {
                Id = a.Id,
                PropertyName = a.Unit.Property.Name,
                UnitNumber = a.Unit.UnitNumber,
                // The email on this application, or the one from their profile if they haven't saved the section yet.
                Applicant = a.ApplicantInformation != null ? a.ApplicantInformation.Email : a.Applicant.Email,
                Status = (ApplicationStatus)a.Status,
                SubmittedAt = a.Submitted
            }).ToListAsync();
        }

        // ---------------- Helpers ----------------
        /// <summary>
        /// Loads an application so the applicant can change it - but only if it's theirs and still Draft or Returned.
        /// Every applicant write goes through here, so rejecting disallowed posts (4.d) happens in one place.
        /// Not theirs → NotFound (404). Not editable → an error message.
        /// </summary>
        private async Task<(RentalApplication? Application, ServiceResult? Error)> LoadEditableAsync(int id, CurrentUser user)
        {
            var application = await Visible(user)
                .Include(a => a.Applicant)
                .Include(a => a.ApplicantInformation)
                .Include(a => a.Residences)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (application is null) return (null, ServiceResult.Missing());
            if (user.IsManager || !ApplicationWorkflow.IsEditable((ApplicationStatus)application.Status))
            {
                return (null, ServiceResult.Error("This application can no longer be edited."));
            }
            return (application, null);
        }

        /// <summary>
        /// Residence changes only touch the Residence table, so on their own they'd never hit the Status concurrency
        /// check. Marking Status as modified makes EF update the application row too - with the same value, but
        /// "WHERE Status = &lt;what we read&gt;" - so if it was submitted or withdrawn after we loaded it (say from
        /// another tab), the save fails instead of changing a locked application.
        /// </summary>
        private void GuardStatus(RentalApplication application)
        {
            db.Entry(application).Property(a => a.Status).IsModified = true;
        }

        /// <summary>
        /// Every status change goes through here. It checks the state machine and writes the history row (5.b, 5.c).
        /// A move the workflow doesn't allow throws - callers check first, so if we ever hit that it's a bug, not
        /// something the user did.
        /// </summary>
        private static void ChangeStatus(RentalApplication application, ApplicationStatus to, CurrentUser user,
            ReviewOutcome? outcome = null, string? comment = null)
        {
            var from = (ApplicationStatus)application.Status;
            if (!ApplicationWorkflow.CanTransition(from, to))
            {
                throw new InvalidOperationException($"An application can't move from {from} to {to}.");
            }
            application.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                PreviousStatus = application.Status,
                NewStatus = (long)to,
                Outcome = outcome,
                Comment = comment,
                ChangedByUser = user.Id,
                ChangedDate = DateTime.Now
            });
            application.Status = (long)to;
        }
    }
}
