// Định dạng số tiền tự động khi nhập
function formatCurrency(input) {
    // Lấy giá trị và loại bỏ các ký tự không phải số
    let value = input.value.replace(/[^\d]/g, '');
    
    // Nếu rỗng thì trả về
    if (value === '') {
        input.setAttribute('data-raw-value', '0');
        return;
    }
    
    // Chuyển thành số (chỉ xử lý số nguyên)
    let number = parseInt(value);
    if (isNaN(number)) {
        input.setAttribute('data-raw-value', '0');
        return;
    }
    
    // Định dạng với dấu chấm ngăn cách hàng nghìn
    let formatted = number.toString().replace(/\B(?=(\d{3})+(?!\d))/g, '.');
    
    // Cập nhật giá trị hiển thị
    input.value = formatted;
    
    // Lưu giá trị số nguyên để submit
    input.setAttribute('data-raw-value', number.toString());
}

// Lấy giá trị số nguyên để submit
function getCurrencyValue(input) {
    let rawValue = input.getAttribute('data-raw-value');
    if (rawValue) {
        return rawValue;
    }
    // Fallback: lấy từ value hiện tại và chuyển thành số
    let cleanValue = input.value.replace(/[^\d]/g, '');
    return cleanValue || '0';
}

// Khởi tạo định dạng cho tất cả input tiền tệ
function initCurrencyInputs() {
    document.addEventListener('DOMContentLoaded', function() {
        // Tìm tất cả input có type="number" và step="0"
        const currencyInputs = document.querySelectorAll('input[type="number"][step="0"]');
        
        currencyInputs.forEach(input => {
            // Thêm class để nhận diện
            input.classList.add('currency-input');
            
            // Xóa type="number" để không hiển thị spinner và validation
            input.type = 'text';
            input.removeAttribute('step');
            
            // Format giá trị ban đầu nếu có
            if (input.value) {
                // Xử lý giá trị từ database (loại bỏ dấu chấm)
                let cleanValue = input.value.replace(/[^\d]/g, '');
                if (cleanValue) {
                    input.value = cleanValue;
                    formatCurrency(input);
                }
            }
            
            // Thêm events
            input.addEventListener('input', function() {
                formatCurrency(this);
            });
            
            input.addEventListener('blur', function() {
                formatCurrency(this);
            });
            
            input.addEventListener('focus', function() {
                let rawValue = this.getAttribute('data-raw-value');
                if (rawValue && rawValue !== '0') {
                    this.value = rawValue;
                } else {
                    // Loại bỏ định dạng, chỉ giữ số
                    this.value = this.value.replace(/[^\d]/g, '');
                }
            });
        });
        
        // Override form submission để gửi giá trị đúng
        const forms = document.querySelectorAll('form');
        forms.forEach(form => {
            form.addEventListener('submit', function(e) {
                const currencyInputs = form.querySelectorAll('.currency-input');
                currencyInputs.forEach(input => {
                    const rawValue = getCurrencyValue(input);
                    if (rawValue) {
                        // Tạo hidden input để gửi giá trị đúng
                        const hiddenInput = document.createElement('input');
                        hiddenInput.type = 'hidden';
                        hiddenInput.name = input.name;
                        hiddenInput.value = rawValue;
                        form.appendChild(hiddenInput);
                        
                        // Xóa name của input gốc để không gửi giá trị formatted
                        input.removeAttribute('name');
                    }
                });
            });
        });
    });
}

// Hàm khởi tạo lại cho trường hợp trang load động
function reinitCurrencyInputs() {
    // Tìm tất cả input chưa được khởi tạo
    const uninitInputs = document.querySelectorAll('input[type="number"][step="0"]:not(.currency-input)');
    
    uninitInputs.forEach(input => {
        // Thêm class để nhận diện
        input.classList.add('currency-input');
        
        // Xóa type="number" để không hiển thị spinner và validation
        input.type = 'text';
        input.removeAttribute('step');
        
        // Format giá trị ban đầu nếu có
        if (input.value) {
            let cleanValue = input.value.replace(/[^\d]/g, '');
            if (cleanValue) {
                input.value = cleanValue;
                formatCurrency(input);
            }
        }
        
        // Thêm events
        input.addEventListener('input', function() {
            formatCurrency(this);
        });
        
        input.addEventListener('blur', function() {
            formatCurrency(this);
        });
        
        input.addEventListener('focus', function() {
            let rawValue = this.getAttribute('data-raw-value');
            if (rawValue && rawValue !== '0') {
                this.value = rawValue;
            } else {
                this.value = this.value.replace(/[^\d]/g, '');
            }
        });
    });
}

// Gọi hàm khởi tạo
initCurrencyInputs();

// Thêm timeout để đảm bảo chạy sau khi trang load hoàn toàn
setTimeout(reinitCurrencyInputs, 100);

// Thêm interval để kiểm tra và khởi tạo lại các input mới
setInterval(reinitCurrencyInputs, 1000);
