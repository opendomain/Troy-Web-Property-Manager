// Modal forms loaded from partial views.
//
// - Any element with data-modal-url loads that URL (a partial view) into the shared #app-modal.
// - A form inside the modal with data-modal-form is posted with fetch:
//     * HTML response (200 or 422) -> validation failed; the partial is re-rendered in place.
//     * JSON response  -> { success, refreshTarget, refreshUrl }: close the modal, then reload
//                         refreshTarget from refreshUrl, or the whole page when no target is given.
// - Buttons with data-confirm ask before submitting.
(() => {
    'use strict';

    const modalEl = document.getElementById('app-modal');
    if (!modalEl || !window.bootstrap) {
        return;
    }

    const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
    const content = modalEl.querySelector('.modal-content');
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

    // Unobtrusive validation only scans the page on load, so newly inserted forms must be parsed.
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

    // For AJAX requests the cookie auth handler returns 401 (signed out) or 403 (wrong role) instead of
    // redirecting. A signed-out user gets a full page load so they land on the login page with a return URL,
    // rather than seeing the login page inside the modal. Returns true when the response was handled here.
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

    async function refresh(result) {
        const target = result.refreshTarget && document.querySelector(result.refreshTarget);
        if (!target || !result.refreshUrl) {
            window.location.reload();
            return;
        }
        const response = await fetch(result.refreshUrl, { headers: ajaxHeaders });
        if (response.ok && !response.redirected) {
            // A partial whose root element is the target itself (e.g. _ResidenceHistory) replaces it;
            // otherwise the partial is the target's contents (e.g. _PropertyList).
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

    content.addEventListener('submit', async (event) => {
        const form = event.target.closest('form[data-modal-form]');
        if (!form) {
            return;
        }
        event.preventDefault();

        const $ = window.jQuery;
        if ($ && $.fn.valid && !$(form).valid()) {
            return;
        }

        const submitButtons = form.querySelectorAll('[type=submit]');
        submitButtons.forEach((b) => { b.disabled = true; });
        try {
            const response = await fetch(form.action, { method: 'POST', body: new FormData(form), headers: ajaxHeaders });
            if (handleAuthFailure(response)) {
                return;
            }
            const type = response.headers.get('content-type') || '';
            if (response.ok && type.includes('application/json')) {
                const result = await response.json();
                modal.hide();
                await refresh(result);
            } else if ((response.ok || response.status === 422) && type.includes('text/html')) {
                // 422 = validation failed (AppController.ModalInvalid): re-render the same partial with its errors.
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
