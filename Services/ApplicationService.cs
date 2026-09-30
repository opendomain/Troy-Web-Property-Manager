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
    /// withdraw, claim and release it from the review queue, review, see the history, and list them.
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
    /// <para>Sections save even when they break their rules (the one exception is text too long for its column).
    /// <see cref="CheckSections"/> runs each section's rules against what's saved and turns the result into field
    /// errors for the page and the one list of submit blockers - the editor and <see cref="SubmitAsync"/> both use it,
    /// so what the Summary says and what Submit enforces can't drift apart.</para>
    /// <para>An application can have several applicants, and any of them can edit it - so two of them can be saving at
    /// once. Each section has its own version; <see cref="SaveSectionsAsync"/> swaps it for a new one only if it still
    /// matches the version the page was loaded with, in the same transaction as the save. Saves to different sections
    /// never touch each other's version, so they don't interfere; a second save to the same section finds the version
    /// changed and is rejected as stale.</para>
    /// </remarks>
    public class ApplicationService(ApplicationDbContext db)
    {
        /// <summary>Section 1's field keys start with this, matching the input names on the application page.</summary>
        public const string ApplicantInformationPrefix = nameof(ApplicationEditorViewModel.ApplicantInformation) + ".";

        /// <summary>Key for Residence History's section-level error (no residences).</summary>
        public const string ResidencesKey = nameof(ApplicationEditorViewModel.Residences);

        /// <summary>What a stale section save says: someone else (another applicant on it) saved it first.</summary>
        public const string SectionChangedMessage =
            "Someone else saved this section after you opened it. Reload the page to see their changes, then try again.";

        /// <summary>What a stale Submit says: a section changed after the Summary was loaded.</summary>
        public const string ChangedBeforeSubmitMessage =
            "This application was changed by someone else after you opened the Summary. Reload the page and check it before submitting.";

        /// <summary>Statuses that count as an open application (the ones the unique index covers).</summary>
        private static readonly long[] OpenStatuses =
        [
            (long)ApplicationStatus.Draft, (long)ApplicationStatus.Submitted,
            (long)ApplicationStatus.Returned, (long)ApplicationStatus.UnderReview
        ];

        /// <summary>
        /// Who can see what, in one spot: managers see every application that has been submitted at least once,
        /// applicants only the ones they're on - whether they started it or were added to it. A draft that was never
        /// submitted is still private to its applicants. Every query that touches an application starts here so no
        /// method can forget the ownership check. It returns an <see cref="IQueryable{T}"/>, so the filter ends up in
        /// the SQL WHERE clause.
        /// </summary>
        private IQueryable<RentalApplication> Visible(CurrentUser user)
        {
            return user.IsManager
                ? db.RentalApplications.Where(a => a.Submitted != null)
                : db.RentalApplications.Where(a => a.ApplicationApplicants.Any(m => m.Applicant.UserId == user.Id));
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

            // Already on an open application for this unit (one they started, or one they were added to)? Reopen
            // that one instead of making a duplicate.
            var openId = await db.RentalApplications
                .Where(a => a.UnitId == unitId && OpenStatuses.Contains(a.Status)
                    && a.ApplicationApplicants.Any(m => m.Applicant.UserId == user.Id))
                .Select(a => (int?)a.Id)
                .FirstOrDefaultAsync();
            if (openId is int existing) return ServiceResult.Ok(existing);

            if (await UnitHasActiveLeaseAsync(unitId)) return ServiceResult.Error("This unit is not available.");

            var applicant = await GetOrCreateProfileAsync(user.Id);
            var application = new RentalApplication
            {
                UnitId = unitId,
                Applicant = applicant,
                Status = (long)ApplicationStatus.Draft,
                Created = DateTime.Now,
                ApplicantInformationVersion = Guid.NewGuid(),
                ResidenceHistoryVersion = Guid.NewGuid()
            };
            // The starter is on it like anyone added later, so the ownership check only has one thing to look at.
            application.ApplicationApplicants.Add(new ApplicationApplicant
            {
                Applicant = applicant,
                Added = application.Created,
                AddedByUser = user.Id
            });

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
        /// The user's applicant profile, created (blank, with their login email) if they don't have one yet - on their
        /// first application, or the first time someone adds them to one. One per user (unique index on UserId). It
        /// holds their latest details, which we only use to pre-fill section 1 on applications they start.
        /// </summary>
        private async Task<Applicant> GetOrCreateProfileAsync(string userId)
        {
            var applicant = await db.Applicants.FirstOrDefaultAsync(a => a.UserId == userId);
            if (applicant is not null) return applicant;

            var email = await db.Users.Where(u => u.Id == userId).Select(u => u.Email).FirstOrDefaultAsync();
            applicant = new Applicant
            {
                UserId = userId,
                Name = "",
                Phone = "",
                Email = email is { Length: <= 50 } ? email : "",
                CurrentAddress = ""
            };
            db.Applicants.Add(applicant);
            return applicant;
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
            // Field errors and what's stopping Submit, so the page and the Summary can show them. Only for the
            // applicant while they can still fix things (and only then is the lease query worth it).
            var checks = canEdit ? CheckSections(application, await UnitHasActiveLeaseAsync(application.UnitId)) : null;
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

            // Everyone on it, starter first. Left join to AspNetUsers for their login email; if the login is gone, fall
            // back to the profile's email.
            var members = await (
                from m in db.ApplicationApplicants.AsNoTracking()
                where m.RentalApplicationId == id
                join u in db.Users on m.Applicant.UserId equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby m.Added, m.ApplicantId
                select new { m.ApplicantId, m.Applicant.UserId, Email = u != null ? u.Email : m.Applicant.Email })
                .ToListAsync();
            var applicants = members
                .Select(m => new ApplicationApplicantViewModel
                {
                    ApplicantId = m.ApplicantId,
                    Email = string.IsNullOrEmpty(m.Email) ? "(no email)" : m.Email,
                    IsStarter = m.ApplicantId == application.ApplicantId,
                    IsYou = m.UserId == user.Id
                })
                .OrderByDescending(m => m.IsStarter)
                .ToList();

            // Who has it claimed, for managers only. Applicants just see "Under Review", not who's reviewing it.
            string? reviewer = null;
            if (user.IsManager && application.ReviewerUser is not null)
            {
                reviewer = await db.Users.Where(u => u.Id == application.ReviewerUser).Select(u => u.Email).FirstOrDefaultAsync()
                    ?? "(deleted user)";
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
                Residences = checks?.Residences ?? OrderedResidences(application).Select(r => ToViewModel(r, withErrors: false)).ToList(),
                ApplicantInformationSaved = application.ApplicantInformationSaved,
                ResidenceHistorySaved = application.ResidenceHistorySaved,
                ApplicantInformationVersion = application.ApplicantInformationVersion,
                ResidenceHistoryVersion = application.ResidenceHistoryVersion,
                Applicants = applicants,
                SubmitBlockers = checks?.Blockers ?? [],
                ApplicantInformationErrors = checks?.ApplicantInformation ?? [],
                ResidenceHistoryErrors = checks?.ResidenceHistory ?? [],
                CanEdit = canEdit,
                // The Summary is always read-only (4.a.iii). The other sections are read-only if you can't edit.
                IsReadOnly = !canEdit || current == ApplicationSection.Summary,
                IsManager = user.IsManager,
                ReviewComment = string.IsNullOrWhiteSpace(reviewComment) ? null : reviewComment,
                Reviewer = reviewer,
                ReviewClaimed = user.IsManager ? application.ReviewClaimed : null,
                ClaimedByMe = user.IsManager && application.ReviewerUser == user.Id
            };
        }

        /// <summary>
        /// Continue on section 1 (4.b.i): saves the Applicant Information and marks the section saved - even if it
        /// breaks the section's rules. The errors come back in <see cref="ServiceResult.Unresolved"/> and block Submit
        /// until they're fixed. Only text too long for its column stops the save. We re-check ownership and status
        /// here, so a post against an already-submitted application gets turned away (4.d).
        /// </summary>
        /// <param name="version">The section's version when the page was loaded. If another applicant saved the section
        /// since, nothing is saved and the result is stale.</param>
        public async Task<ServiceResult> SaveApplicantInformationAsync(int id, ApplicantInformationViewModel model, Guid version, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            if (TooLongToSave(model) is { } tooLong) return tooLong;

            // This only saves to this application - other ones (like already submitted ones) aren't touched.
            var info = application!.ApplicantInformation ??= new ApplicantInformation();
            info.Name = Clean(model.Name);
            info.Phone = Clean(model.Phone);
            info.Email = Clean(model.Email);
            info.CurrentAddress = Clean(model.CurrentAddress);
            application.ApplicantInformationSaved = true;

            // Check what was actually saved (trimmed), not what was posted.
            var remaining = SectionValidator.Validate(ToViewModel(info));
            if (remaining.Count == 0 && application.Applicant.UserId == user.Id)
            {
                // Remember these details to pre-fill the starter's next application - but only once they're valid, so
                // a half-finished section doesn't get copied into the next one. Only when the starter saves it: the
                // section pre-fills from the starter's profile, and another applicant on it shouldn't rewrite that.
                var defaults = application.Applicant;
                (defaults.Name, defaults.Phone, defaults.Email, defaults.CurrentAddress) = (info.Name, info.Phone, info.Email, info.CurrentAddress);
            }
            // If section 1 was already saved, the application row itself doesn't change, so force the status check.
            GuardStatus(application);
            var (saved, _) = await SaveSectionsAsync(id, SectionChangedMessage, (ApplicationSection.ApplicantInformation, version));
            return saved.Succeeded ? ServiceResult.Saved(remaining) : saved;
        }

        /// <summary>
        /// Continue on Residence History (4.b.i). The modal already saved each residence (4.c), so this marks the
        /// section saved - even with no residences, or residences that still have errors. Those come back in
        /// <see cref="ServiceResult.Unresolved"/> and block Submit until they're fixed.
        /// </summary>
        /// <param name="version">The section's version when the page was loaded (see <see cref="SaveApplicantInformationAsync"/>).</param>
        public async Task<ServiceResult> SaveResidenceHistoryAsync(int id, Guid version, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            application!.ResidenceHistorySaved = true;
            GuardStatus(application);
            var (saved, _) = await SaveSectionsAsync(id, SectionChangedMessage, (ApplicationSection.ResidenceHistory, version));
            if (!saved.Succeeded) return saved;

            var checks = CheckSections(application, unitHasActiveLease: false);
            return ServiceResult.Saved([.. checks.ResidenceHistory, .. checks.Residences.SelectMany(r => r.Errors)]);
        }

        /// <summary>
        /// Gets one residence for the edit modal. We go through <see cref="Visible"/> and then to that application's
        /// residences, so a residence id from someone else's application just comes back empty.
        /// </summary>
        /// <remarks>It comes with its rule errors, so the modal can show what still needs fixing, and the section's
        /// current version, so saving it is rejected if another applicant saves the section while it's open.</remarks>
        public async Task<ResidenceViewModel?> GetResidenceAsync(int id, int residenceId, CurrentUser user)
        {
            var found = await Visible(user).AsNoTracking().Where(a => a.Id == id)
                .Select(a => new { a.ResidenceHistoryVersion, Residence = a.Residences.FirstOrDefault(r => r.Id == residenceId) })
                .FirstOrDefaultAsync();
            if (found?.Residence is null) return null;
            var model = ToViewModel(found.Residence, withErrors: true);
            model.SectionVersion = found.ResidenceHistoryVersion;
            return model;
        }

        /// <summary>
        /// Adds or edits a residence from the modal (4.c). Only while the application can still be edited. It saves
        /// even if it breaks the residence rules - the result carries the new residence's id and whatever errors are
        /// left, so the modal can stay open on the saved residence and show them. Adding or editing a residence is a
        /// save to Residence History, so it's checked against <see cref="ResidenceViewModel.SectionVersion"/>, and the
        /// result carries the section's new version for the modal to use next.
        /// </summary>
        public async Task<ServiceResult> SaveResidenceAsync(int id, ResidenceViewModel model, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            if (TooLongToSave(model) is { } tooLong) return tooLong;
            // Look the residence up in this application's residences only, not by id across the whole table,
            // so a faked ResidenceId can't reach into another application.
            var residence = model.ResidenceId is null
                ? new Residence()
                : application!.Residences.FirstOrDefault(r => r.Id == model.ResidenceId);
            if (residence is null) return ServiceResult.Missing();
            residence.Address = Clean(model.Address);
            residence.LandlordName = Clean(model.LandlordName);
            residence.LandlordPhone = Clean(model.LandlordPhone);
            residence.MoveInDate = model.MoveInDate;
            residence.MoveOutDate = model.MoveOutDate;
            if (model.ResidenceId is null) application!.Residences.Add(residence);
            GuardStatus(application!);
            var (saved, next) = await SaveSectionsAsync(id, SectionChangedMessage, (ApplicationSection.ResidenceHistory, model.SectionVersion));
            return saved.Succeeded ? ServiceResult.Saved(SectionValidator.Validate(ToViewModel(residence)), residence.Id, next) : saved;
        }

        /// <summary>
        /// Removes a residence from the modal (4.c). Only while the application can still be edited. It's a save to
        /// Residence History, so it's rejected as stale if another applicant saved the section since
        /// <paramref name="version"/> was loaded.
        /// </summary>
        public async Task<ServiceResult> DeleteResidenceAsync(int id, int residenceId, Guid version, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            var residence = application!.Residences.FirstOrDefault(r => r.Id == residenceId);
            if (residence is null) return ServiceResult.Missing();
            application.Residences.Remove(residence);
            db.Residences.Remove(residence);
            // If that was the last one, the section stays saved but now has the "add at least one" error, which blocks
            // Submit (see CheckSections).
            GuardStatus(application);
            var (saved, _) = await SaveSectionsAsync(id, SectionChangedMessage, (ApplicationSection.ResidenceHistory, version));
            return saved;
        }

        /// <summary>
        /// Submit from the Summary (Challenge a; 4.b.ii). Also handles resubmitting a Returned application once it's
        /// been fixed - Returned is editable, and Returned → Submitted is allowed.
        /// </summary>
        /// <remarks>
        /// The Summary posts both sections' versions. If another applicant saved either section after the Summary was
        /// loaded, it isn't submitted - they'd be submitting changes they haven't seen.
        /// </remarks>
        public async Task<ServiceResult> SubmitAsync(int id, Guid applicantInformationVersion, Guid residenceHistoryVersion, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            // 4.b.ii / 4.e: both sections saved with no errors left, and the unit not leased. The Summary shows the
            // same list and the page disables the button, but this is the check that actually matters. We leave any
            // other open applications for the unit alone.
            var blockers = CheckSections(application!, await UnitHasActiveLeaseAsync(application!.UnitId)).Blockers;
            if (blockers.Count > 0)
            {
                return ServiceResult.Error(string.Join(" ", blockers));
            }
            ChangeStatus(application, ApplicationStatus.Submitted, user);
            application.Submitted = DateTime.Now;
            var (saved, _) = await SaveSectionsAsync(id, ChangedBeforeSubmitMessage,
                (ApplicationSection.ApplicantInformation, applicantInformationVersion),
                (ApplicationSection.ResidenceHistory, residenceHistoryVersion));
            return saved;
        }

        // ---------------- Applicants on an application ----------------

        /// <summary>
        /// Adds another applicant to the application by their login email. Any applicant already on it can do this,
        /// while it can still be edited. The new applicant can then view and edit it like everyone else.
        /// </summary>
        /// <remarks>
        /// It has to be an existing account in the Applicant role (managers can't be added). An applicant can only be on
        /// one open application per unit - the same rule Start follows - so someone who already has an open
        /// application for this unit can't be added to this one too.
        /// </remarks>
        public async Task<ServiceResult> AddApplicantAsync(int id, AddApplicantViewModel model, CurrentUser user)
        {
            const string field = nameof(AddApplicantViewModel.Email);
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;

            // Identity stores emails upper-cased in NormalizedEmail, so this matches however it was typed.
            var normalized = (model.Email ?? "").Trim().ToUpperInvariant();
            var userId = await (
                from u in db.Users
                where u.NormalizedEmail == normalized
                    && db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == AppRoles.Applicant))
                select u.Id).FirstOrDefaultAsync();
            if (userId is null) return ServiceResult.Error("There's no applicant account with that email.", field);

            if (await db.ApplicationApplicants.AnyAsync(m => m.RentalApplicationId == id && m.Applicant.UserId == userId))
            {
                return ServiceResult.Error("They're already on this application.", field);
            }
            var hasOtherOpen = await db.RentalApplications.AnyAsync(a => a.Id != id && a.UnitId == application!.UnitId
                && OpenStatuses.Contains(a.Status) && a.ApplicationApplicants.Any(m => m.Applicant.UserId == userId));
            if (hasOtherOpen) return ServiceResult.Error("They already have an open application for this unit.", field);

            application!.ApplicationApplicants.Add(new ApplicationApplicant
            {
                Applicant = await GetOrCreateProfileAsync(userId),
                Added = DateTime.Now,
                AddedByUser = user.Id
            });
            // The application row doesn't change otherwise, so force the status check: no adding to one that was just
            // submitted.
            GuardStatus(application);
            try
            {
                return await SaveApplicationAsync();
            }
            catch (DbUpdateException ex) when (SqlErrors.IsUniqueViolation(ex))
            {
                // Someone added them (or created their profile) at the same moment.
                db.ChangeTracker.Clear();
                return ServiceResult.Stale();
            }
        }

        /// <summary>
        /// Takes an applicant off the application - another applicant, or yourself (leaving it). Any applicant on it
        /// can do this while it can still be edited. The applicant who started it can't be removed. Once removed, they
        /// get a 404 on it like on anyone else's application.
        /// </summary>
        public async Task<ServiceResult> RemoveApplicantAsync(int id, int applicantId, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            if (applicantId == application!.ApplicantId)
            {
                return ServiceResult.Error("The applicant who started this application can't be removed.");
            }
            var membership = await db.ApplicationApplicants
                .FirstOrDefaultAsync(m => m.RentalApplicationId == id && m.ApplicantId == applicantId);
            if (membership is null) return ServiceResult.Missing();

            db.ApplicationApplicants.Remove(membership);
            GuardStatus(application);
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
        /// <summary>True if this manager has the application claimed (Under Review), so it's theirs to review.</summary>
        public async Task<bool> CanReviewAsync(int id, CurrentUser user)
        {
            if (!user.IsManager) return false;
            var claim = await db.RentalApplications.Where(a => a.Id == id)
                .Select(a => new { a.Status, a.ReviewerUser }).FirstOrDefaultAsync();
            return claim is not null && ApplicationWorkflow.CanReview((ApplicationStatus)claim.Status) && claim.ReviewerUser == user.Id;
        }

        /// <summary>True if the application is Under Review, so a manager can release it back to the queue.</summary>
        public async Task<bool> CanReleaseAsync(int id, CurrentUser user)
        {
            if (!user.IsManager) return false;
            var status = await Visible(user).Where(a => a.Id == id).Select(a => (long?)a.Status).FirstOrDefaultAsync();
            return status is long s && ApplicationWorkflow.CanRelease((ApplicationStatus)s);
        }

        /// <summary>
        /// The review queue: Submitted applications waiting for someone, oldest first, plus the ones already Under
        /// Review, split into this manager's claims and everyone else's. Managers only.
        /// </summary>
        public async Task<ReviewQueueViewModel> GetQueueAsync(CurrentUser user)
        {
            if (!user.IsManager) return new ReviewQueueViewModel();

            // One query for both statuses, with a left join for the reviewer's email (same as the history panel).
            var rows = await (
                from a in Visible(user).AsNoTracking()
                where a.Status == (long)ApplicationStatus.Submitted || a.Status == (long)ApplicationStatus.UnderReview
                join u in db.Users on a.ReviewerUser equals u.Id into users
                from u in users.DefaultIfEmpty()
                orderby a.Submitted, a.Id
                select new
                {
                    a.Status,
                    a.ReviewerUser,
                    Item = new ReviewQueueItemViewModel
                    {
                        Id = a.Id,
                        PropertyName = a.Unit.Property.Name,
                        UnitNumber = a.Unit.UnitNumber,
                        Applicant = a.ApplicantInformation != null ? a.ApplicantInformation.Email : a.Applicant.Email,
                        SubmittedAt = a.Submitted,
                        Reviewer = a.ReviewerUser == null ? null : u != null ? u.Email : "(deleted user)",
                        ClaimedAt = a.ReviewClaimed
                    }
                }).ToListAsync();

            return new ReviewQueueViewModel
            {
                Mine = rows.Where(r => r.ReviewerUser == user.Id).Select(r => r.Item).ToList(),
                Waiting = rows.Where(r => r.Status == (long)ApplicationStatus.Submitted).Select(r => r.Item).ToList(),
                ClaimedByOthers = rows.Where(r => r.Status == (long)ApplicationStatus.UnderReview && r.ReviewerUser != user.Id)
                    .Select(r => r.Item).ToList()
            };
        }

        /// <summary>
        /// Takes a Submitted application out of the review queue for this manager (Under Review), so nobody else
        /// reviews it at the same time. If two managers claim at once, the status concurrency token lets only the
        /// first one through; the second gets a "changed by someone else" result.
        /// </summary>
        public async Task<ServiceResult> ClaimAsync(int id, CurrentUser user)
        {
            if (!user.IsManager) return ServiceResult.Forbid("Only property managers can claim applications.");
            var application = await Visible(user).FirstOrDefaultAsync(a => a.Id == id);
            if (application is null) return ServiceResult.Missing();
            if (!ApplicationWorkflow.CanClaim((ApplicationStatus)application.Status))
            {
                return ServiceResult.Error(application.Status == (long)ApplicationStatus.UnderReview
                    ? "This application has already been claimed."
                    : "Only submitted applications can be claimed.");
            }
            ChangeStatus(application, ApplicationStatus.UnderReview, user);
            return await SaveApplicationAsync();
        }

        /// <summary>
        /// Puts an Under Review application back in the queue (Submitted) without a decision. Any manager can do this,
        /// not just the one who claimed it, so a claim never gets stuck if that manager is away. The history row
        /// shows who released it.
        /// </summary>
        public async Task<ServiceResult> ReleaseAsync(int id, CurrentUser user)
        {
            if (!user.IsManager) return ServiceResult.Forbid("Only property managers can release applications.");
            var application = await Visible(user).FirstOrDefaultAsync(a => a.Id == id);
            if (application is null) return ServiceResult.Missing();
            if (!ApplicationWorkflow.CanRelease((ApplicationStatus)application.Status))
            {
                return ServiceResult.Error("Only applications under review can be released.");
            }
            ChangeStatus(application, ApplicationStatus.Submitted, user);
            return await SaveApplicationAsync();
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
                return ServiceResult.Error("Claim this application from the review queue before reviewing it.");
            }
            // Only the manager who claimed it can finish the review.
            if (application.ReviewerUser != user.Id)
            {
                return ServiceResult.Error("This application is claimed by another property manager.");
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

        // ---------------- Manager notes ----------------

        /// <summary>
        /// Property managers' private notes on an application. Returns null for applicants, and for any application
        /// the manager can't see - the controller turns that into a 404, and the view component renders nothing.
        /// </summary>
        /// <remarks>
        /// This is the only way to read <see cref="ManagerNote"/>, and the role check is here (not just on the
        /// controller or view component), so nothing else can accidentally hand notes to an applicant.
        /// </remarks>
        public async Task<ManagerNotesViewModel?> GetManagerNotesAsync(int id, CurrentUser user)
        {
            if (!user.IsManager) return null;
            if (!await Visible(user).AnyAsync(a => a.Id == id)) return null;

            // Left join to AspNetUsers for the email of whoever saved last, same as the history panel.
            var note = await (
                from n in db.ManagerNotes.AsNoTracking()
                where n.RentalApplicationId == id
                join u in db.Users on n.UpdatedByUser equals u.Id into users
                from u in users.DefaultIfEmpty()
                select new { n.Notes, n.Version, n.UpdatedDate, UpdatedBy = u != null ? u.Email : null })
                .FirstOrDefaultAsync();

            return new ManagerNotesViewModel
            {
                ApplicationId = id,
                Notes = note?.Notes,
                Version = note?.Version,
                UpdatedAt = note?.UpdatedDate,
                UpdatedBy = note is null ? null : note.UpdatedBy ?? "(deleted user)"
            };
        }

        /// <summary>
        /// Saves the notes from the modal. Managers only, on any application they can see, whatever its status -
        /// the notes are internal, so they stay editable after a decision too.
        /// </summary>
        /// <remarks>
        /// Two managers can have the modal open at once. The form posts back the <see cref="ManagerNote.Version"/> it
        /// was loaded with, and we use that as the original value of the concurrency token, so the UPDATE only matches
        /// if nobody saved in between. If someone did, nothing is saved and the manager is told to reload.
        /// </remarks>
        public async Task<ServiceResult> SaveManagerNotesAsync(int id, ManagerNotesViewModel model, CurrentUser user)
        {
            const string changedMessage = "These notes were changed by someone else. Reload the page and try again.";

            if (!user.IsManager) return ServiceResult.Forbid("Only property managers can edit notes.");
            if (!await Visible(user).AnyAsync(a => a.Id == id)) return ServiceResult.Missing();

            var note = await db.ManagerNotes.FirstOrDefaultAsync(n => n.RentalApplicationId == id);
            if (note is null)
            {
                // The form thought there were notes, but they're gone - the row is never deleted, so this is a
                // tampered or badly out-of-date form.
                if (model.Version is not null) return ServiceResult.Stale(changedMessage);
                note = new ManagerNote { RentalApplicationId = id };
                db.ManagerNotes.Add(note);
            }
            else
            {
                // Compare against the version the form was loaded with, not the one we just read. A form opened
                // before the first note existed has no version, which never matches, so that's stale too.
                db.Entry(note).Property(n => n.Version).OriginalValue = model.Version ?? Guid.Empty;
            }

            note.Notes = model.Notes?.Trim() ?? "";
            note.UpdatedByUser = user.Id;
            note.UpdatedDate = DateTime.Now;
            note.Version = Guid.NewGuid();

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                return ServiceResult.Stale(changedMessage);
            }
            catch (DbUpdateException ex) when (SqlErrors.IsUniqueViolation(ex))
            {
                // Two managers added the first note at the same moment, and the primary key stopped the second one.
                db.ChangeTracker.Clear();
                return ServiceResult.Stale(changedMessage);
            }
            return ServiceResult.Ok();
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

        // ---------------- Section versions ----------------

        /// <summary>
        /// Saves the tracked changes for one or more sections, but only if each section's version still matches what
        /// the page was loaded with. Returns the result and the sections' new version.
        /// </summary>
        /// <remarks>
        /// <para>For each section, one <c>UPDATE ... SET Version = new WHERE id = @id AND Version = @expected</c>
        /// (a compare-and-swap). If it matches no row, someone else saved that section since this page loaded: we
        /// roll back and return <paramref name="staleMessage"/>. Otherwise the tracked changes are saved and it all
        /// commits together, so a save and its version change can't be separated.</para>
        /// <para>Each section only swaps its own version column, so a save to Applicant Information and a save to
        /// Residence History at the same time both go through (they briefly queue on the row lock, then both
        /// commit). The Status concurrency token still guards against the application being submitted, withdrawn or
        /// reviewed in the meantime.</para>
        /// </remarks>
        private async Task<(ServiceResult Result, Guid Version)> SaveSectionsAsync(int id, string staleMessage,
            params (ApplicationSection Section, Guid Expected)[] sections)
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var next = Guid.NewGuid();
            foreach (var (section, expected) in sections)
            {
                var swapped = section == ApplicationSection.ApplicantInformation
                    ? await db.RentalApplications.Where(a => a.Id == id && a.ApplicantInformationVersion == expected)
                        .ExecuteUpdateAsync(set => set.SetProperty(a => a.ApplicantInformationVersion, next))
                    : await db.RentalApplications.Where(a => a.Id == id && a.ResidenceHistoryVersion == expected)
                        .ExecuteUpdateAsync(set => set.SetProperty(a => a.ResidenceHistoryVersion, next));
                if (swapped == 0)
                {
                    // Nothing's committed yet; disposing the transaction rolls back any swap already done.
                    db.ChangeTracker.Clear();
                    return (ServiceResult.Stale(staleMessage), Guid.Empty);
                }
            }

            var saved = await SaveApplicationAsync();
            if (!saved.Succeeded) return (saved, Guid.Empty);
            await transaction.CommitAsync();
            return (saved, next);
        }

        // ---------------- Section rules ----------------

        /// <summary>Everything the section rules say about a saved application (see <see cref="CheckSections"/>).</summary>
        /// <param name="ApplicantInformation">Field errors on section 1, keyed like the page's inputs.</param>
        /// <param name="Residences">The residences, newest first, each with its own errors.</param>
        /// <param name="ResidenceHistory">Section-level errors on section 2 (no residences).</param>
        /// <param name="Blockers">The one list of what's stopping Submit.</param>
        private sealed record SectionChecks(List<FieldError> ApplicantInformation, List<ResidenceViewModel> Residences,
            List<FieldError> ResidenceHistory, List<string> Blockers);

        /// <summary>
        /// Runs each section's rules against what's saved. A section's field errors only count once it's been saved
        /// (until then its blocker is just "not saved yet"), but residences are saved one by one from the modal, so
        /// theirs always count. Needs <c>ApplicantInformation</c> and <c>Residences</c> loaded.
        /// </summary>
        private static SectionChecks CheckSections(RentalApplication application, bool unitHasActiveLease)
        {
            var info = application.ApplicantInformationSaved && application.ApplicantInformation is { } saved
                ? SectionValidator.Validate(ToViewModel(saved), ApplicantInformationPrefix)
                : [];
            var residences = OrderedResidences(application).Select(r => ToViewModel(r, withErrors: true)).ToList();
            List<FieldError> history = application.ResidenceHistorySaved && residences.Count == 0
                ? [new FieldError(ResidencesKey, SubmissionRules.NoResidences)]
                : [];
            var blockers = SubmissionRules.GetBlockers(
                application.ApplicantInformationSaved, info.Select(e => e.Message),
                application.ResidenceHistorySaved, residences.Count,
                residences.SelectMany(r => r.Errors.Select(e => $"{Describe(r)} - {e.Message}")),
                unitHasActiveLease);
            return new SectionChecks(info, residences, history, blockers);
        }

        /// <summary>How a residence is named in the blocker list.</summary>
        private static string Describe(ResidenceViewModel residence)
        {
            return string.IsNullOrWhiteSpace(residence.Address) ? "Residence with no address" : residence.Address;
        }

        private static IEnumerable<Residence> OrderedResidences(RentalApplication application)
        {
            return application.Residences.OrderByDescending(r => r.MoveInDate).ThenByDescending(r => r.Id);
        }

        private static ApplicantInformationViewModel ToViewModel(ApplicantInformation info)
        {
            return new() { Name = info.Name, Phone = info.Phone, Email = info.Email, CurrentAddress = info.CurrentAddress };
        }

        private static ResidenceViewModel ToViewModel(Residence residence, bool withErrors = false)
        {
            var model = new ResidenceViewModel
            {
                ApplicationId = residence.RentalApplicationId,
                ResidenceId = residence.Id,
                Address = residence.Address,
                LandlordName = residence.LandlordName,
                LandlordPhone = residence.LandlordPhone,
                MoveInDate = residence.MoveInDate,
                MoveOutDate = residence.MoveOutDate
            };
            if (withErrors) model.Errors = SectionValidator.Validate(model);
            return model;
        }

        /// <summary>
        /// The one rule that does stop a save: text longer than its column can't be stored. Returns those errors
        /// (and nothing is saved), or null if everything fits.
        /// </summary>
        private static ServiceResult? TooLongToSave(object model)
        {
            var tooLong = SectionValidator.Validate(model).Where(e => e.PreventsSave).ToList();
            return tooLong.Count > 0 ? ServiceResult.Invalid(tooLong) : null;
        }

        /// <summary>Trimmed, with blank stored as "" (the columns are NOT NULL).</summary>
        private static string Clean(string? value)
        {
            return value?.Trim() ?? "";
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
        /// <remarks>
        /// It also keeps the review claim in step with the status: moving to Under Review records who claimed it and
        /// when, and any move out of it (review, release, withdraw) clears both. The database's
        /// CK_RentalApplications_ReviewClaim constraint backs that up.
        /// </remarks>
        private static void ChangeStatus(RentalApplication application, ApplicationStatus to, CurrentUser user,
            ReviewOutcome? outcome = null, string? comment = null)
        {
            var from = (ApplicationStatus)application.Status;
            if (!ApplicationWorkflow.CanTransition(from, to))
            {
                throw new InvalidOperationException($"An application can't move from {from} to {to}.");
            }
            var now = DateTime.Now;
            application.ApplicationStatusHistories.Add(new ApplicationStatusHistory
            {
                PreviousStatus = application.Status,
                NewStatus = (long)to,
                Outcome = outcome,
                Comment = comment,
                ChangedByUser = user.Id,
                ChangedDate = now
            });
            application.Status = (long)to;
            var claimed = to == ApplicationStatus.UnderReview;
            application.ReviewerUser = claimed ? user.Id : null;
            application.ReviewClaimed = claimed ? now : null;
        }
    }
}
