using System.ComponentModel.DataAnnotations;

namespace PharmaWebApp.Models
{
    /// <summary>
    /// ViewModel để hiển thị thông tin khách hàng
    /// </summary>
    public class CustomerViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel để tạo khách hàng mới
    /// </summary>
    public class CreateCustomerViewModel
    {
        [Required(ErrorMessage = "Tên khách hàng không được để trống")]
        [Display(Name = "Tên khách hàng")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [Display(Name = "Số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string Phone { get; set; } = string.Empty;
    }

    /// <summary>
    /// ViewModel để sửa thông tin khách hàng
    /// </summary>
    public class EditCustomerViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên khách hàng không được để trống")]
        [Display(Name = "Tên khách hàng")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [Display(Name = "Số điện thoại")]
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        public string Phone { get; set; } = string.Empty;
    }
}

