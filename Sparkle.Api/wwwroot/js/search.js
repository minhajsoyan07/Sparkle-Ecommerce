document.addEventListener('DOMContentLoaded', function () {
    // VERSION-BASED FORCE CLEAR - WILL PERMANENTLY DELETE ALL GARBAGE
    const SEARCH_VERSION = '2.0';
    const storedVersion = localStorage.getItem('sparkle_search_version');

    if (storedVersion !== SEARCH_VERSION) {
        localStorage.removeItem('sparkle_search_history');
        localStorage.setItem('sparkle_search_version', SEARCH_VERSION);
    }

    const searchInput = document.getElementById('searchInput');
    const searchDropdown = document.getElementById('searchDropdown');
    const searchForm = searchInput?.closest('form');

    if (!searchInput || !searchDropdown) {
        console.error('Sparkle Search: Elements not found');
        return;
    }

    let debounceTimer;

    // Focus: Show recent or suggestions
    searchInput.addEventListener('focus', () => {
        if (!searchInput.value.trim()) {
            showRecentSearches();
        } else {
            fetchSuggestions(searchInput.value.trim());
        }
    });

    // Input: Debounce
    searchInput.addEventListener('input', () => {
        clearTimeout(debounceTimer);
        const term = searchInput.value.trim();

        if (term.length < 1) {
            showRecentSearches();
        } else {
            debounceTimer = setTimeout(() => fetchSuggestions(term), 300);
        }
    });

    // Submit: Save
    if (searchForm) {
        searchForm.addEventListener('submit', () => {
            const term = searchInput.value.trim();
            if (term && term.length > 1 && /^[a-zA-Z0-9\s]+$/.test(term)) {
                saveSearch(term);
            }
        });
    }

    // Click outside
    document.addEventListener('click', (e) => {
        if (!searchInput.contains(e.target) && !searchDropdown.contains(e.target)) {
            searchDropdown.style.display = 'none';
        }
    });

    function saveSearch(term) {
        let history = getHistory();
        history = history.filter(item => item.toLowerCase() !== term.toLowerCase());
        history.unshift(term);
        if (history.length > 10) history.pop();
        localStorage.setItem('sparkle_search_history', JSON.stringify(history));
    }

    function removeSearch(e, term) {
        e.stopPropagation();
        let history = getHistory();
        history = history.filter(item => item !== term);
        localStorage.setItem('sparkle_search_history', JSON.stringify(history));
        if (!searchInput.value.trim()) {
            showRecentSearches();
        }
    }

    function getHistory() {
        try {
            const history = JSON.parse(localStorage.getItem('sparkle_search_history') || '[]');
            // ONLY return 100% valid entries
            return history.filter(t => t && typeof t === 'string' && t.length >= 2 && t.length <= 50 && /^[a-zA-Z0-9\s]+$/.test(t));
        } catch (e) {
            return [];
        }
    }

    function showRecentSearches() {
        // Handled by smart-search.js now for a professional UI
        // const history = getHistory();
        // ... legacy code commented out ...
        if (typeof renderSearchHistory === 'function') {
            renderSearchHistory();
        }
    }

    async function fetchSuggestions(term) {
        try {
            const response = await fetch(`/api/products/search-suggestions?term=${encodeURIComponent(term)}`);
            let suggestions = await response.json();

            if (suggestions.length === 0) {
                searchDropdown.style.display = 'none';
                return;
            }

            // Dedupe
            const seen = new Set();
            suggestions = suggestions.filter(s => {
                const lower = s.toLowerCase();
                if (seen.has(lower)) return false;
                seen.add(lower);
                return true;
            });

            let html = '<div class="search-dropdown-header">Suggestions</div>';
            suggestions.forEach(suggestion => {
                const regex = new RegExp(`(${term})`, 'gi');
                const highlighted = suggestion.replace(regex, '<span class="search-highlight">$1</span>');

                html += `
                    <div class="search-dropdown-item" onclick="selectSearch('${suggestion}')">
                        <i class="bi bi-search"></i>
                        <span>${highlighted}</span>
                    </div>
                `;
            });

            searchDropdown.innerHTML = html;
            searchDropdown.style.display = 'block';

        } catch (error) {
            console.error('Error fetching suggestions:', error);
        }
    }

    window.selectSearch = function (term) {
        searchInput.value = term;
        saveSearch(term);
        searchForm.submit();
    };
});
