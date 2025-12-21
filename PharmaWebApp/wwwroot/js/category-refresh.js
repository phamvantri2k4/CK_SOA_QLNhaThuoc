// Auto-refresh category dropdown in OrderCreate page
(function() {
    'use strict';
    
    const categoryFilter = document.getElementById('categoryFilter');
    if (!categoryFilter) return;
    
    // Create refresh button
    const refreshBtn = document.createElement('button');
    refreshBtn.className = 'btn btn-outline-secondary btn-sm ms-1';
    refreshBtn.type = 'button';
    refreshBtn.innerHTML = '<i class="bi bi-arrow-clockwise"></i>';
    refreshBtn.title = 'Làm mới danh mục';
    refreshBtn.style.marginLeft = '5px';
    
    // Insert button after select
    categoryFilter.parentNode.insertBefore(refreshBtn, categoryFilter.nextSibling);
    
    // Refresh function
    async function refreshCategories() {
        const currentValue = categoryFilter.value;
        refreshBtn.disabled = true;
        refreshBtn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';
        
        try {
            // Reload page to get fresh categories
            location.reload();
        } catch (error) {
            console.error('Error refreshing categories:', error);
            refreshBtn.disabled = false;
            refreshBtn.innerHTML = '<i class="bi bi-arrow-clockwise"></i>';
        }
    }
    
    // Add click handler
    refreshBtn.addEventListener('click', refreshCategories);
    
    // Also add "Add Category" button
    const addBtn = document.createElement('a');
    addBtn.href = '/Categories/Create';
    addBtn.target = '_blank';
    addBtn.className = 'btn btn-outline-success btn-sm ms-1';
    addBtn.innerHTML = '<i class="bi bi-plus-circle"></i>';
    addBtn.title = 'Thêm danh mục mới';
    addBtn.style.marginLeft = '5px';
    
    refreshBtn.parentNode.insertBefore(addBtn, refreshBtn.nextSibling);
    
    // Watch for window closing and reload
    addBtn.addEventListener('click', function(e) {
        e.preventDefault();
        const newWindow = window.open(this.href, '_blank', 'width=800,height=600');
        
        const checkInterval = setInterval(function() {
            if (newWindow && newWindow.closed) {
                clearInterval(checkInterval);
                refreshCategories();
            }
        }, 500);
    });
})();
