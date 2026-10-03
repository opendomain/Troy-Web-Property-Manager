// Data grids: the client half of DataGridViewComponent (Views/Shared/Components/DataGrid/Default.cshtml).
//
// - Every element with data-grid is a grid. The attribute holds its settings as JSON (DataGridViewModel, camelCase):
//   dataUrl, columns, defaultSort, defaultDir, pageSize, filterFormId, emptyText.
// - The grid GETs dataUrl?page=&pageSize=&sort=&dir= plus the filter form's fields, and expects a PagedResult back:
//   { items: [...], total, page, pageSize }. The server does the filtering, sorting and paging (for applications it's
//   all in SQL), so this script never sorts or slices anything itself - it only draws what came back.
// - Clicking a sortable header sorts by it (again to flip the direction), the pager and rows-per-page change the page,
//   and submitting the filter form reloads the grid from page 1 instead of reloading the whole page.
// - The state goes into the page's query string (history.replaceState), so refresh, bookmarks and the Back button
//   from an application all come back to the same page of the same list. Defaults are left out to keep URLs short.
//   That assumes one grid per page, which is all we have.
//
// Same ground rules as site.js: no framework, everything inside an IIFE, and all data goes in with textContent (or
// as URL-encoded href values), never innerHTML, so a crafted email or property name can't inject markup.
(() => {
    'use strict';

    // X-Requested-With keeps the cookie auth handler from answering with a login page (see site.js).
    const requestHeaders = { 'Accept': 'application/json', 'X-Requested-With': 'XMLHttpRequest' };

    // Fills "{field}" placeholders from the row. encode is applied to each value (for hrefs).
    function fill(template, row, encode) {
        return template.replace(/\{(\w+)\}/g, (_, field) => {
            const value = row[field] ?? '';
            return encode ? encodeURIComponent(value) : String(value);
        });
    }

    // Server times come in the business's time zone with their offset (2026-10-03T14:05:00-04:00), so take the date
    // part as-is - the business's date - rather than letting Date shift it into the viewer's zone.
    function formatDate(value) {
        const match = typeof value === 'string' && /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
        return match ? new Date(+match[1], +match[2] - 1, +match[3]).toLocaleDateString() : '';
    }

    function cellText(column, row) {
        if (column.template) {
            return fill(column.template, row, false);
        }
        const value = row[column.field || column.key];
        if (value === null || value === undefined) {
            return '';
        }
        return column.format === 'date' ? formatDate(value) : String(value);
    }

    // Page numbers to show: the first, the last, and two either side of the current one, with null for a gap.
    function pageWindow(page, lastPage) {
        const pages = [];
        for (let p = 1; p <= lastPage; p++) {
            if (p === 1 || p === lastPage || Math.abs(p - page) <= 2) {
                pages.push(p);
            } else if (pages[pages.length - 1] !== null) {
                pages.push(null);
            }
        }
        return pages;
    }

    function initGrid(root) {
        const settings = JSON.parse(root.dataset.grid);
        const table = root.querySelector('table');
        const body = table.querySelector('tbody');
        const status = root.querySelector('.data-grid-status');
        const footer = root.querySelector('.data-grid-footer');
        const summary = root.querySelector('.data-grid-summary');
        const pager = root.querySelector('.pagination');
        const pageSizeSelect = root.querySelector('.data-grid-page-size');
        const filterForm = settings.filterFormId ? document.getElementById(settings.filterFormId) : null;
        const sortKeys = settings.columns.filter((c) => c.sortable).map((c) => c.key.toLowerCase());

        // Start from the query string, so a refreshed or bookmarked page comes back the same.
        const initial = new URLSearchParams(window.location.search);
        const state = {
            page: Math.max(1, parseInt(initial.get('page'), 10) || 1),
            pageSize: settings.pageSizes.includes(parseInt(initial.get('pageSize'), 10))
                ? parseInt(initial.get('pageSize'), 10) : settings.pageSize,
            sort: sortKeys.includes((initial.get('sort') || '').toLowerCase()) ? initial.get('sort') : settings.defaultSort,
            dir: ['asc', 'desc'].includes(initial.get('dir')) ? initial.get('dir') : settings.defaultDir
        };
        pageSizeSelect.value = String(state.pageSize);

        // Bumped on every load, so if responses come back out of order only the newest one is drawn.
        let latest = 0;
        // The pager button that was clicked (by its aria-label), so focus can go back to it after the pager is redrawn.
        let pagerFocus = null;

        function filterParams() {
            const params = new URLSearchParams();
            if (filterForm) {
                for (const [name, value] of new FormData(filterForm)) {
                    if (value !== '' && name !== '__RequestVerificationToken') {
                        params.append(name, value);
                    }
                }
            }
            return params;
        }

        // The filters in effect: what the form said when the page loaded (the server fills it from the query string)
        // or when it was last submitted. Changing a dropdown without pressing Filter doesn't change the list, even if
        // you page or sort afterwards - the same as a plain GET form.
        let filters = filterParams();

        function apiParams() {
            const params = new URLSearchParams(filters);
            params.set('page', state.page);
            params.set('pageSize', state.pageSize);
            params.set('sort', state.sort);
            params.set('dir', state.dir);
            return params;
        }

        function syncUrl() {
            const params = new URLSearchParams(filters);
            if (state.page !== 1) params.set('page', state.page);
            if (state.pageSize !== settings.pageSize) params.set('pageSize', state.pageSize);
            if (state.sort.toLowerCase() !== settings.defaultSort.toLowerCase() || state.dir !== settings.defaultDir) {
                params.set('sort', state.sort);
                params.set('dir', state.dir);
            }
            const query = params.toString();
            window.history.replaceState(null, '', window.location.pathname + (query ? '?' + query : ''));
        }

        function showStatus(message, isError) {
            status.textContent = message;
            status.hidden = !message;
            status.classList.toggle('text-danger', !!isError);
            status.classList.toggle('text-muted', !isError);
        }

        function drawHeaders() {
            root.querySelectorAll('th[aria-sort]').forEach((th) => {
                const key = th.querySelector('[data-sort]').dataset.sort;
                const sorted = key.toLowerCase() === state.sort.toLowerCase();
                th.setAttribute('aria-sort', sorted ? (state.dir === 'asc' ? 'ascending' : 'descending') : 'none');
                // Font Awesome sort icons (the span is aria-hidden; aria-sort above is what screen readers use).
                const icon = sorted ? (state.dir === 'asc' ? 'fa-sort-up' : 'fa-sort-down') : 'fa-sort data-grid-sort-idle';
                th.querySelector('.data-grid-sort-icon').className = 'data-grid-sort-icon fa-solid ms-1 ' + icon;
            });
        }

        function drawRows(items) {
            body.replaceChildren(...items.map((row) => {
                const tr = document.createElement('tr');
                for (const column of settings.columns) {
                    const td = document.createElement('td');
                    const text = cellText(column, row);
                    if (column.href) {
                        const a = document.createElement('a');
                        a.href = fill(column.href, row, true);
                        a.textContent = text;
                        td.appendChild(a);
                    } else {
                        td.textContent = text;
                    }
                    tr.appendChild(td);
                }
                return tr;
            }));
        }

        function pageItem(label, page, { active = false, disabled = false, ariaLabel } = {}) {
            const li = document.createElement('li');
            li.className = 'page-item' + (active ? ' active' : '') + (disabled ? ' disabled' : '');
            const el = document.createElement(active || disabled ? 'span' : 'button');
            el.className = 'page-link';
            el.textContent = label;
            if (active) {
                li.setAttribute('aria-current', 'page');
            } else if (!disabled) {
                el.type = 'button';
                el.dataset.page = page;
            }
            if (ariaLabel) {
                el.setAttribute('aria-label', ariaLabel);
            }
            li.appendChild(el);
            return li;
        }

        function drawPager(result) {
            const lastPage = Math.max(1, Math.ceil(result.total / result.pageSize));
            const first = (result.page - 1) * result.pageSize + 1;
            summary.textContent = `${first}–${first + result.items.length - 1} of ${result.total}`;
            const items = [pageItem('‹', result.page - 1, { disabled: result.page === 1, ariaLabel: 'Previous page' })];
            for (const p of pageWindow(result.page, lastPage)) {
                items.push(p === null ? pageItem('…', 0, { disabled: true })
                    : pageItem(String(p), p, { active: p === result.page, ariaLabel: `Page ${p}` }));
            }
            items.push(pageItem('›', result.page + 1, { disabled: result.page === lastPage, ariaLabel: 'Next page' }));
            pager.replaceChildren(...items);
        }

        // Paging redraws the pager, which would drop keyboard focus onto the page. Put it back on the same button, or
        // on the current page number when that button is gone or disabled (e.g. Next on the last page).
        function restorePagerFocus(label) {
            const same = [...pager.querySelectorAll('[aria-label]')].find((el) => el.getAttribute('aria-label') === label);
            const target = same && same.tagName === 'BUTTON' ? same : pager.querySelector('.active .page-link');
            if (target) {
                if (target.tagName !== 'BUTTON') {
                    target.tabIndex = -1;
                }
                target.focus();
            }
        }

        async function load(focusLabel = null) {
            const request = ++latest;
            pagerFocus = focusLabel;
            drawHeaders();
            table.setAttribute('aria-busy', 'true');
            let response;
            try {
                response = await fetch(settings.dataUrl + '?' + apiParams(), { headers: requestHeaders });
            } catch {
                if (request === latest) {
                    table.removeAttribute('aria-busy');
                    showStatus('Could not reach the server. Please try again.', true);
                }
                return;
            }
            if (request !== latest) {
                return;
            }
            table.removeAttribute('aria-busy');
            // Signed out since the page loaded: reload so the login page comes up with a return URL.
            if (response.status === 401 || response.redirected) {
                window.location.reload();
                return;
            }
            if (!response.ok) {
                showStatus('The list could not be loaded. Please try again.', true);
                return;
            }
            let result;
            try {
                result = await response.json();
            } catch {
                // Not our JSON (say, an error page from a proxy).
                if (request === latest) {
                    showStatus('The list could not be loaded. Please try again.', true);
                }
                return;
            }
            if (request !== latest) {
                return;
            }
            // The server moves a page past the end back to the last page; keep in step with it.
            state.page = result.page;
            syncUrl();
            drawRows(result.items);
            if (result.total === 0) {
                showStatus(settings.emptyText, false);
                footer.hidden = true;
            } else {
                showStatus('', false);
                drawPager(result);
                footer.hidden = false;
                if (pagerFocus) {
                    restorePagerFocus(pagerFocus);
                }
            }
        }

        root.addEventListener('click', (event) => {
            const sortButton = event.target.closest('[data-sort]');
            if (sortButton) {
                const key = sortButton.dataset.sort;
                state.dir = key.toLowerCase() === state.sort.toLowerCase() && state.dir === 'asc' ? 'desc' : 'asc';
                state.sort = key;
                state.page = 1;
                load();
                return;
            }
            const pageButton = event.target.closest('[data-page]');
            if (pageButton) {
                state.page = parseInt(pageButton.dataset.page, 10);
                // Only when the button had focus (always from the keyboard; on click, depending on the browser).
                load(document.activeElement === pageButton ? pageButton.getAttribute('aria-label') : null);
                // Keep the table in view after paging from the bottom of a long page.
                table.scrollIntoView({ block: 'nearest' });
            }
        });

        pageSizeSelect.addEventListener('change', () => {
            state.pageSize = parseInt(pageSizeSelect.value, 10);
            state.page = 1;
            load();
        });

        if (filterForm) {
            filterForm.addEventListener('submit', (event) => {
                event.preventDefault();
                filters = filterParams();
                state.page = 1;
                load();
            });
        }

        load();
    }

    document.querySelectorAll('[data-grid]').forEach(initGrid);
})();
