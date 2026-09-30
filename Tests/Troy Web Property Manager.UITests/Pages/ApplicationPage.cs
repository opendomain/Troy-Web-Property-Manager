using System.Text.RegularExpressions;
using OpenQA.Selenium;
using Troy_Web_Property_Manager.UITests.Infrastructure;

namespace Troy_Web_Property_Manager.UITests.Pages
{
    /// <summary>Section 1's fields. Null leaves a field as it is.</summary>
    public sealed record ApplicantInfo(string? Name, string? Phone, string? Email, string? CurrentAddress)
    {
        public static ApplicantInfo Valid(string email = "alex.tester@example.com")
        {
            return new("Alex Tester", "518-555-0100", email, "9 Oak Ave, Troy");
        }
    }

    /// <summary>One residence for the residence modal. Dates are yyyy-MM-dd; null leaves a field as it is.</summary>
    public sealed record ResidenceData(string? Address, string? LandlordName, string? LandlordPhone, string? MoveIn, string? MoveOut)
    {
        public static ResidenceData Valid(string address = "5 Elm St, Albany")
        {
            return new(address, "Pat Landlord", "518-555-0199", "2020-01-01", "2024-12-31");
        }
    }

    /// <summary>One residence row as displayed.</summary>
    public sealed record ResidenceRow(string Address, string Landlord, string LandlordPhone, string MovedIn, string MovedOut);

    /// <summary>
    /// The application page (4.b): one page, one section at a time, one form. Applicants edit it; managers read it,
    /// claim/release/review it and keep private notes. Methods that post and reload wait for the new page; the
    /// modal ones wait for the modal to close (or for its error, in the "…ExpectingError" versions).
    /// </summary>
    public sealed partial class ApplicationPage(Browser browser)
    {
        public static string PathFor(int id, string? section = null)
        {
            return $"/Applications/Edit/{id}" + (section is null ? "" : $"?section={section}");
        }

        public ApplicationPage Open(int id, string? section = null)
        {
            browser.Go(PathFor(id, section));
            return this;
        }

        public int Id
        {
            get
            {
                var match = IdPattern().Match(browser.PathAndQuery);
                return match.Success ? int.Parse(match.Groups[1].Value) : throw new InvalidOperationException($"Not on an application page: {browser.PathAndQuery}");
            }
        }

        public string Status => browser.Text(By.XPath("//p[contains(., 'Status:')]/strong"));

        /// <summary>The section being shown ("Applicant information", "Residence history" or "Summary").</summary>
        public string Section => browser.Text(By.XPath("//p[starts-with(normalize-space(), 'Section:')]/strong"));

        public bool HasButton(string text)
        {
            return browser.IsVisible(By.XPath($"//main{Xp.Button(text)}"));
        }

        public bool IsSubmitEnabled => browser.Find(By.XPath(Xp.Button("Submit"))).Enabled;

        /// <summary>True when section 1's inputs are locked (the read-only fieldset).</summary>
        public bool IsApplicantInformationReadOnly => browser.Find(By.Id("ApplicantInformation_Name")).Enabled == false;

        // ---------------- Section 1 ----------------

        public void FillApplicantInformation(ApplicantInfo info)
        {
            if (info.Name is not null) browser.SetValue(By.Id("ApplicantInformation_Name"), info.Name);
            if (info.Phone is not null) browser.SetValue(By.Id("ApplicantInformation_Phone"), info.Phone);
            if (info.Email is not null) browser.SetValue(By.Id("ApplicantInformation_Email"), info.Email);
            if (info.CurrentAddress is not null) browser.SetValue(By.Id("ApplicantInformation_CurrentAddress"), info.CurrentAddress);
        }

        public ApplicantInfo ReadApplicantInformation()
        {
            return new(browser.Value(By.Id("ApplicantInformation_Name")), browser.Value(By.Id("ApplicantInformation_Phone")),
                browser.Value(By.Id("ApplicantInformation_Email")), browser.Value(By.Id("ApplicantInformation_CurrentAddress")));
        }

        /// <summary>The error shown under a section 1 field, e.g. FieldError("Phone").</summary>
        public string FieldError(string field)
        {
            return browser.FindAll(By.CssSelector($"span[data-valmsg-for='ApplicantInformation.{field}']")).Select(e => e.Text.Trim()).FirstOrDefault() ?? "";
        }

        /// <summary>Errors in the form's validation summary (not tied to a field).</summary>
        public string FormErrors =>
            string.Join("\n", browser.FindAll(By.CssSelector("main form .validation-summary-errors li")).Select(e => e.Text.Trim()));

        // ---------------- Buttons on the one form ----------------

        public void Continue()
        {
            browser.ClickAndWaitForPage(By.XPath(Xp.Button("Continue")));
        }

        public void Back()
        {
            browser.ClickAndWaitForPage(By.XPath($"//main{Xp.Button("Back")}"));
        }

        public void Next()
        {
            browser.ClickAndWaitForPage(By.XPath($"//main{Xp.Button("Next")}"));
        }

        public void Submit()
        {
            browser.ClickAndWaitForPage(By.XPath(Xp.Button("Submit")));
        }

        // ---------------- Summary ----------------

        /// <summary>The "Before you can submit" list on the Summary (empty when there's nothing blocking).</summary>
        public IReadOnlyList<string> Blockers =>
            browser.FindAll(By.XPath("//div[contains(@class,'alert')][.//strong[normalize-space()='Before you can submit:']]//li"))
                .Select(e => e.Text.Trim()).ToList();

        // ---------------- Residences (modal) ----------------

        public IReadOnlyList<ResidenceRow> Residences =>
            browser.FindAll(By.CssSelector("#residence-history tbody tr"))
                .Select(tr => tr.FindElements(By.TagName("td")).Select(td => td.Text.Trim()).ToList())
                .Where(c => c.Count == 6)
                .Select(c => new ResidenceRow(c[0], c[1], c[2], c[3], c[4])).ToList();

        /// <summary>The error lines shown under residence rows.</summary>
        public IReadOnlyList<string> ResidenceErrors =>
            browser.FindAll(By.CssSelector("#residence-history tbody ul.text-danger li")).Select(e => e.Text.Trim()).ToList();

        /// <summary>Section-level Residence History errors (e.g. "Add at least one prior residence.").</summary>
        public IReadOnlyList<string> ResidenceHistoryErrors =>
            browser.FindAll(By.CssSelector("#residence-history > div.text-danger")).Select(e => e.Text.Trim()).ToList();

        public bool CanAddResidence => browser.IsVisible(By.XPath(Xp.Button("Add residence")));

        public void OpenAddResidence()
        {
            browser.Click(By.XPath(Xp.Button("Add residence")));
            browser.WaitForModal("Add residence");
        }

        private static string ResidenceRowXPath(string address)
        {
            return $"//div[@id='residence-history']//tbody/tr[td[1][normalize-space()={Xp.Literal(address)}]]";
        }

        public void OpenEditResidence(string address)
        {
            browser.Click(By.XPath($"{ResidenceRowXPath(address)}{Xp.Button("Edit")}"));
            browser.WaitForModal("Edit residence");
        }

        public void FillResidenceModal(ResidenceData residence)
        {
            if (residence.Address is not null) browser.SetValue(By.CssSelector("#app-modal #Address"), residence.Address);
            if (residence.LandlordName is not null) browser.SetValue(By.CssSelector("#app-modal #LandlordName"), residence.LandlordName);
            if (residence.LandlordPhone is not null) browser.SetValue(By.CssSelector("#app-modal #LandlordPhone"), residence.LandlordPhone);
            if (residence.MoveIn is not null) browser.SetValue(By.CssSelector("#app-modal #MoveInDate"), residence.MoveIn);
            if (residence.MoveOut is not null) browser.SetValue(By.CssSelector("#app-modal #MoveOutDate"), residence.MoveOut);
            browser.ClickModalButton("Save");
        }

        /// <summary>Adds a residence that passes every rule; the modal closes and the list redraws.</summary>
        public void AddResidence(ResidenceData residence)
        {
            OpenAddResidence();
            FillResidenceModal(residence);
            browser.WaitForModalClosed();
            browser.WaitUntil(() => Residences.Any(r => r.Address == residence.Address), $"residence '{residence.Address}' in the list");
        }

        public void EditResidence(string address, ResidenceData changes)
        {
            OpenEditResidence(address);
            FillResidenceModal(changes);
            browser.WaitForModalClosed();
        }

        public void RemoveResidence(string address)
        {
            browser.Click(By.XPath($"{ResidenceRowXPath(address)}{Xp.Button("Remove")}"));
            browser.WaitForModal("Remove residence");
            browser.ClickModalButton("Remove");
            browser.WaitForModalClosed();
            browser.WaitUntil(() => Residences.All(r => r.Address != address), $"residence '{address}' to go away");
        }

        // ---------------- Applicant actions ----------------

        public void Withdraw()
        {
            browser.Click(By.XPath($"//main{Xp.Button("Withdraw")}"));
            browser.WaitForModal("Withdraw application");
            browser.ClickModalButtonAndWaitForPage("Withdraw");
        }

        /// <summary>The reviewer's comment shown to the applicant after a Return or Deny, or null.</summary>
        public string? ReviewComment =>
            browser.FindAll(By.XPath("//div[contains(@class,'alert')][.//strong[normalize-space()=\"Reviewer's comment:\"]]"))
                .Select(e => e.Text.Replace("Reviewer's comment:", "").Trim()).FirstOrDefault();

        // ---------------- Applicants on the application (bonus 5) ----------------

        /// <summary>The applicant badges, e.g. "a@b.com (started it) (you)".</summary>
        public IReadOnlyList<string> Applicants =>
            browser.FindAll(By.CssSelector("#application-applicants .badge")).Select(e => e.Text.Trim()).ToList();

        public void OpenAddApplicant()
        {
            browser.Click(By.XPath(Xp.Button("Add applicant")));
            browser.WaitForModal("Add applicant");
        }

        public void FillAddApplicantModal(string email)
        {
            browser.SetValue(By.CssSelector("#app-modal #Email"), email);
            browser.ClickModalButton("Add");
        }

        public void AddApplicant(string email)
        {
            OpenAddApplicant();
            FillAddApplicantModal(email);
            browser.WaitForModalClosed();
            browser.WaitUntil(() => Applicants.Any(a => a.StartsWith(email, StringComparison.OrdinalIgnoreCase)), $"{email} on the application");
        }

        /// <summary>Removes another applicant (or leaves, if it's you - which lands on your application list).</summary>
        public void RemoveApplicant(string email, bool isYou = false)
        {
            browser.Click(By.XPath($"//div[@id='application-applicants']//span[contains(@class,'badge')][starts-with(normalize-space(), {Xp.Literal(email)})]{Xp.Button(isYou ? "Leave" : "Remove")}"));
            browser.WaitForModal(isYou ? "Leave application" : "Remove applicant");
            if (isYou)
            {
                browser.ClickModalButtonAndWaitForPage("Leave");
                return;
            }
            browser.ClickModalButton("Remove");
            browser.WaitForModalClosed();
            browser.WaitUntil(() => Applicants.All(a => !a.StartsWith(email, StringComparison.OrdinalIgnoreCase)), $"{email} to be removed");
        }

        // ---------------- Manager actions ----------------

        /// <summary>"Claimed by you · time" / "Claimed by x@y · time" under the title, managers only.</summary>
        public string? ClaimLine =>
            browser.FindAll(By.XPath("//p[starts-with(normalize-space(), 'Claimed by')]")).Select(e => e.Text.Trim()).FirstOrDefault();

        public void ClaimForReview()
        {
            browser.ClickAndWaitForPage(By.XPath(Xp.Button("Claim for review")));
        }

        public void Release()
        {
            browser.Click(By.XPath($"//main{Xp.Button("Release")}"));
            browser.WaitForModal("Release application");
            browser.ClickModalButtonAndWaitForPage("Release");
        }

        public void OpenReview()
        {
            browser.Click(By.XPath($"//main{Xp.Button("Review")}"));
            browser.WaitForModal();
        }

        /// <summary>Picks the outcome (Approve, Return, Deny) and comment in the open review modal and saves.</summary>
        public void FillReviewModal(string? outcome, string? comment)
        {
            if (outcome is not null) browser.Click(By.Id($"outcome-{outcome}"));
            if (comment is not null) browser.SetValue(By.CssSelector("#app-modal #Comment"), comment);
            browser.ClickModalButton("Save review");
        }

        /// <summary>Reviews successfully: the page reloads with the new status.</summary>
        public void Review(string outcome, string? comment = null)
        {
            OpenReview();
            browser.Click(By.Id($"outcome-{outcome}"));
            if (comment is not null) browser.SetValue(By.CssSelector("#app-modal #Comment"), comment);
            browser.ClickModalButtonAndWaitForPage("Save review");
        }

        /// <summary>The History panel's entries (managers only), oldest first.</summary>
        public IReadOnlyList<string> History =>
            browser.FindAll(By.XPath("//div[contains(@class,'card')][div[contains(@class,'card-header')][normalize-space()='History']]//li"))
                .Select(e => e.Text.Trim()).ToList();

        public bool HasHistoryPanel => browser.IsVisible(By.XPath("//div[contains(@class,'card-header')][normalize-space()='History']"));

        public bool HasManagerNotesPanel => browser.IsVisible(By.Id("manager-notes"));

        public string ManagerNotes => browser.Text(By.CssSelector("#manager-notes .card-body"));

        public void OpenEditManagerNotes()
        {
            browser.Click(By.XPath($"//div[@id='manager-notes']{Xp.Button("Edit")}"));
            browser.WaitForModal("Manager notes");
        }

        public void SaveManagerNotesModal(string notes)
        {
            browser.SetValue(By.CssSelector("#app-modal #Notes"), notes);
            browser.ClickModalButton("Save notes");
        }

        public void EditManagerNotes(string notes)
        {
            OpenEditManagerNotes();
            SaveManagerNotesModal(notes);
            browser.WaitForModalClosed();
            browser.WaitUntil(() => ManagerNotes.Contains(notes), "the notes panel to show the new notes");
        }

        [GeneratedRegex(@"/Applications/Edit/(\d+)")]
        private static partial Regex IdPattern();
    }
}
