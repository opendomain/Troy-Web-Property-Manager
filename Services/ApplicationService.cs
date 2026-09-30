using System.Data;
using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Data;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;
using Troy_Web_Property_Manager.ViewModels;

namespace Troy_Web_Property_Manager.Services
{
    public class ApplicationService(ApplicationDbContext db)
    {
        /// <summary>Row-level security in one place: managers see all applications, applicants only their own.</summary>
        private IQueryable<RentalApplication> Visible(CurrentUser user) =>
            user.IsManager ? db.RentalApplications : db.RentalApplications.Where(a => a.Applicant.UserId == user.Id);

        private Task<bool> UnitHasActiveLeaseAsync(int unitId) =>
            db.Leases.Where(l => l.UnitId == unitId).AnyAsync(LeaseRules.ActiveOn(DateTime.Today));

        // ---------------- Applicant ----------------
        public async Task<ServiceResult> StartAsync(int unitId, CurrentUser user)
        {
            if (user.IsManager) return ServiceResult.Error("Only applicants can apply for a unit.");
            if (!await db.Units.AnyAsync(u => u.Id == unitId)) return ServiceResult.Missing();

            // Applying again for the same unit reopens the user's open application instead of starting a duplicate.
            var openId = await db.RentalApplications
                .Where(a => a.UnitId == unitId && a.Applicant.UserId == user.Id
                    && (a.Status == (long)ApplicationStatus.Draft || a.Status == (long)ApplicationStatus.Submitted || a.Status == (long)ApplicationStatus.Returned))
                .Select(a => (int?)a.Id)
                .FirstOrDefaultAsync();
            if (openId is int existing) return ServiceResult.Ok(existing);

            if (await UnitHasActiveLeaseAsync(unitId)) return ServiceResult.Error("This unit is not available.");

            // One applicant profile per user (unique index on UserId); create it on the first application.
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

        public async Task<ApplicationEditorViewModel?> GetEditorAsync(int id, ApplicationSection? section, CurrentUser user)
        {
            var application = await Visible(user).AsNoTracking()
                .Include(a => a.Unit).ThenInclude(u => u.Property)
                .Include(a => a.Applicant)
                .Include(a => a.ApplicantInformation)
                .Include(a => a.Residences)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (application is null) return null;

            // Server-side decision: only the applicant, and only while Draft or Returned, may edit.
            var canEdit = !user.IsManager && ApplicationWorkflow.IsEditable((ApplicationStatus)application.Status);
            var current = section ?? (canEdit ? ApplicationSection.ApplicantInformation : ApplicationSection.Summary);

            // The application's own copy once the section is saved; until then, the applicant's defaults pre-fill it.
            var info = application.ApplicantInformation is { } saved
                ? (saved.Name, saved.Phone, saved.Email, saved.CurrentAddress)
                : (application.Applicant.Name, application.Applicant.Phone, application.Applicant.Email, application.Applicant.CurrentAddress);

            // A returned or denied applicant needs to know why; the full history stays manager-only.
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
                CanEdit = canEdit,
                IsReadOnly = !canEdit || current == ApplicationSection.Summary,
                IsManager = user.IsManager,
                ReviewComment = string.IsNullOrWhiteSpace(reviewComment) ? null : reviewComment
            };
        }

        public async Task<ServiceResult> SaveApplicantInformationAsync(int id, ApplicantInformationViewModel model, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;

            // Callers pass a model that passed validation, so the required fields are present.
            // Saved on this application only, so other applications (e.g. already submitted ones) are unaffected.
            var info = application!.ApplicantInformation ??= new ApplicantInformation();
            info.Name = model.Name!.Trim();
            info.Phone = model.Phone!.Trim();
            info.Email = model.Email!.Trim();
            info.CurrentAddress = model.CurrentAddress!.Trim();
            application.ApplicantInformationSaved = true;

            // Remember the latest details as defaults for the applicant's next application.
            var defaults = application.Applicant;
            (defaults.Name, defaults.Phone, defaults.Email, defaults.CurrentAddress) = (info.Name, info.Phone, info.Email, info.CurrentAddress);
            await db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        /// <summary>Continue on Residence History: residences are stored by the modal; this validates the list and marks the section saved.</summary>
        public async Task<ServiceResult> SaveResidenceHistoryAsync(int id, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            if (application!.Residences.Count == 0) return ServiceResult.Error("Add at least one prior residence.");
            application.ResidenceHistorySaved = true;
            await db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public Task<ResidenceViewModel?> GetResidenceAsync(int id, int residenceId, CurrentUser user) =>
            Visible(user).Where(a => a.Id == id).SelectMany(a => a.Residences).Where(r => r.Id == residenceId)
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

        public async Task<ServiceResult> SaveResidenceAsync(int id, ResidenceViewModel model, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
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
            await db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteResidenceAsync(int id, int residenceId, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            var residence = application!.Residences.FirstOrDefault(r => r.Id == residenceId);
            if (residence is null) return ServiceResult.Missing();
            application.Residences.Remove(residence);
            db.Residences.Remove(residence);
            // With no residences left the section is no longer complete, so Submit waits until it's saved again.
            if (application.Residences.Count == 0) application.ResidenceHistorySaved = false;
            await db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> SubmitAsync(int id, CurrentUser user)
        {
            var (application, error) = await LoadEditableAsync(id, user);
            if (error is not null) return error;
            if (!application!.ApplicantInformationSaved || !application.ResidenceHistorySaved || application.Residences.Count == 0)
            {
                return ServiceResult.Error("Save both sections before submitting.");
            }
            // Requirement: reject submit while the unit has an active lease. Other open applications are left as they are.
            if (await UnitHasActiveLeaseAsync(application.UnitId))
            {
                return ServiceResult.Error("This unit has an active lease and is no longer available.");
            }
            ChangeStatus(application, ApplicationStatus.Submitted, user);
            application.Submitted = DateTime.Now;
            await db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> WithdrawAsync(int id, CurrentUser user)
        {
            var application = await Visible(user).FirstOrDefaultAsync(a => a.Id == id);
            if (application is null) return ServiceResult.Missing();
            if (user.IsManager || !ApplicationWorkflow.CanTransition((ApplicationStatus)application.Status, ApplicationStatus.Withdrawn))
            {
                return ServiceResult.Error("This application can't be withdrawn.");
            }
            ChangeStatus(application, ApplicationStatus.Withdrawn, user);
            await db.SaveChangesAsync();
            return ServiceResult.Ok();
        }

        // ---------------- Property manager ----------------
        /// <summary>True when the application exists and is waiting for review. Reads only the status.</summary>
        public async Task<bool> CanReviewAsync(int id, CurrentUser user)
        {
            if (!user.IsManager) return false;
            var status = await db.RentalApplications.Where(a => a.Id == id).Select(a => (long?)a.Status).FirstOrDefaultAsync();
            return status is long s && ApplicationWorkflow.CanReview((ApplicationStatus)s);
        }

        public async Task<ServiceResult> ReviewAsync(int id, ReviewViewModel model, CurrentUser user)
        {
            if (!user.IsManager) return ServiceResult.Error("Only property managers can review applications.");

            try
            {
                return await ReviewInTransactionAsync(id, model, user);
            }
            catch (Exception ex) when (IsDeadlock(ex))
            {
                // Another review touching the same unit or application won the race; this one was rolled back.
                db.ChangeTracker.Clear();
                return ServiceResult.Error("Another review of this unit was saved at the same time. Reload the page and try again.");
            }
        }

        /// <summary>SQL Server error 1205: chosen as the deadlock victim. EF may wrap it in a DbUpdateException.</summary>
        private static bool IsDeadlock(Exception ex)
        {
            for (Exception? e = ex; e is not null; e = e.InnerException)
            {
                if (e is Microsoft.Data.SqlClient.SqlException { Number: 1205 }) return true;
            }
            return false;
        }

        private async Task<ServiceResult> ReviewInTransactionAsync(int id, ReviewViewModel model, CurrentUser user)
        {
            // Serializable: two approvals for the same unit can't both pass the active-lease check.
            // If they race, SQL Server rolls one back (a deadlock), which ReviewAsync reports to the user.
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

            var application = await db.RentalApplications.FirstOrDefaultAsync(a => a.Id == id);
            if (application is null) return ServiceResult.Missing();
            if (!ApplicationWorkflow.CanReview((ApplicationStatus)application.Status))
            {
                return ServiceResult.Error("Only submitted applications can be reviewed.");
            }
            var outcome = model.Outcome!.Value;
            if (ApplicationWorkflow.RequiresComment(outcome) && string.IsNullOrWhiteSpace(model.Comment))
            {
                return ServiceResult.Error("A comment is required to return or deny an application.", nameof(model.Comment));
            }
            if (outcome == ReviewOutcome.Approve)
            {
                // Checked again at approval: this prevents a second lease on the unit.
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
            ChangeStatus(application, ApplicationWorkflow.StatusFor(outcome), user, outcome, model.Comment?.Trim());
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return ServiceResult.Ok();
        }

        /// <summary>Status changes and review outcomes (who, when, comment). Shown to property managers only.</summary>
        public async Task<List<HistoryItemViewModel>> GetHistoryAsync(int id, CurrentUser user)
        {
            if (!user.IsManager) return [];

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
        /// <summary>Filters are composed on the IQueryable, so they run in SQL, not in memory.</summary>
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
                // The email given on this application, or the applicant's default before the section is saved.
                Applicant = a.ApplicantInformation != null ? a.ApplicantInformation.Email : a.Applicant.Email,
                Status = (ApplicationStatus)a.Status,
                SubmittedAt = a.Submitted
            }).ToListAsync();
        }

        // ---------------- Helpers ----------------
        /// <summary>Loads an application for an applicant write; rejects it unless it is theirs and still Draft or Returned.</summary>
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

        /// <summary>Every status change goes through here: it checks the state machine and records history.</summary>
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
