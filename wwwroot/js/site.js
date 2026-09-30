// Modal forms loaded from partial views.
//
// - Any element with data-modal-url loads that URL (a partial view) into the shared #app-modal.
// - A form inside the modal with data-modal-form is posted with fetch:
//     * HTML response  -> validation failed; the partial is re-rendered in place.
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

    async function load(url) {
        try {
            const response = await fetch(url, { headers: ajaxHeaders });
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
        if (response.ok) {
            target.innerHTML = await response.text();
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
            const type = response.headers.get('content-type') || '';
            if (response.ok && type.includes('application/json')) {
                const result = await response.json();
                modal.hide();
                await refresh(result);
            } else if (response.ok) {
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
