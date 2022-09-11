using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using eVoucherManagementSystem.Data;
using eVoucherManagementSystem.Model;
using System.Drawing;
using IronBarCode;
using Microsoft.Extensions.Hosting;
using RestSharp;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Authorization;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using RestSharp.Authenticators;

namespace eVoucherManagementSystem.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EvouchersController : ControllerBase
    {
        private readonly eVoucherContext _context;
        private static IConfiguration _configuration;

        public EvouchersController(eVoucherContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [Authorize]
        [HttpPut("UpdateEVoucher")]
        public async Task<IActionResult> UpdateEVoucher(eVoucherUpdateModel eVoucher)
        {

            TblEvoucher? voucher = await _context.TblEvouchers.FindAsync(eVoucher.Id);
            try
            {
                if (voucher != null)
                {
                    voucher.Title = eVoucher.Title;
                    voucher.Price = eVoucher.Price;
                    voucher.Amount = eVoucher.Amount;
                    voucher.PaymentType = eVoucher.PaymentType;
                    voucher.PaymentDiscount = eVoucher.PaymentDiscount;
                    _context.Entry(voucher).State = EntityState.Modified;
                }
                else
                {
                    return NotFound();
                }
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                return Problem("Error occured " + ex.Message);
            }
            return CreatedAtAction("UpdateEVoucher", new { }, Convert.ToBase64String(voucher.Qrimage));
        }

        [Authorize]
        [HttpPost("CreateEVouchers")]
        public async Task<ActionResult<TblEvoucher>> CreateEVouchers(eVoucherModel eVoucher)
        {

            TblEvoucher tblEvoucher = new TblEvoucher();
            List<string> generatedQRs = new List<string>();
            //
            string voucherCodes = "";
            Byte[] qr;
            for (int i = 0; i < eVoucher.Quantity; i++)
            {
                try
                {
                    voucherCodes = GenerateCodes();
                    qr = GenerateQR(voucherCodes);
                    tblEvoucher = new TblEvoucher()
                    {
                        Id = Guid.NewGuid().ToString(),
                        Name = eVoucher.UserName,
                        Phone = eVoucher.Phone,
                        Title = eVoucher.Title,
                        Description = eVoucher.Description,
                        ExpiryDate = eVoucher.ExpirtyDate,
                        Amount = eVoucher.Amount,
                        PaymentDiscount = eVoucher.PaymentDiscount,
                        PaymentType = eVoucher.PaymentType,
                        IsGift = eVoucher.IsGift,
                        VoucherCodes = voucherCodes,
                        Active = true,
                        CreatedDate = DateTime.Now,
                        Qrimage = qr,
                        IsUsed = false,
                        Price = eVoucher.Price

                    };
                    _context.TblEvouchers.Add(tblEvoucher);
                    await _context.SaveChangesAsync();
                    //add to list of QR to return created QR images
                    generatedQRs.Add(Convert.ToBase64String(qr));
                }
                catch (Exception ex)
                {
                    return Problem("Error occured " + ex.Message);
                }

            }
            return CreatedAtAction("CreateEVouchers", new { phone = tblEvoucher.Phone }, generatedQRs);
        }

        [Authorize]
        [HttpDelete("DeleteEVoucher")]
        public async Task<IActionResult> DeleteEVoucher(eVoucherUpdateModel eVoucher)
        {
            try
            {

                var voucher = await _context.TblEvouchers.FindAsync(eVoucher.Id);
                if (voucher == null)
                {
                    return NotFound();
                }
                voucher.Active = false;
                _context.Entry(voucher).State = EntityState.Modified;
                await _context.SaveChangesAsync();
                return CreatedAtAction("DeleteEVoucher", new { Id = voucher.Id }, "Deleted Voucher");
            }
            catch (Exception ex)
            {
                return Problem("Error Deleting Voucher!!!");
            }
        }

        [AllowAnonymous]
        [HttpPost("GetToken")]
        public async Task<IActionResult> GetToken(string userName, string password)
        {
            var tokenUser = (from o in _context.TblTokenusers
                             where
                             o.User == userName && o.Password == password
                             select o).FirstOrDefault();
            if (tokenUser != null)
            {
                var issuer = _configuration.GetSection("Jwt").GetValue<string>("Issuer");
                var audience = _configuration.GetSection("Jwt").GetValue<string>("Audience");
                var key = Encoding.ASCII.GetBytes
                (_configuration.GetSection("Jwt").GetValue<string>("Key"));
                var tokenDescriptor = new SecurityTokenDescriptor
                {

                    Expires = DateTime.UtcNow.AddDays(_configuration.GetSection("Jwt").GetValue<int>("ExpireDay")),
                    Issuer = issuer,
                    Audience = audience,
                    SigningCredentials = new SigningCredentials
                    (new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha512Signature)
                };
                var tokenHandler = new JwtSecurityTokenHandler();
                var token = tokenHandler.CreateToken(tokenDescriptor);
                var jwtToken = tokenHandler.WriteToken(token);
                var stringToken = tokenHandler.WriteToken(token);
                return CreatedAtAction("GetToken", new { }, stringToken);
            }
            return NoContent();
        }

        [Authorize]
        [HttpPut("UpdatePurchaseHistory")]
        public async Task<IActionResult> UpdatePurchaseHistroy()
        {
            //get list of Phones used to purchased evouchers
            var purchaseList = (from o in _context.TblPurchasehistories
                                where o.PromoCodes == null && o.Qrimage == null && o.IsUsed == null
                                group o by o.Phone into x
                                select x.FirstOrDefault()).ToList();
            if (purchaseList == null)
            {
                return NotFound();
            }
            //prepare api to get promocode related to phone number
            PromoCodeGetModel getPromo = new PromoCodeGetModel();

            foreach (var purchase in purchaseList)
            {
                var client = new RestClient($"https://localhost:7207/api/Promocodes/GetPromoCode");
                var request = new RestRequest("https://localhost:7207/api/Promocodes/GetPromoCode", Method.Get);
                request.AddHeader("Content-Type", "application/json");
                string token = GetJwTTokenFromPromoManagementSystem();
                client.Authenticator = new JwtAuthenticator(token.Replace("\"", ""));
                getPromo.Phone = purchase.Phone;
                request.AddBody(JsonConvert.SerializeObject(getPromo));
                RestResponse response = await client.ExecuteAsync(request);
                var output = response.Content;
                //add promo code to purchase history
                if (output != null && output != "[]")
                {
                    var data = JsonConvert.DeserializeObject<List<AvaliablePromo>>(output);
                    var purchasedHistroy = (from o in _context.TblPurchasehistories
                                            where o.PromoCodes == null && o.Qrimage == null && o.IsUsed == null && o.Phone == purchase.Phone
                                            select o).ToList();
                    int i = 0;
                    foreach (var promo in data)
                    {

                        if (purchasedHistroy != null)
                        {
                            purchasedHistroy[i].PromoCodes = promo.Code;
                            purchasedHistroy[i].Qrimage = promo.QRImage;
                            purchasedHistroy[i].IsUsed = false;
                            _context.Entry(purchasedHistroy[i]).State = EntityState.Modified;
                        }

                        i++;
                    }
                    await _context.SaveChangesAsync();
                }

            }
            return CreatedAtAction("UpdatePurchaseHistroy", new { }, "");
        }
        static Random random = new Random();
        public static string GenerateCodes()
        {

            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string nums = "0987654321";
            string alpha = new string(Enumerable.Repeat(chars, 5)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            string digit = new string(Enumerable.Repeat(nums, 6)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            return digit + alpha;

        }
        public static byte[] GenerateQR(string text)
        {
            try
            {
                GeneratedBarcode barcode = QRCodeWriter.CreateQrCode(text, 200);
                barcode.AddBarcodeValueTextBelowBarcode();
                barcode.SetMargins(10);
                barcode.ChangeBarCodeColor(Color.BlueViolet);
                var bitMap = barcode.ToBitmap();
                System.IO.MemoryStream stream = new System.IO.MemoryStream();
                bitMap.Save(stream, System.Drawing.Imaging.ImageFormat.Bmp);
                byte[] imageBytes = stream.ToArray();
                stream.Dispose();
                return imageBytes;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static string GetJwTTokenFromPromoManagementSystem()
        {
            var client = new RestClient($"https://localhost:7207/api/Promocodes/GetToken");
            var request = new RestRequest("https://localhost:7207/api/Promocodes/GetToken", Method.Post);
            request.AddHeader("Content-Type", "application/json");
            request.AddQueryParameter("userName", _configuration.GetSection("JwT").GetValue<string>("User"));
            request.AddQueryParameter("password", _configuration.GetSection("JwT").GetValue<string>("Password"));
            RestResponse response = client.Execute(request);
            var output = response.Content;
            return output;
        }
    }
}
