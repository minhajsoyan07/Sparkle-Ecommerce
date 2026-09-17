/**
 * Categories Management JavaScript
 * Handles interactive features for category management
 */

(function () {
    'use strict';

    // Initialize on page load
    document.addEventListener('DOMContentLoaded', function () {
        initializeTooltips();
        initializeImagePreviews();
        initializeConfirmDialogs();
    });

    /**
     * Initialize Bootstrap tooltips
     */
    function initializeTooltips() {
        const tooltipTriggerList = [].slice.call(document.querySelectorAll('[title]'));
        tooltipTriggerList.map(function (tooltipTriggerEl) {
            return new bootstrap.Tooltip(tooltipTriggerEl);
        });
    }

    /**
     * Initialize image preview functionality
     */
    function initializeImagePreviews() {
        const categoryImages = document.querySelectorAll('.category-image');

        categoryImages.forEach(img => {
            img.addEventListener('click', function (e) {
                e.preventDefault();
                showImageModal(this.src, this.alt);
            });

            // Add cursor pointer
            img.style.cursor = 'pointer';
        });
    }

    /**
     * Show image in modal
     */
    function showImageModal(src, alt) {
        // Create modal if it doesn't exist
        let modal = document.getElementById('categoryImageModal');
        if (!modal) {
            modal = createImageModal();
            document.body.appendChild(modal);
        }

        const modalImg = modal.querySelector('.modal-body img');
        const modalTitle = modal.querySelector('.modal-title');

        modalImg.src = src;
        modalTitle.textContent = alt;

        const bsModal = new bootstrap.Modal(modal);
        bsModal.show();
    }

    /**
     * Create image preview modal
     */
    function createImageModal() {
        const modal = document.createElement('div');
        modal.className = 'modal fade';
        modal.id = 'categoryImageModal';
        modal.innerHTML = `
            <div class="modal-dialog modal-dialog-centered modal-lg">
                <div class="modal-content">
                    <div class="modal-header">
                        <h5 class="modal-title">Category Image</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body text-center">
                        <img src="" alt="" class="img-fluid" style="max-height: 70vh;">
                    </div>
                </div>
            </div>
        `;
        return modal;
    }

    /**
     * Initialize confirm dialogs
     */
    function initializeConfirmDialogs() {
        const deleteForms = document.querySelectorAll('form[onsubmit*="confirm"]');

        deleteForms.forEach(form => {
            form.addEventListener('submit', function (e) {
                const categoryName = this.getAttribute('onsubmit').match(/'([^']+)'/)?.[1] || 'this category';

                if (!confirm(`Are you sure you want to delete ${categoryName}? This action cannot be undone.`)) {
                    e.preventDefault();
                    return false;
                }
            });
        });
    }

    /**
     * Show success toast message
     */
    window.showSuccessToast = function (message) {
        showToast(message, 'success');
    };

    /**
     * Show error toast message
     */
    window.showErrorToast = function (message) {
        showToast(message, 'danger');
    };

    /**
     * Show toast notification
     */
    function showToast(message, type) {
        const toastContainer = getToastContainer();

        const toast = document.createElement('div');
        toast.className = `toast align-items-center text-white bg-${type} border-0`;
        toast.setAttribute('role', 'alert');
        toast.innerHTML = `
            <div class="d-flex">
                <div class="toast-body">
                    ${message}
                </div>
                <button type="button" class="btn-close btn-close-white me-2 m-auto" data-bs-dismiss="toast"></button>
            </div>
        `;

        toastContainer.appendChild(toast);
        const bsToast = new bootstrap.Toast(toast, { delay: 3000 });
        bsToast.show();

        toast.addEventListener('hidden.bs.toast', function () {
            toast.remove();
        });
    }

    /**
     * Get or create toast container
     */
    function getToastContainer() {
        let container = document.getElementById('toastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toastContainer';
            container.className = 'toast-container position-fixed top-0 end-0 p-3';
            container.style.zIndex = '9999';
            document.body.appendChild(container);
        }
        return container;
    }

})();
