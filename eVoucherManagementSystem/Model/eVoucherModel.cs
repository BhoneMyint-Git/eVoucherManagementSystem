namespace eVoucherManagementSystem.Model
{
    public class eVoucherModel
    {
        public string Title { get; set; }
        public string UserName { get; set; } = "";

        public string Phone { get; set; } = "";
        public string Description { get; set; }
        public DateTime ExpirtyDate { get; set; } = DateTime.Now;
        public double Amount { get; set; }
        public string PaymentType { get; set; } = "";
        public double PaymentDiscount { get; set; } = 0.0;
        public int Quantity { get; set; }
        public bool IsGift { get; set; } = false;
        public double Price { get; set; }
       
    }
}
