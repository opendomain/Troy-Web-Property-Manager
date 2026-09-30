using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Troy_Web_Property_Manager.Models;
using Troy_Web_Property_Manager.Rules;

namespace Troy_Web_Property_Manager.Data
{
    /// <summary>
    /// Fills an empty database with believable demo data from Bogus: property managers, applicants, properties
    /// with units, and applications in every status along with their history, residences and leases.
    /// It plays by the same rules as the app: every history row is a legal workflow step, units never have
    /// overlapping leases, nothing gets submitted while its unit is leased, and an applicant has at most one open
    /// application per unit (whether they started it or were added to it). Some applications have a second applicant.
    /// The seed is fixed so everyone gets the same data.
    /// </summary>
    /// <remarks>
    /// <para>This covers Technical 2.b.ii (seed lookups, managers, applicants, properties, units and applications in
    /// every status, using Bogus). Lookups and roles are seeded separately in Program.cs since the app needs those
    /// everywhere. This demo data has accounts with a known password, so it only runs in Development.</para>
    /// <para>Safe to run more than once: it bails if there are any properties, reuses demo accounts that already
    /// exist, and does everything in one transaction so a failure halfway through doesn't leave a mess.</para>
    /// <para>Why not just set a status and be done? An Approved application with no history, lease or residences
    /// is something the app could never actually produce, and the history panel, summary and availability queries
    /// would all look wrong. So each application walks a real path through the state machine (checked with
    /// <see cref="ApplicationWorkflow.CanTransition"/>) and approvals get real leases. <c>DemoDataSeederTests</c>
    /// checks all of this.</para>
    /// </remarks>
    public static class DemoDataSeeder
    {
        /// <summary>Password for every demo account.</summary>
        public const string Password = "Demo#2026";

        private const int RandomSeed = 20260930;
        private const int PropertyCount = 6;
        private const int ApplicantCount = 12;
        private const int ApplicationCount = 45;

        /// <summary>Seeds the demo data unless there are already properties. Returns false if it skipped.</summary>
        /// <param name="now">"Now" in the business's time zone (BusinessClock); defaults to the server's clock.</param>
        public static async Task<bool> SeedAsync(ApplicationDbContext db, UserManager<IdentityUser> userManager, DateTime? now = null)
        {
            if (await db.Properties.AnyAsync()) return false;

            var faker = new Faker("en_US") { Random = new Randomizer(RandomSeed) };

            // UserManager saves through the same context, so creating users is part of this transaction too.
            await using var transaction = await db.Database.BeginTransactionAsync();

            var managers = await CreateUsersAsync(userManager, AppRoles.PropertyManager,
                Enumerable.Range(1, 2).Select(i => $"manager{i}@example.com"));
            var applicantUsers = await CreateUsersAsync(userManager, AppRoles.Applicant,
                Enumerable.Range(1, ApplicantCount).Select(i => $"applicant{i}@example.com"));

            var properties = CreateProperties(faker, await db.UnitTypes.ToListAsync());
            var applicants = applicantUsers.Select(user => CreateApplicant(faker, user)).ToList();
            var applications = new ApplicationGenerator(faker, managers, now ?? DateTime.Now)
                .Generate(properties.SelectMany(p => p.Units).ToList(), applicants);

            db.Properties.AddRange(properties);
            db.Applicants.AddRange(applicants);
            db.RentalApplications.AddRange(applications);
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }

        private static async Task<List<IdentityUser>> CreateUsersAsync(UserManager<IdentityUser> userManager, string role, IEnumerable<string> emails)
        {
            var users = new List<IdentityUser>();
            foreach (var email in emails)
            {
                // Account's already there? Just reuse it and don't touch the password.
                var user = await userManager.FindByEmailAsync(email);
                if (user is null)
                {
                    user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                    ThrowIfFailed(await userManager.CreateAsync(user, Password), email);
                }
                if (!await userManager.IsInRoleAsync(user, role))
                {
                    ThrowIfFailed(await userManager.AddToRoleAsync(user, role), email);
                }
                users.Add(user);
            }
            return users;
        }

        private static void ThrowIfFailed(IdentityResult result, string email)
        {
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Couldn't seed demo user {email}: {string.Join(" ", result.Errors.Select(e => e.Description))}");
            }
        }

        private static List<Property> CreateProperties(Faker f, List<UnitType> unitTypes)
        {
            var active = unitTypes.Where(t => t.IsActive).ToList();
            var inactive = unitTypes.Where(t => !t.IsActive).ToList();
            string[] suffixes = ["Apartments", "Commons", "Residences", "Terrace", "Court", "Place"];

            return Enumerable.Range(0, PropertyCount).Select(_ =>
            {
                var property = new Property
                {
                    Name = Fit($"{f.Address.StreetName()} {f.PickRandom(suffixes)}"),
                    Address = Fit($"{f.Address.StreetAddress()}, {f.Address.City()}")
                };

                var unitCount = f.Random.Int(4, 10);
                for (var i = 0; i < unitCount; i++)
                {
                    // Every so often a unit keeps a retired (inactive) type - the app allows that for existing units.
                    var type = inactive.Count > 0 && f.Random.Bool(0.1f) ? f.PickRandom(inactive) : f.PickRandom(active);
                    var bedrooms = type.Name == "Studio" ? 0 : f.Random.Int(1, 4);
                    property.Units.Add(new Unit
                    {
                        UnitNumber = $"{i / 4 + 1}{i % 4 + 1:00}", // 101, 102, 103, 104, 201, ...
                        Bedrooms = bedrooms,
                        MonthlyRent = Math.Round((900 + bedrooms * 450 + f.Random.Int(-150, 400)) / 25m) * 25,
                        UnitType = type
                    });
                }
                return property;
            }).ToList();
        }

        private static Applicant CreateApplicant(Faker f, IdentityUser user)
        {
            return new()
            {
                UserId = user.Id,
                Name = Fit(f.Name.FullName()),
                Phone = f.Phone.PhoneNumber("(###) ###-####"),
                Email = user.Email!,
                CurrentAddress = Fit($"{f.Address.StreetAddress()}, {f.Address.City()}")
            };
        }

        /// <summary>Keeps generated text within the 50-character columns.</summary>
        private static string Fit(string value)
        {
            return value.Length <= 50 ? value : value[..50].TrimEnd();
        }

        /// <summary>Builds applications whose history, sections and leases all line up.</summary>
        private sealed class ApplicationGenerator(Faker f, List<IdentityUser> managers, DateTime now)
        {
            // Every review goes through the queue: a manager claims it (Under Review) and then decides.
            private static readonly ApplicationStatus[][] ApprovedPaths =
            [
                [ApplicationStatus.Draft, ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Approved],
                [ApplicationStatus.Draft, ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Returned,
                    ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Approved]
            ];

            private static readonly ApplicationStatus[][] OtherPaths =
            [
                [ApplicationStatus.Draft],
                [ApplicationStatus.Draft, ApplicationStatus.Withdrawn],
                [ApplicationStatus.Draft, ApplicationStatus.Submitted],
                [ApplicationStatus.Draft, ApplicationStatus.Submitted, ApplicationStatus.UnderReview],
                // Claimed, then released back to the queue without a decision.
                [ApplicationStatus.Draft, ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Submitted],
                [ApplicationStatus.Draft, ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Returned, ApplicationStatus.Submitted],
                [ApplicationStatus.Draft, ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Returned],
                [ApplicationStatus.Draft, ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Denied],
                [ApplicationStatus.Draft, ApplicationStatus.Submitted, ApplicationStatus.Withdrawn],
                [ApplicationStatus.Draft, ApplicationStatus.Submitted, ApplicationStatus.UnderReview, ApplicationStatus.Withdrawn]
            ];

            private static readonly string[] ReturnComments =
            [
                "Please add your landlord's phone number for your current residence.",
                "We need at least two years of residence history.",
                "The move-in dates don't line up; please check them.",
                "Please confirm your current address."
            ];

            private static readonly string[] DenyComments =
            [
                "Income doesn't meet the requirement for this unit.",
                "We couldn't verify your rental history.",
                "Another applicant was approved for this unit.",
                "Negative landlord reference."
            ];

            private readonly Dictionary<Unit, List<(DateTime Start, DateTime End)>> _leases = [];
            private readonly Dictionary<Unit, List<DateTime>> _submits = [];
            private readonly HashSet<(Applicant, Unit)> _open = [];
            private readonly List<RentalApplication> _applications = [];
            private List<Applicant> _applicants = [];

            public List<RentalApplication> Generate(List<Unit> units, List<Applicant> applicants)
            {
                _applicants = applicants;
                // Approvals first, so every later submit can be checked against the leases they create.
                // About a third of the units end up with a lease, some already expired.
                foreach (var unit in f.PickRandom(units, units.Count / 3))
                {
                    TryAdd(f.PickRandom(ApprovedPaths), unit, f.PickRandom(applicants), maxDaysAgo: 700);
                }

                for (var attempts = 0; _applications.Count < ApplicationCount && attempts < ApplicationCount * 20; attempts++)
                {
                    TryAdd(f.PickRandom(OtherPaths), f.PickRandom(units), f.PickRandom(applicants), maxDaysAgo: 240);
                }
                return _applications;
            }

            /// <summary>Adds the application unless its timeline would break a rule. Returns whether it got added.</summary>
            private bool TryAdd(ApplicationStatus[] path, Unit unit, Applicant applicant, int maxDaysAgo)
            {
                // Each step lands somewhere between a few hours and two weeks after the last, all in the past.
                var times = new List<DateTime> { now.AddDays(-f.Random.Double(3, maxDaysAgo)) };
                for (var i = 1; i < path.Length; i++)
                {
                    times.Add(times[^1].AddHours(f.Random.Int(4, 24 * 14)));
                }
                if (times[^1] > now) return false;

                var final = path[^1];
                var isOpen = !ApplicationWorkflow.IsTerminal(final);
                if (isOpen && _open.Contains((applicant, unit))) return false;

                var leases = _leases.GetValueOrDefault(unit) ?? [];
                var submits = SubmitTimes(path, times);
                if (submits.Any(t => leases.Any(l => LeasedOn(l, t)))) return false; // the app rejects submitting a leased unit

                (DateTime Start, DateTime End)? lease = null;
                if (final == ApplicationStatus.Approved)
                {
                    var start = times[^1].Date;
                    lease = (start, LeaseRules.EndDateFor(start));
                    var otherSubmits = _submits.GetValueOrDefault(unit) ?? [];
                    if (leases.Any(l => l.Start < lease.Value.End && lease.Value.Start < l.End)) return false; // one lease at a time
                    if (otherSubmits.Any(t => LeasedOn(lease.Value, t))) return false;
                }

                var application = Build(path, times, unit, applicant);
                if (lease is { } l)
                {
                    application.Leases.Add(new Lease { Unit = unit, StartDate = l.Start, EndDate = l.End });
                    Remember(_leases, unit, l);
                }
                foreach (var submit in submits) Remember(_submits, unit, submit);
                if (isOpen) _open.Add((applicant, unit));
                MaybeAddSecondApplicant(application, applicant, unit, isOpen, times[0]);
                _applications.Add(application);
                return true;
            }

            /// <summary>
            /// About one application in five gets a second applicant, added by the starter a little after they started
            /// it. On an open application they're skipped if they already have an open one for the unit - the app
            /// refuses that too.
            /// </summary>
            private void MaybeAddSecondApplicant(RentalApplication application, Applicant starter, Unit unit, bool isOpen, DateTime started)
            {
                if (!f.Random.Bool(0.2f)) return;
                var other = f.PickRandom(_applicants);
                if (other == starter || (isOpen && _open.Contains((other, unit)))) return;

                application.ApplicationApplicants.Add(new ApplicationApplicant
                {
                    Applicant = other,
                    Added = started.AddHours(1),
                    AddedByUser = starter.UserId!
                });
                if (isOpen) _open.Add((other, unit));
            }

            /// <summary>
            /// When the applicant submitted. A release (Under Review → Submitted) also lands on Submitted, but it's a
            /// manager putting it back in the queue, not a new submission, so it doesn't count.
            /// </summary>
            private static List<DateTime> SubmitTimes(ApplicationStatus[] path, List<DateTime> times)
            {
                return Enumerable.Range(0, path.Length)
                    .Where(i => path[i] == ApplicationStatus.Submitted && path[i - 1] != ApplicationStatus.UnderReview)
                    .Select(i => times[i])
                    .ToList();
            }

            private static bool LeasedOn((DateTime Start, DateTime End) lease, DateTime time)
            {
                return lease.Start <= time.Date && time.Date < lease.End;
            }

            private static void Remember<T>(Dictionary<Unit, List<T>> byUnit, Unit unit, T value)
            {
                if (!byUnit.TryGetValue(unit, out var list)) byUnit[unit] = list = [];
                list.Add(value);
            }

            private RentalApplication Build(ApplicationStatus[] path, List<DateTime> times, Unit unit, Applicant applicant)
            {
                var application = new RentalApplication
                {
                    Unit = unit,
                    Applicant = applicant,
                    Status = (long)path[^1],
                    Created = times[0],
                    // Set on every submit, so it holds the latest one.
                    Submitted = SubmitTimes(path, times).Select(t => (DateTime?)t).LastOrDefault(),
                    ApplicantInformationVersion = Guid.NewGuid(),
                    ResidenceHistoryVersion = Guid.NewGuid()
                };
                // The starter is on it too, the same as when the app creates one.
                application.ApplicationApplicants.Add(new ApplicationApplicant
                {
                    Applicant = applicant,
                    Added = times[0],
                    AddedByUser = applicant.UserId!
                });

                // Anything submitted has both sections saved; a draft could be anywhere.
                var sectionsDone = path.Contains(ApplicationStatus.Submitted) ? 2 : f.Random.Int(0, 2);
                if (sectionsDone >= 1)
                {
                    application.ApplicantInformation = new ApplicantInformation
                    {
                        Name = applicant.Name,
                        Phone = applicant.Phone,
                        Email = applicant.Email,
                        CurrentAddress = applicant.CurrentAddress
                    };
                    application.ApplicantInformationSaved = true;
                }
                if (sectionsDone >= 2)
                {
                    AddResidences(application, times[0]);
                    application.ResidenceHistorySaved = true;
                }

                // The manager who claimed it most recently. Claims, releases and the review that follows are all theirs.
                string? reviewer = null;
                for (var i = 0; i < path.Length; i++)
                {
                    var from = i == 0 ? (ApplicationStatus?)null : path[i - 1];
                    var to = path[i];
                    if (from is { } previous && !ApplicationWorkflow.CanTransition(previous, to))
                    {
                        throw new InvalidOperationException($"Demo data path has an invalid transition {previous} -> {to}.");
                    }

                    if (to == ApplicationStatus.UnderReview) reviewer = f.PickRandom(managers).Id;

                    ReviewOutcome? outcome = to switch
                    {
                        ApplicationStatus.Approved => ReviewOutcome.Approve,
                        ApplicationStatus.Returned => ReviewOutcome.Return,
                        ApplicationStatus.Denied => ReviewOutcome.Deny,
                        _ => null
                    };
                    application.ApplicationStatusHistories.Add(new ApplicationStatusHistory
                    {
                        PreviousStatus = (long?)from ?? 0,
                        NewStatus = (long)to,
                        Outcome = outcome,
                        Comment = outcome switch
                        {
                            ReviewOutcome.Return => f.PickRandom(ReturnComments),
                            ReviewOutcome.Deny => f.PickRandom(DenyComments),
                            ReviewOutcome.Approve => f.Random.Bool(0.3f) ? "Welcome aboard!" : null,
                            _ => null
                        },
                        // Claims, releases and reviews are done by the property manager who claimed it; everything else
                        // (submit, withdraw) by the applicant.
                        ChangedByUser = outcome is not null || to == ApplicationStatus.UnderReview
                            || (from == ApplicationStatus.UnderReview && to == ApplicationStatus.Submitted)
                            ? reviewer! : applicant.UserId!,
                        ChangedDate = times[i]
                    });
                }

                // Still Under Review: record the open claim, like ApplicationService.ClaimAsync does.
                if (path[^1] == ApplicationStatus.UnderReview)
                {
                    application.ReviewerUser = reviewer;
                    application.ReviewClaimed = times[^1];
                }
                return application;
            }

            /// <summary>One to three prior residences, back to back, ending a little before the application was started.</summary>
            private void AddResidences(RentalApplication application, DateTime created)
            {
                var moveOut = DateOnly.FromDateTime(created).AddDays(-f.Random.Int(0, 60));
                for (var i = f.Random.Int(1, 3); i > 0; i--)
                {
                    var moveIn = moveOut.AddMonths(-f.Random.Int(6, 48));
                    application.Residences.Add(new Residence
                    {
                        Address = Fit($"{f.Address.StreetAddress()}, {f.Address.City()}"),
                        LandlordName = Fit(f.Name.FullName()),
                        LandlordPhone = f.Phone.PhoneNumber("(###) ###-####"),
                        MoveInDate = moveIn,
                        MoveOutDate = moveOut
                    });
                    moveOut = moveIn.AddDays(-f.Random.Int(0, 30));
                }
            }
        }
    }
}
