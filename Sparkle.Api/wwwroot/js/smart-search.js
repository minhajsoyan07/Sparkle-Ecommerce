// ============================================
// AI-Powered Search Features
// Mini AI Assistant, Auto-Filters, Dynamic Placeholders
// ============================================

document.addEventListener('DOMContentLoaded', function () {
    // initAIAssistant(); // Removed
    initDynamicPlaceholders();
    initAutoFilterDetection();
    initSearchHistory();
});

// ============================================
// Mini AI Assistant (Floating Chat)
// ============================================

// AI Assistant removed for professional UI

// ============================================
// Dynamic Placeholders
// ============================================

function initDynamicPlaceholders() {
    const searchInput = document.getElementById('searchInput');
    if (!searchInput) return;

    const placeholders = [
        'Search for products...',
        'Find exactly what you need...',
        'Discover amazing deals...',
        'Looking for something special?'
    ];

    let currentIndex = 0;

    // Change placeholder every 3 seconds when input is empty and not focused
    setInterval(() => {
        if (searchInput.value === '' && document.activeElement !== searchInput) {
            currentIndex = (currentIndex + 1) % placeholders.length;
            searchInput.placeholder = placeholders[currentIndex];
        }
    }, 3000);
}

function showThinkingIndicator(show) {
    // Disabled for professional UI
}

// ============================================
// Auto-Filter Detection
// ============================================

function initAutoFilterDetection() {
    const searchParams = new URLSearchParams(window.location.search);
    const query = searchParams.get('q');

    if (!query) return;

    const detectedFilters = detectFilters(query.toLowerCase());

    if (Object.keys(detectedFilters).length > 0) {
        displayAutoFilters(detectedFilters);
    }
}

function detectFilters(query) {
    const filters = {};

    // Brand detection
    const brands = ['samsung', 'apple', 'xiaomi', 'realme', 'oppo', 'vivo', 'oneplus',
        'huawei', 'nokia', 'sony', 'lg', 'dell', 'hp', 'asus', 'lenovo',
        'acer', 'msi', 'razer'];

    for (const brand of brands) {
        if (query.includes(brand)) {
            filters.brand = brand.charAt(0).toUpperCase() + brand.slice(1);
            break;
        }
    }

    // Category detection
    const categories = {
        'phone': 'Mobiles & Tablets',
        'mobile': 'Mobiles & Tablets',
        'smartphone': 'Mobiles & Tablets',
        'laptop': 'Laptops & Computers',
        'computer': 'Laptops & Computers',
        'pc': 'Laptops & Computers',
        'gaming': 'Gaming',
        'headphone': 'Electronics & Gadgets',
        'earphone': 'Electronics & Gadgets',
        'watch': 'Fashion & Lifestyle',
        'shoe': 'Fashion & Lifestyle',
        'shirt': 'Fashion & Lifestyle',
        'dress': 'Fashion & Lifestyle'
    };

    for (const [keyword, category] of Object.entries(categories)) {
        if (query.includes(keyword)) {
            filters.category = category;
            break;
        }
    }

    // Price detection
    if (query.includes('cheap') || query.includes('affordable') || query.includes('budget')) {
        filters.price = 'Under 20,000';
    } else if (query.includes('under')) {
        const match = query.match(/under\s*(\d+)k?/);
        if (match) {
            const price = match[1].includes('k') ? match[1] : match[1] + '00';
            filters.price = `Under ${price.replace('k', ',000')}`;
        }
    } else if (query.includes('premium') || query.includes('expensive') || query.includes('high-end')) {
        filters.price = 'Above 50,000';
    }

    return filters;
}

function displayAutoFilters(filters) {
    const searchResults = document.querySelector('.container.my-4');
    if (!searchResults) return;

    const filterHTML = `
        <div class="auto-filters-card">
            <div class="card-header">
                <i class="bi bi-magic"></i>
                <h6>Smart Filters</h6>
            </div>
            <div class="auto-filters-list" id="autoFiltersList">
                ${Object.entries(filters).map(([key, value]) => `
                    <div class="auto-filter-item" data-filter="${key}">
                        <i class="bi bi-check-circle-fill check"></i>
                        <span>${key.charAt(0).toUpperCase() + key.slice(1)}: ${value}</span>
                        <i class="bi bi-x close" onclick="removeAutoFilter('${key}')"></i>
                    </div>
                `).join('')}
            </div>
        </div>
    `;

    searchResults.insertAdjacentHTML('afterbegin', filterHTML);
}

function removeAutoFilter(filterKey) {
    const filterItem = document.querySelector(`[data-filter="${filterKey}"]`);
    if (filterItem) {
        filterItem.style.animation = 'fade-out 0.3s ease-out';
        setTimeout(() => {
            filterItem.remove();

            // If no filters left, remove the entire card
            const filtersList = document.getElementById('autoFiltersList');
            if (filtersList && filtersList.children.length === 0) {
                document.querySelector('.auto-filters-card').remove();
            }
        }, 300);
    }
}

// ============================================
// Confidence Score Display
// ============================================

function displayConfidenceScores() {
    // This will be called from the view with product data
    const productCards = document.querySelectorAll('[data-confidence]');

    productCards.forEach(card => {
        const confidence = parseInt(card.dataset.confidence);
        const confidenceContainer = card.querySelector('.confidence-container');

        if (confidenceContainer) {
            const level = confidence >= 80 ? 'high' : confidence >= 60 ? 'medium' : 'low';
            const levelText = confidence >= 80 ? 'High match' : confidence >= 60 ? 'Good match' : 'Fair match';

            confidenceContainer.innerHTML = `
                <div class="ai-confidence-bar">
                    <div class="ai-confidence-fill ${level}" style="width: ${confidence}%"></div>
                </div>
                <div class="ai-confidence-text ${level}">
                    <i class="bi bi-lightning-charge-fill"></i>
                    ${confidence}% - ${levelText}
                </div>
            `;
        }
    });
}

// ============================================
// Smart Tags Display
// ============================================

function displaySmartTags(productId, tags) {
    const tagContainer = document.querySelector(`[data-product-id="${productId}"] .smart-tags-container`);
    if (!tagContainer || !tags || tags.length === 0) return;

    const tagHTML = tags.map(tag => {
        const tagClass = tag.toLowerCase().replace(/\s+/g, '-');
        return `<span class="smart-badge ${tagClass}">
            <i class="bi bi-star-fill"></i>
            ${tag}
        </span>`;
    }).join('');

    tagContainer.innerHTML = tagHTML;
}

// ============================================
// Export functions for use in views
// ============================================

window.toggleAIAssistant = toggleAIAssistant;
window.removeAutoFilter = removeAutoFilter;
window.displayConfidenceScores = displayConfidenceScores;
window.displaySmartTags = displaySmartTags;
window.addAIMessage = addAIMessage;

// ============================================
// Professional Search History (Amazon/Daraz Style)
// ============================================

function initSearchHistory() {
    const searchInput = document.getElementById('searchInput');
    const searchDropdown = document.getElementById('searchDropdown');
    const searchForm = searchInput?.closest('form');

    if (!searchInput || !searchDropdown) return;

    // Show history on focus
    searchInput.addEventListener('focus', function () {
        renderSearchHistory();
    });

    // Hide on click outside
    document.addEventListener('click', function (e) {
        if (!searchInput.contains(e.target) && !searchDropdown.contains(e.target)) {
            searchDropdown.style.display = 'none';
        }
    });

    // Save search on submit
    if (searchForm) {
        searchForm.addEventListener('submit', function () {
            const term = searchInput.value.trim();
            if (term) {
                saveSearchHistory(term);
            }
        });
    }
}

function getSearchHistory() {
    return JSON.parse(localStorage.getItem('sparkle_search_history') || '[]');
}

function saveSearchHistory(term) {
    let history = getSearchHistory();
    // Remove if exists (to move to top)
    history = history.filter(item => item.toLowerCase() !== term.toLowerCase());
    // Add to top
    history.unshift(term);
    // Keep max 5
    history = history.slice(0, 5);
    localStorage.setItem('sparkle_search_history', JSON.stringify(history));
}

function removeSearchHistoryItem(term) {
    let history = getSearchHistory();
    history = history.filter(item => item !== term);
    localStorage.setItem('sparkle_search_history', JSON.stringify(history));
    renderSearchHistory(); // Re-render
}

function clearAllSearchHistory() {
    localStorage.removeItem('sparkle_search_history');
    renderSearchHistory(); // Re-render
}

function renderSearchHistory() {
    const history = getSearchHistory();
    const searchDropdown = document.getElementById('searchDropdown');
    const searchInput = document.getElementById('searchInput');

    if (history.length === 0) {
        searchDropdown.style.display = 'none';
        return;
    }

    let html = `
        <div class="search-history-header">
            <span><i class="bi bi-clock-history"></i> Recent Searches</span>
            <button class="clear-btn" onclick="clearAllSearchHistory()">Clear All</button>
        </div>
        <div class="search-history-chips">
    `;

    // Show up to 6 items in a modern chip layout
    history.slice(0, 6).forEach(term => {
        const escapedTerm = term.replace(/'/g, "\\'");
        html += `
            <div class="search-history-chip" onclick="selectSearchTerm('${escapedTerm}')">
                <span>${term}</span>
                <i class="bi bi-x" onclick="event.stopPropagation(); removeSearchHistoryItem('${escapedTerm}')" title="Remove"></i>
            </div>
        `;
    });

    html += `</div>`;

    searchDropdown.innerHTML = html;
    searchDropdown.style.display = 'block';
}

function selectSearchTerm(term) {
    const searchInput = document.getElementById('searchInput');
    const searchForm = searchInput?.closest('form');
    if (searchInput) {
        searchInput.value = term;
        if (searchForm) searchForm.submit();
    }
}

// Export for global access
window.removeSearchHistoryItem = removeSearchHistoryItem;
window.clearAllSearchHistory = clearAllSearchHistory;
window.selectSearchTerm = selectSearchTerm;
