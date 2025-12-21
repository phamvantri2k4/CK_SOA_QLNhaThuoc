// Định dạng tiền tệ đơn giản và ổn định
function initSimpleCurrencyFormat() {
    document.addEventListener('DOMContentLoaded', function() {
        // Tìm tất cả input tiền tệ
        const inputs = document.querySelectorAll('input[type="number"][step="0"]');
        
        inputs.forEach(input => {
            // Đổi type thành text ngay lập tức để loại bỏ validation
            input.type = 'text';
            input.removeAttribute('step');
            
            // Format giá trị ban đầu nếu có
            if (input.value) {
                let value = input.value.replace(/[^\d]/g, '');
                if (value && !isNaN(value)) {
                    let number = parseInt(value);
                    if (number > 0) {
                        let formatted = number.toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.');
                        input.value = formatted;
                    }
                }
            }
            
            // Định dạng ngay khi đang nhập
            input.addEventListener('input', function() {
                let value = this.value.replace(/[^\d]/g, '');
                if (value && !isNaN(value)) {
                    let number = parseInt(value);
                    if (number > 0) {
                        let formatted = number.toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.');
                        this.value = formatted;
                    }
                }
            });
            
            // Định dạng khi blur (đảm bảo)
            input.addEventListener('blur', function() {
                let value = this.value.replace(/[^\d]/g, '');
                if (value && !isNaN(value)) {
                    let number = parseInt(value);
                    if (number > 0) {
                        let formatted = number.toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.');
                        this.value = formatted;
                    }
                }
            });
            
            // Khi focus, hiển thị số nguyên
            input.addEventListener('focus', function() {
                let value = this.value.replace(/[^\d]/g, '');
                if (value && !isNaN(value)) {
                    this.value = value;
                }
            });
        });
        
        // Override form submission để gửi giá trị đúng
        const forms = document.querySelectorAll('form');
        forms.forEach(form => {
            form.addEventListener('submit', function(e) {
                const currencyInputs = form.querySelectorAll('input[type="text"]');
                currencyInputs.forEach(input => {
                    // Kiểm tra xem input có phải là input tiền tệ không
                    if (input.value.includes('.')) {
                        let cleanValue = input.value.replace(/[^\d]/g, '');
                        if (cleanValue && !isNaN(cleanValue)) {
                            // Tạo hidden input để gửi giá trị đúng
                            const hiddenInput = document.createElement('input');
                            hiddenInput.type = 'hidden';
                            hiddenInput.name = input.name;
                            hiddenInput.value = cleanValue;
                            form.appendChild(hiddenInput);
                            
                            // Xóa name của input gốc để không gửi giá trị formatted
                            input.removeAttribute('name');
                        }
                    }
                });
            });
        });
    });
}

// Khởi tạo
initSimpleCurrencyFormat();
