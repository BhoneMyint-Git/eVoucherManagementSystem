using System.ComponentModel.DataAnnotations;

namespace eVoucherManagementSystem.Model
{
    public class PromoCodeGetModel
    {
        [Required]
        public string Phone { get; set; }
    }
}
