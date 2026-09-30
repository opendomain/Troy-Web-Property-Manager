// Modal forms loaded from partial views.
//
// - Any element with data-modal-url loads that URL (a partial view) into the shared #app-modal.
// - A form inside the modal with data-modal-form is posted with fetch:
//     * HTML response (200, 422 or 409) -> validation failed, or someone else changed the data first;
//                         the partial is re-rendered in place with the message.
//     * JSON response  -> { success, refreshTarget, refreshUrl }: close the modal, then reload
//                         refreshTarget from refreshUrl, or the whole page when no target is given.
// - Buttons with data-confirm ask before submitting.
//
// This is here for Technical 1.b: load modals from partial views the controllers return. If the form doesn't
// validate, send back the same partial with the errors so the modal redraws in place; if it works, close the modal
// and refresh just the part of the page that changed.
//
// A few notes on how it's put together:
// - It's all server-rendered HTML, no SPA framework (the assessment doesn't allow one). The forms are normal Razor
//   partials with tag helpers, so validation attributes, antiforgery tokens and labels all come from the server.
//   All this script does is move HTML in and out of the shared modal in _Layout.cshtml.
// - Every modal works the same way, driven by data- attributes, so a new modal doesn't need any new JavaScript:
//     data-modal-url="..." on a button     -> GET that URL and show the partial in the modal
//     data-modal-form on a <form>           -> post it with fetch and deal with the response (below)
// - The status code tells us what happened: JSON = worked, 422 + HTML = same partial with errors,
//   409 + HTML = same partial, but someone else changed the data (the message says to reload),
//   401/403 = auth problem, anything else = something went wrong. See AppController.ModalSuccess / ModalInvalid /
//   ModalFailed.
// - We post the form as FormData, which includes the hidden antiforgery token, so the server's global filter checks
//   these like any other post. Server HTML goes in as-is since it's our own (already encoded) Razor output; any
//   error text we build here uses textContent.
// - It's all inside an IIFE with 'use strict' so nothing leaks into the global scope.
(() => {
    'use strict';

    const modalEl = document.getElementById('app-modal');
    if (!modalEl || !window.bootstrap) {
        return;
    }

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const content = modalEl.querySelector('.modal-content');
    // This header flags the request as AJAX, so ASP.NET Core's cookie auth sends back a 401/403 instead of
    // redirecting to the login page. handleAuthFailure depends on that.
    const ajaxHeaders = { 'X-Requested-With': 'XMLHttpRequest' };

    function errorHtml(message) {
        const div = document.createElement('div');
        div.className = 'modal-body';
        const alert = document.createElement('div');
        alert.className = 'alert alert-danger mb-0';
        alert.setAttribute('role', 'alert');
        alert.textContent = message;
        div.appendChild(alert);
        return div.outerHTML +
            '<div class="modal-footer"><button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Close</button></div>';
    }

    // Unobtrusive validation only scans the page once on load, so we have to parse any form we add later.
    function parseValidation() {
        const $ = window.jQuery;
        if (!$ || !$.validator || !$.validator.unobtrusive) {
            return;
        }
        const $form = $(content).find('form');
        $form.removeData('validator').removeData('unobtrusiveValidation');
        $.validator.unobtrusive.parse($form);
    }

    function focusFirstField() {
        const field = content.querySelector('input:not([type=hidden]):not([disabled]), select, textarea');
        if (field) {
            field.focus();
        }
    }

    function showContent(html) {
        content.innerHTML = html;
        parseValidation();
        if (modalEl.classList.contains('show')) {
            focusFirstField();
        } else {
            modal.show();
        }
    }

    // On AJAX requests the cookie auth handler returns 401 (signed out) or 403 (wrong role) instead of redirecting.
    // If they're signed out we do a full page load so they end up on the real login page with a return URL, not a
    // login page stuck inside the modal. Returns true if we dealt with it here.
    function handleAuthFailure(response) {
        if (response.status === 401 || response.redirected) {
            window.location.reload();
            return true;
        }
        if (response.status === 403) {
            showContent(errorHtml("You don't have permission to do that."));
            return true;
        }
        return false;
    }

    async function load(url) {
        try {
            const response = await fetch(url, { headers: ajaxHeaders });
            if (handleAuthFailure(response)) {
                return;
            }
            showContent(response.ok ? await response.text() : errorHtml('That item could not be loaded. It may have been removed.'));
        } catch {
            showContent(errorHtml('Could not reach the server. Please try again.'));
        }
    }

    // The happy path: close the modal and refresh whatever changed. The server tells us which region (a CSS selector)
    // and the URL of a partial to redraw it, so only that part of the page updates.
    async function refresh(result) {
        const target = result.refreshTarget && document.querySelector(result.refreshTarget);
        if (!target || !result.refreshUrl) {
            window.location.reload();
            return;
        }
        const response = await fetch(result.refreshUrl, { headers: ajaxHeaders });
        if (response.ok && !response.redirected) {
            // If the partial's root element is the target itself (like _ResidenceHistory), swap the whole thing;
            // otherwise it's just the inside of the target (like _PropertyList).
            const template = document.createElement('template');
            template.innerHTML = (await response.text()).trim();
            const root = template.content.firstElementChild;
            if (template.content.childElementCount === 1 && root.id && root.id === target.id) {
                target.replaceWith(root);
            } else {
                target.replaceChildren(template.content);
            }
        } else {
            window.location.reload();
        }
    }

    modalEl.addEventListener('shown.bs.modal', focusFirstField);

    document.addEventListener('click', (event) => {
        const trigger = event.target.closest('[data-modal-url]');
        if (trigger) {
            event.preventDefault();
            load(trigger.dataset.modalUrl);
            return;
        }

        const confirmButton = event.target.closest('[data-confirm]');
        if (confirmButton && !window.confirm(confirmButton.dataset.confirm)) {
            event.preventDefault();
        }
    });

    // Posts modal forms with fetch. We listen on the modal content (event delegation), so forms added later -
    // including one that got redrawn with errors - just work without re-binding anything.
    content.addEventListener('submit', async (event) => {
        const form = event.target.closest('form[data-modal-form]');
        if (!form) {
            return;
        }
        event.preventDefault();

        // Check in the browser first (same DataAnnotations rules, through jQuery unobtrusive) so obvious mistakes
        // don't need a round trip. The server checks again no matter what.
        const $ = window.jQuery;
        if ($ && $.fn.valid && !$(form).valid()) {
            return;
        }

        // Disable the buttons while we wait so a double-click doesn't post twice.
        const submitButtons = form.querySelectorAll('[type=submit]');
        submitButtons.forEach((b) => { b.disabled = true; });
        try {
            // Passing the submitter means the clicked button's name/value gets posted too, like a normal form post.
            const response = await fetch(form.action, { method: 'POST', body: new FormData(form, event.submitter), headers: ajaxHeaders });
            if (handleAuthFailure(response)) {
                return;
            }
            const type = response.headers.get('content-type') || '';
            if (response.ok && type.includes('application/json')) {
                const result = await response.json();
                modal.hide();
                await refresh(result);
            } else if ((response.ok || response.status === 422 || response.status === 409) && type.includes('text/html')) {
                // 422 means it didn't validate (AppController.ModalInvalid); 409 means someone else got there first
                // (AppController.ModalFailed). Either way, redraw the partial - it has the message in it.
                showContent(await response.text());
            } else {
                showContent(errorHtml('Something went wrong. Please close this dialog and try again.'));
            }
        } catch {
            showContent(errorHtml('Could not reach the server. Please try again.'));
        } finally {
            submitButtons.forEach((b) => { b.disabled = false; });
        }
    });
})();
