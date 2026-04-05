// Mail-Archiver shared client-side helpers
(function () {
    const namespace = window.mailArchiver = window.mailArchiver || {};
    const autoRefreshStates = new Map();
    let pageUiInitialized = false;
    let themeToggleInitialized = false;
    let navbarOutsideClickInitialized = false;

    function initializeActiveNav() {
        const currentPath = window.location.pathname;
        const navLinks = document.querySelectorAll('.navbar-nav .nav-link');

        navLinks.forEach(link => {
            const href = link.getAttribute('href');
            if (href && currentPath === href) {
                link.classList.add('active');
            }
        });
    }

    function initializeNavbarOutsideClick() {
        if (navbarOutsideClickInitialized) {
            return;
        }

        const navbarToggler = document.querySelector('.navbar-toggler');
        const navbarCollapse = document.querySelector('.navbar-collapse');

        if (!navbarToggler || !navbarCollapse) {
            return;
        }

        document.addEventListener('click', function (event) {
            const isClickInsideNavbar = navbarToggler.contains(event.target) || navbarCollapse.contains(event.target);

            if (!isClickInsideNavbar && navbarCollapse.classList.contains('show')) {
                const bsCollapse = new bootstrap.Collapse(navbarCollapse, {
                    toggle: false
                });
                bsCollapse.hide();
            }
        });

        navbarOutsideClickInitialized = true;
    }

    function initializeTooltips(root = document) {
        const tooltipTriggerList = root.querySelectorAll('[data-bs-toggle="tooltip"]');
        tooltipTriggerList.forEach(tooltipTriggerEl => {
            if (bootstrap.Tooltip.getInstance(tooltipTriggerEl)) {
                return;
            }

            new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }

    function initializeAutoDismissAlerts(root = document) {
        const alerts = root.querySelectorAll('.alert:not(.alert-persistent)');

        alerts.forEach(alert => {
            if (alert.dataset.autoDismissInitialized === 'true') {
                return;
            }

            alert.dataset.autoDismissInitialized = 'true';

            window.setTimeout(() => {
                if (!document.body.contains(alert)) {
                    return;
                }

                const existingAlert = bootstrap.Alert.getOrCreateInstance(alert);
                existingAlert.close();
            }, 5000);
        });
    }

    function initializeThemeToggle() {
        if (themeToggleInitialized) {
            return;
        }

        const themeToggle = document.getElementById('theme-toggle');
        const currentTheme = localStorage.getItem('theme') || (window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');

        if (currentTheme === 'dark') {
            document.documentElement.setAttribute('data-theme', 'dark');
            if (themeToggle) {
                themeToggle.innerHTML = '<i class="bi bi-sun"></i>';
            }
        }

        if (themeToggle) {
            themeToggle.addEventListener('click', function () {
                const activeTheme = document.documentElement.getAttribute('data-theme');
                const newTheme = activeTheme === 'dark' ? 'light' : 'dark';

                document.documentElement.setAttribute('data-theme', newTheme);
                localStorage.setItem('theme', newTheme);
                this.innerHTML = newTheme === 'dark' ? '<i class="bi bi-sun"></i>' : '<i class="bi bi-moon"></i>';
            });
        }

        themeToggleInitialized = true;
    }

    function updateLastUpdatedElements(root = document) {
        const elements = root.querySelectorAll('[data-auto-refresh-last-updated]');
        if (!elements.length) {
            return;
        }

        const timestamp = new Date().toLocaleTimeString();
        elements.forEach(element => {
            const label = element.dataset.autoRefreshLastUpdated || '';
            element.textContent = label ? `${label} ${timestamp}` : timestamp;
        });
    }

    function initializeDynamicUi(root = document) {
        initializeTooltips(root);
        initializeAutoDismissAlerts(root);
        updateLastUpdatedElements(root);
    }

    function clearAutoRefreshState(containerId) {
        const existingState = autoRefreshStates.get(containerId);
        if (!existingState) {
            return;
        }

        if (existingState.timerId) {
            window.clearTimeout(existingState.timerId);
        }

        autoRefreshStates.delete(containerId);
    }

    function getRefreshUrl(container) {
        return container.dataset.autoRefreshUrl || window.location.href;
    }

    function buildRefreshRequestUrl(container) {
        const baseUrl = getRefreshUrl(container);
        const url = new URL(baseUrl, window.location.origin);
        url.searchParams.set('_mar_refresh', Date.now().toString());
        return url.toString();
    }

    function isAutoRefreshEnabled(container) {
        return container.dataset.autoRefreshEnabled !== 'false';
    }

    function parseRefreshInterval(value) {
        const parsed = Number.parseInt(value || '0', 10);
        return Number.isFinite(parsed) ? parsed : 0;
    }

    function getDefaultRefreshInterval() {
        return parseRefreshInterval(document.body?.dataset.defaultAutoRefreshInterval);
    }

    function getRefreshInterval(container) {
        if (!isAutoRefreshEnabled(container)) {
            return 0;
        }

        const explicitValue = parseRefreshInterval(container.dataset.autoRefreshInterval);
        if (explicitValue > 0) {
            return explicitValue;
        }

        return getDefaultRefreshInterval();
    }

    function scheduleAutoRefresh(container) {
        if (!container || !container.id) {
            return;
        }

        const interval = getRefreshInterval(container);
        if (interval <= 0) {
            clearAutoRefreshState(container.id);
            return;
        }

        clearAutoRefreshState(container.id);

        const state = {
            inFlight: false,
            timerId: window.setTimeout(() => {
                namespace.refreshContainerById(container.id);
            }, interval)
        };

        autoRefreshStates.set(container.id, state);
    }

    async function refreshContainerById(containerId, options = {}) {
        const currentContainer = document.getElementById(containerId);
        if (!currentContainer) {
            clearAutoRefreshState(containerId);
            return false;
        }

        const state = autoRefreshStates.get(containerId) || { inFlight: false, timerId: null };
        if (state.inFlight) {
            return false;
        }

        if (state.timerId) {
            window.clearTimeout(state.timerId);
            state.timerId = null;
        }

        if (document.hidden && currentContainer.dataset.autoRefreshWhenHidden !== 'true') {
            autoRefreshStates.set(containerId, state);
            scheduleAutoRefresh(currentContainer);
            return false;
        }

        state.inFlight = true;
        autoRefreshStates.set(containerId, state);

        try {
            const response = await fetch(buildRefreshRequestUrl(currentContainer), {
                headers: {
                    'X-Requested-With': 'XMLHttpRequest',
                    'X-MailArchiver-Fragment': containerId,
                    'Cache-Control': 'no-cache, no-store, max-age=0',
                    'Pragma': 'no-cache'
                },
                cache: 'no-store',
                credentials: 'same-origin'
            });

            if (!response.ok) {
                throw new Error(`Refresh request failed with status ${response.status}`);
            }

            const html = await response.text();
            const parsedDocument = new DOMParser().parseFromString(html, 'text/html');
            const replacement = parsedDocument.getElementById(containerId);

            if (!replacement) {
                throw new Error(`Could not find refreshed fragment '${containerId}' in response.`);
            }

            currentContainer.replaceWith(replacement);

            initializeDynamicUi(replacement);
            scheduleAutoRefresh(replacement);

            document.dispatchEvent(new CustomEvent('mailarchiver:fragment-refreshed', {
                detail: {
                    containerId: containerId,
                    element: replacement,
                    manual: options.manual === true
                }
            }));

            return true;
        } catch (error) {
            console.error('Error refreshing fragment:', error);
            scheduleAutoRefresh(currentContainer);
            return false;
        } finally {
            const latestState = autoRefreshStates.get(containerId) || state;
            latestState.inFlight = false;
            autoRefreshStates.set(containerId, latestState);
        }
    }

    function initializeAutoRefresh(root = document) {
        if (root.matches && (root.hasAttribute('data-auto-refresh-url') || root.hasAttribute('data-auto-refresh-interval'))) {
            scheduleAutoRefresh(root);
        }

        const containers = root.querySelectorAll('[data-auto-refresh-url], [data-auto-refresh-interval]');
        containers.forEach(scheduleAutoRefresh);
    }

    function initializePageUi() {
        if (pageUiInitialized) {
            return;
        }

        initializeActiveNav();
        initializeNavbarOutsideClick();
        initializeThemeToggle();
        initializeDynamicUi(document);
        initializeAutoRefresh(document);

        document.addEventListener('visibilitychange', function () {
            if (!document.hidden) {
                initializeAutoRefresh(document);
            }
        });

        pageUiInitialized = true;
    }

    namespace.initializeDynamicUi = initializeDynamicUi;
    namespace.initializeAutoRefresh = initializeAutoRefresh;
    namespace.refreshContainerById = function (containerId) {
        return refreshContainerById(containerId, { manual: true });
    };

    document.addEventListener('DOMContentLoaded', initializePageUi);
})();

// Funktion zum Formatieren von Dateigröße
function formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';

    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(1024));

    return parseFloat((bytes / Math.pow(1024, i)).toFixed(2)) + ' ' + sizes[i];
}

// Bestätigungsdialog für gefährliche Aktionen
function confirmAction(message) {
    return confirm(message || 'Sind Sie sicher, dass Sie diese Aktion ausführen möchten?');
}

// Datum-Formatter
function formatDate(dateString) {
    const date = new Date(dateString);
    return date.toLocaleDateString('de-DE', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}
