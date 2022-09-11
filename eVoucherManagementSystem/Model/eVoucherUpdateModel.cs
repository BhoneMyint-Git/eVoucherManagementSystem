using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;

namespace eVoucherManagementSystem.Model
{
    public class eVoucherUpdateModel
    {
        [Required]
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public double Amount { get; set; }
        public double Price { get; set; }
        public string PaymentType { get; set; }
        public double PaymentDiscount { get; set; }
    }
}
