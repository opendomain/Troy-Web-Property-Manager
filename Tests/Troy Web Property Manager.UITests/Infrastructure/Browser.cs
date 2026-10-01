using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;

namespace Troy_Web_Property_Manager.UITests.Infrastructure
{
    /// <summary>
    /// A headless Chrome window, with the few actions the page objects need: go to a page, find, click, type, and
    /// wait for the app's JavaScript (modals, the data grid). Every wait has a timeout, and when one runs out the
    /// failure says what it was waiting for, where the browser was, and saves a screenshot and the page source under
    /// <c>bin/.../UiTestArtifacts</c>.
    /// </summary>
    /// <remarks>
    /// Selenium Manager (built into Selenium 4) finds the installed Chrome and downloads the matching ChromeDriver on
    /// first use, so the only requirement is Chrome itself.
    /// </remarks>
    public sealed class Browser : IDisposable
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

        private static readonly string ArtifactsDirectory = Path.Combine(AppContext.BaseDirectory, "UiTestArtifacts");

        public Browser(string baseUrl)
        {
            BaseUrl = baseUrl;
            var options = new ChromeOptions();
            options.AddArguments("--headless=new", "--window-size=1400,1000", "--disable-gpu",
                "--disable-search-engine-choice-screen", "--no-first-run", "--lang=en-US");
            Driver = new ChromeDriver(options);
        }

        public IWebDriver Driver { get; }

        public string BaseUrl { get; }

        /// <summary>The current path and query, e.g. "/Applications/Edit/12?section=Summary".</summary>
        public string PathAndQuery => new Uri(Driver.Url).PathAndQuery;

        /// <summary>The visible text of the whole page.</summary>
        public string PageText => Driver.FindElement(By.TagName("body")).Text;

        public string PageSource => Driver.PageSource;

        // ---------------- Navigation ----------------

        public void Go(string path)
        {
            Driver.Navigate().GoToUrl(BaseUrl + path);
            WaitForDocumentReady();
        }

        public void Reload()
        {
            Driver.Navigate().Refresh();
            WaitForDocumentReady();
        }

        /// <summary>Clicks something that loads a new page (a link, or a button in a plain form) and waits for it.</summary>
        public void ClickAndWaitForPage(By by)
        {
            var oldPage = Driver.FindElement(By.TagName("html"));
            Click(by);
            WaitUntil(() => IsStale(oldPage), $"a new page after clicking {by}");
            WaitForDocumentReady();
        }

        private void WaitForDocumentReady()
        {
            WaitUntil(() => Equals(Js("return document.readyState"), "complete"), "the page to finish loading");
        }

        private static bool IsStale(IWebElement element)
        {
            try
            {
                _ = element.Enabled;
                return false;
            }
            catch (StaleElementReferenceException)
            {
                return true;
            }
        }

        // ---------------- Finding things ----------------

        /// <summary>The first visible match, waiting for it to appear.</summary>
        public IWebElement Find(By by)
        {
            return WaitUntil(() => Driver.FindElements(by).FirstOrDefault(e => e.Displayed), $"{by} to be visible");
        }

        public IReadOnlyList<IWebElement> FindAll(By by)
        {
            return Driver.FindElements(by).Where(e => e.Displayed).ToList();
        }

        /// <summary>True if there's a visible match right now (no waiting).</summary>
        public bool IsVisible(By by)
        {
            return Driver.FindElements(by).Any(e => e.Displayed);
        }

        public string Text(By by)
        {
            return Find(by).Text;
        }

        public string Value(By by)
        {
            return Find(by).GetAttribute("value") ?? "";
        }

        // ---------------- Doing things ----------------

        public void Click(By by)
        {
            var element = WaitUntil(() => Driver.FindElements(by).FirstOrDefault(e => e.Displayed && e.Enabled),
                $"{by} to be clickable");
            // Instant, because Bootstrap turns on smooth scrolling and a click during the animation misses.
            Js("arguments[0].scrollIntoView({block: 'center', behavior: 'instant'});", element);
            element.Click();
        }

        public void Type(By by, string text)
        {
            var element = Find(by);
            element.Clear();
            element.SendKeys(text);
        }

        /// <summary>
        /// Sets a field's value directly and fires the input/change events. Used for date inputs, whose typed format
        /// depends on the browser's locale, and for blanking fields the app pre-fills.
        /// </summary>
        public void SetValue(By by, string value)
        {
            Js("arguments[0].value = arguments[1];" +
               "arguments[0].dispatchEvent(new Event('input', {bubbles: true}));" +
               "arguments[0].dispatchEvent(new Event('change', {bubbles: true}));", Find(by), value);
        }

        public void SelectByText(By by, string text)
        {
            new SelectElement(Find(by)).SelectByText(text);
        }

        public void SelectByValue(By by, string value)
        {
            new SelectElement(Find(by)).SelectByValue(value);
        }

        /// <summary>The option texts of a dropdown.</summary>
        public IReadOnlyList<string> Options(By by)
        {
            return new SelectElement(Find(by)).Options.Select(o => o.Text.Trim()).ToList();
        }

        public object? Js(string script, params object[] args)
        {
            return ((IJavaScriptExecutor)Driver).ExecuteScript(script, args);
        }

        /// <summary>
        /// Makes a request from inside the page with the browser's cookies, and returns the status code and the URL it
        /// ended up on (after any redirects). With <paramref name="ajax"/> it sends X-Requested-With, so the cookie
        /// handler answers 401/403 instead of redirecting to the login or access denied page. Lets security tests check
        /// exact status codes, which a normal page load doesn't show.
        /// </summary>
        public FetchResult Fetch(string path, string method = "GET", string? formBody = null, bool ajax = false)
        {
            // The page's origin is needed for a same-origin fetch; make sure we're on a page of the app.
            if (!Driver.Url.StartsWith(BaseUrl, StringComparison.OrdinalIgnoreCase)) Go("/");
            var result = (IReadOnlyCollection<object>?)((IJavaScriptExecutor)Driver).ExecuteAsyncScript(
                """
                const [path, method, body, ajax, done] = arguments;
                const headers = {};
                if (ajax) headers['X-Requested-With'] = 'XMLHttpRequest';
                if (body !== null) headers['Content-Type'] = 'application/x-www-form-urlencoded';
                fetch(path, { method, headers, body: body ?? undefined, credentials: 'same-origin' })
                    .then(async r => done([r.status, r.url, await r.text()]))
                    .catch(e => done([0, '', String(e)]));
                """, path, method, formBody!, ajax);
            var parts = result?.ToArray() ?? throw new InvalidOperationException($"Fetching {path} returned nothing.");
            return new FetchResult(Convert.ToInt32(parts[0]), new Uri((string)parts[1] is { Length: > 0 } u ? u : BaseUrl).PathAndQuery, (string)parts[2]);
        }

        /// <summary>The page's antiforgery token (from any form on it), for requests a test builds by hand.</summary>
        public string AntiforgeryToken()
        {
            return Driver.FindElement(By.CssSelector("input[name='__RequestVerificationToken']")).GetAttribute("value")!;
        }

        // ---------------- The shared modal (site.js) ----------------

        public const string ModalXPath = "//div[@id='app-modal']";

        /// <summary>Waits for the modal to be open with content in it (and with this title, if given).</summary>
        public void WaitForModal(string? title = null)
        {
            WaitUntil(() => IsModalOpen() && (title is null || ModalTitle() == title),
                title is null ? "the modal to open" : $"the '{title}' modal to open");
        }

        public void WaitForModalClosed()
        {
            WaitUntil(() => !IsVisible(By.CssSelector("#app-modal")) && !IsVisible(By.CssSelector(".modal-backdrop")) && !IsModalAnimating(),
                "the modal to close");
        }

        /// <summary>
        /// True when the modal is open, has content, and has finished its open animation. Waiting for the animation
        /// matters: Bootstrap ignores hide() while a modal is still opening, so a save that comes back faster than
        /// the animation (easy for a test, not for a person) would leave the modal open.
        /// </summary>
        public bool IsModalOpen()
        {
            return Driver.FindElements(By.CssSelector("#app-modal.show .modal-content > *")).Any(e => e.Displayed) && !IsModalAnimating();
        }

        private bool IsModalAnimating()
        {
            return Js("const m = window.bootstrap && bootstrap.Modal.getInstance(document.getElementById('app-modal')); return !!(m && m._isTransitioning);") is true;
        }

        public string ModalTitle()
        {
            return Driver.FindElements(By.CssSelector("#app-modal.show .modal-title")).FirstOrDefault()?.Text ?? "";
        }

        public string ModalText => Find(By.CssSelector("#app-modal .modal-content")).Text;

        /// <summary>A button in the modal, by its text (e.g. "Save", "Remove").</summary>
        public static By ModalButton(string text)
        {
            return By.XPath($"{ModalXPath}{Xp.Button(text)}");
        }

        public void ClickModalButton(string text)
        {
            Click(ModalButton(text));
        }

        /// <summary>
        /// For modals whose success reloads the whole page (withdraw, review, release): clicks the button and waits
        /// for the new page.
        /// </summary>
        public void ClickModalButtonAndWaitForPage(string text)
        {
            ClickAndWaitForPage(ModalButton(text));
        }

        /// <summary>
        /// Turns off jQuery's client-side validation on the open modal's form, so Save really posts and the test sees
        /// the server's answer (Technical 1.b: the same partial comes back with the errors).
        /// </summary>
        public void DisableModalClientValidation()
        {
            Js("""
               const form = document.querySelector('#app-modal form');
               form.setAttribute('novalidate', '');
               if (window.jQuery) jQuery(form).removeData('validator').removeData('unobtrusiveValidation').off('.validate');
               """);
        }

        /// <summary>
        /// Leaves a marker on the current page. <see cref="IsSamePage"/> is true until the page reloads, which proves a
        /// modal save refreshed part of the page in place instead of reloading it (Technical 1.b).
        /// </summary>
        public void MarkPage()
        {
            Js("window.__uiTestMarker = 'still-here';");
        }

        public bool IsSamePage()
        {
            return Equals(Js("return window.__uiTestMarker || null;"), "still-here");
        }

        /// <summary>Waits for the modal to show this text - typically a validation or service error.</summary>
        public void WaitForModalText(string text)
        {
            WaitUntil(() => IsModalOpen() && ModalText.Contains(text), $"the modal to show \"{text}\"");
        }

        // ---------------- Waiting ----------------

        public void WaitForText(string text, TimeSpan? timeout = null)
        {
            WaitUntil(() => PageText.Contains(text), $"the page to show \"{text}\"", timeout);
        }

        public void WaitUntil(Func<bool> condition, string what, TimeSpan? timeout = null)
        {
            WaitUntil(() => condition() ? true : (bool?)null, what, timeout);
        }

        public T WaitUntil<T>(Func<T?> condition, string what, TimeSpan? timeout = null)
        {
            var deadline = DateTime.UtcNow + (timeout ?? DefaultTimeout);
            Exception? last = null;
            while (true)
            {
                try
                {
                    var result = condition();
                    if (result is not null && !(result is bool b && !b)) return result;
                }
                catch (Exception ex) when (ex is StaleElementReferenceException or NoSuchElementException or InvalidOperationException
                                               or WebDriverException)
                {
                    // The page changed under us (a modal redrew, the grid re-rendered) - just look again.
                    last = ex;
                }
                if (DateTime.UtcNow > deadline) throw Timeout(what, last);
                Thread.Sleep(100);
            }
        }

        private Exception Timeout(string what, Exception? last)
        {
            var file = SaveArtifacts(what);
            string text;
            try { text = PageText; } catch { text = "(page text unavailable)"; }
            if (text.Length > 1500) text = text[..1500] + "…";
            return new TimeoutException($"Timed out waiting for {what}.\nURL: {Driver.Url}\nScreenshot: {file}\nPage text:\n{text}", last);
        }

        /// <summary>Saves a screenshot and the page source, named after <paramref name="label"/>. Returns the PNG path.</summary>
        public string SaveArtifacts(string label)
        {
            try
            {
                Directory.CreateDirectory(ArtifactsDirectory);
                var safe = string.Concat(label.Select(c => char.IsLetterOrDigit(c) ? c : '-'));
                var name = $"{DateTime.Now:HHmmss-fff}-{safe[..Math.Min(safe.Length, 60)]}";
                var png = Path.Combine(ArtifactsDirectory, name + ".png");
                ((ITakesScreenshot)Driver).GetScreenshot().SaveAsFile(png);
                File.WriteAllText(Path.Combine(ArtifactsDirectory, name + ".html"), Driver.PageSource);
                return png;
            }
            catch (Exception ex)
            {
                return $"(couldn't save: {ex.Message})";
            }
        }

        public void Dispose()
        {
            Driver.Quit();
            Driver.Dispose();
        }
    }

    /// <summary>A response from <see cref="Browser.Fetch"/>.</summary>
    public sealed record FetchResult(int Status, string FinalPath, string Body);

    /// <summary>XPath snippets for finding things by their visible text.</summary>
    public static class Xp
    {
        /// <summary>
        /// A button (or link styled as one) whose text is exactly <paramref name="text"/>, anywhere below. Prefix it with
        /// <c>//main</c> (or another container) when the navbar has a link with the same text, like "Register".
        /// </summary>
        public static string Button(string text)
        {
            return $"//*[self::button or self::a][normalize-space()={Literal(text)}]";
        }

        public static string Link(string text)
        {
            return $"//a[normalize-space()={Literal(text)}]";
        }

        /// <summary>An XPath string literal, even when the text has quotes or apostrophes in it.</summary>
        public static string Literal(string text)
        {
            if (!text.Contains('\'')) return $"'{text}'";
            if (!text.Contains('"')) return $"\"{text}\"";
            return "concat('" + text.Replace("'", "', \"'\", '") + "')";
        }
    }
}
