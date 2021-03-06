using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using CmsData;
using CmsData.Finance;
using CmsData.Registration;
using CmsWeb.Code;
using UtilityExtensions;

namespace CmsWeb.Models
{
    public class PaymentForm
    {
        public decimal? AmtToPay { get; set; }
        public decimal? Donate { get; set; }
        public decimal Amtdue { get; set; }
        public string Coupon { get; set; }
        public string CreditCard { get; set; }
        public string Expires { get; set; }
        public string CVV { get; set; }
        public string Routing { get; set; }
        public string Account { get; set; }

        /// <summary>
        /// "B" for e-check and "C" for credit card, see PaymentType
        /// </summary>
        public string Type { get; set; }
        public bool AskDonation { get; set; }
        public bool AllowCoupon { get; set; }
        public string Terms { get; set; }
        public int DatumId { get; set; }
        public Guid FormId { get; set; }
        public string URL { get; set; }
        public string FullName()
        {
            string n;
            if (MiddleInitial.HasValue())
                n = "{0} {1} {2}".Fmt(First, MiddleInitial, Last);
            else
                n = "{0} {1}".Fmt(First, Last);
            if (Suffix.HasValue())
                n = n + " " + Suffix;
            return n;
        }
        private int? timeOut;
        public int TimeOut
        {
            get
            {
                if (!timeOut.HasValue)
                    timeOut = Util.IsDebug() ? 16000000 : DbUtil.Db.Setting("RegTimeout", "180000").ToInt();
                return timeOut.Value;
            }
        }

        public string First { get; set; }
        public string MiddleInitial { get; set; }
        public string Last { get; set; }
        public string Suffix { get; set; }
        public string Description { get; set; }
        public bool PayBalance { get; set; }
        public int? OrgId { get; set; }
        public int? OriginalId { get; set; }
        public bool testing { get; set; }
        public bool? FinanceOnly { get; set; }
        public bool? IsLoggedIn { get; set; }
        public bool? CanSave { get; set; }
        public bool SavePayInfo { get; set; }
        public bool? AllowSaveProgress { get; set; }
        public bool? IsGiving { get; set; }
        public bool NoCreditCardsAllowed { get; set; }
        private bool? _noEChecksAllowed;
        public bool NoEChecksAllowed
        {
            get
            {
                if (!_noEChecksAllowed.HasValue)
                    _noEChecksAllowed = DbUtil.Db.Setting("NoEChecksAllowed", "false") == "true";
                return _noEChecksAllowed.Value;
            }
        }

        public string Email { get; set; }
        public string Address { get; set; }
        public string Address2 { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string Country { get; set; }

        public IEnumerable<SelectListItem> Countries
        {
            get
            {
                var list = CodeValueModel.ConvertToSelect(CodeValueModel.GetCountryList().Where(c => c.Code != "NA"), null);
                list.Insert(0, new SelectListItem {Text = "(not specified)", Value = ""});
                return list;
            }
        }

        public string Phone { get; set; }
        public int? TranId { get; set; }

        public Transaction CreateTransaction(CMSDataContext Db, decimal? amount = null, OnlineRegModel m = null)
        {
            if (!amount.HasValue)
                amount = AmtToPay;
            decimal? amtdue = null;
            if (Amtdue > 0)
                amtdue = Amtdue - (amount ?? 0);
            var ti = new Transaction
                     {
                         First = First,
                         MiddleInitial = MiddleInitial.Truncate(1) ?? "",
                         Last = Last,
                         Suffix = Suffix,
                         Donate = Donate,
                         Regfees = AmtToPay,
                         Amt = amount,
                         Amtdue = amtdue,
                         Emails = Email,
                         Testing = testing,
                         Description = Description,
                         OrgId = OrgId,
                         Url = URL,
                         TransactionGateway = OnlineRegModel.GetTransactionGateway(),
                         Address = Address.Truncate(50),
                         Address2 = Address2.Truncate(50),
                         City = City,
                         State = State,
                         Country = Country,
                         Zip = Zip,
                         DatumId = DatumId,
                         Phone = Phone.Truncate(20),
                         OriginalId = OriginalId,
                         Financeonly = FinanceOnly,
                         TransactionDate = DateTime.Now,
                         PaymentType = Type,
                         LastFourCC = Type == PaymentType.CreditCard ? CreditCard.Last(4) : null,
                         LastFourACH = Type == PaymentType.Ach ? Account.Last(4) : null
                     };

            Db.Transactions.InsertOnSubmit(ti);
            Db.SubmitChanges();
            if (OriginalId == null) // first transaction
                ti.OriginalId = ti.Id;
            return ti;
        }
        public static Decimal AmountDueTrans(CMSDataContext db, Transaction ti)
        {
            var org = db.LoadOrganizationById(ti.OrgId);
            var tt = (from t in db.ViewTransactionSummaries
                          where t.RegId == ti.OriginalId
                          select t).FirstOrDefault();
            if (tt == null)
                return 0;
            if (org.IsMissionTrip ?? false)
                return (tt.IndAmt ?? 0) - (db.TotalPaid(tt.OrganizationId, tt.PeopleId) ?? 0);
            return tt.TotDue ?? 0;
        }

        public static PaymentForm CreatePaymentFormForBalanceDue(Transaction ti, decimal amtdue, string email)
        {
            PaymentInfo pi = null;
            if (ti.Person != null)
                pi = ti.Person.PaymentInfos.FirstOrDefault();
            if (pi == null)
                pi = new PaymentInfo();

            var pf = new PaymentForm
                     {
                         URL = ti.Url,
                         PayBalance = true,
                         AmtToPay = amtdue,
                         Amtdue = 0,
                         AllowCoupon = true,
                         AskDonation = false,
                         Description = ti.Description,
                         OrgId = ti.OrgId,
                         OriginalId = ti.OriginalId,
                         Email = Util.FirstAddress(ti.Emails ?? email).Address,
                         FormId = Guid.NewGuid(),

                         First = ti.First,
                         MiddleInitial = ti.MiddleInitial.Truncate(1) ?? "",
                         Last = ti.Last,
                         Suffix = ti.Suffix,

                         Phone = ti.Phone,
                         Address = ti.Address,
                         Address2 = ti.Address2,
                         City = ti.City,
                         State = ti.State,
                         Country = ti.Country,
                         Zip = ti.Zip,
                         testing = ti.Testing ?? false,
                         TranId = ti.Id,
#if DEBUG2
						 CreditCard = "4111111111111111",
						 CVV = "123",
						 Expires = "1015",
						 Routing = "056008849",
						 Account = "12345678901234"
#else
                         CreditCard = pi.MaskedCard,
                         Expires = pi.Expires,
                         Account = pi.MaskedAccount,
                         Routing = pi.Routing,
                         SavePayInfo =
                            (pi.MaskedAccount != null && pi.MaskedAccount.StartsWith("X"))
                            || (pi.MaskedCard != null && pi.MaskedCard.StartsWith("X")),
#endif
                     };

            ClearMaskedNumbers(pf, pi);

            pf.Type = pf.NoEChecksAllowed ? PaymentType.CreditCard : "";
            var org = DbUtil.Db.LoadOrganizationById(ti.OrgId);
            var setting = new Settings(org.RegSetting, DbUtil.Db, org.OrganizationId);
            return pf;
        }
        public static PaymentForm CreatePaymentForm(OnlineRegModel m)
        {
            var r = m.GetTransactionInfo();
            if (r == null)
                return null;

            var pf = new PaymentForm
            {
                FormId = Guid.NewGuid(),
                AmtToPay = m.PayAmount() + (m.donation ?? 0),
                AskDonation = m.AskDonation(),
                AllowCoupon = !m.OnlineGiving(),
                PayBalance = false,
                Amtdue = m.TotalAmount() + (m.donation ?? 0),
                Donate = m.donation,
                Description = m.DescriptionForPayment,
                Email = r.Email,
                First = r.First,
                MiddleInitial = r.Middle,
                Last = r.Last,
                Suffix = r.Suffix,
                IsLoggedIn = m.UserPeopleId.HasValue,
                OrgId = m.List[0].orgid,
                URL = m.URL,
                testing = m.testing ?? false,
                Terms = m.Terms,
                Address = r.Address,
                Address2 = r.Address2,
                City = r.City,
                State = r.State,
                Country = r.Country,
                Zip = r.Zip,
                Phone = r.Phone,
#if DEBUG2
				 CreditCard = "4111111111111111",
				 CVV = "123",
				 Expires = "1015",
				 Routing = "056008849",
				 Account = "12345678901234"
#else
                CreditCard = r.payinfo.MaskedCard,
                Account = r.payinfo.MaskedAccount,
                Routing = r.payinfo.Routing,
                Expires = r.payinfo.Expires,
                SavePayInfo =
                   (r.payinfo.MaskedAccount != null && r.payinfo.MaskedAccount.StartsWith("X"))
                   || (r.payinfo.MaskedCard != null && r.payinfo.MaskedCard.StartsWith("X")),
                Type = r.payinfo.PreferredPaymentType,
#endif
            };

            ClearMaskedNumbers(pf, r.payinfo);

            pf.AllowSaveProgress = m.AllowSaveProgress();
            pf.NoCreditCardsAllowed = m.NoCreditCardsAllowed();
            if (m.OnlineGiving())
            {
                pf.NoCreditCardsAllowed = DbUtil.Db.Setting("NoCreditCardGiving", "false").ToBool();
                pf.IsGiving = true;
                pf.FinanceOnly = true;
                pf.Type = r.payinfo.PreferredGivingType;
            }
            else if (m.ManageGiving() || m.OnlinePledge())
            {
                pf.FinanceOnly = true;
            }
            if (pf.NoCreditCardsAllowed)
                pf.Type = PaymentType.Ach; // bank account only
            else if (pf.NoEChecksAllowed)
                pf.Type = PaymentType.CreditCard; // credit card only
            pf.Type = pf.NoEChecksAllowed ? PaymentType.CreditCard : pf.Type;
            pf.DatumId = m.DatumId ?? 0;
            return pf;
        }

        private static void ClearMaskedNumbers(PaymentForm pf, PaymentInfo pi)
        {
            var gateway = DbUtil.Db.Setting("TransactionGateway", "");

            var clearBankDetails = false;
            var clearCreditCardDetails = false;

            switch (gateway.ToLower())
            {
                case "sage":
                    clearBankDetails = !pi.SageBankGuid.HasValue;
                    clearCreditCardDetails = !pi.SageCardGuid.HasValue;
                    break;
                case "transnational":
                    clearBankDetails = !pi.TbnBankVaultId.HasValue;
                    clearCreditCardDetails = !pi.TbnCardVaultId.HasValue;
                    break;
                case "authorizenet":
                    clearBankDetails = !pi.AuNetCustPayBankId.HasValue;
                    clearCreditCardDetails = !pi.AuNetCustPayId.HasValue;
                    break;
            }

            if (clearBankDetails)
            {
                pf.Account = string.Empty;
                pf.Routing = string.Empty;
            }

            if (clearCreditCardDetails)
            {
                pf.CreditCard = string.Empty;
                pf.CVV = string.Empty;
                pf.Expires = string.Empty;
            }
        }

        public static Transaction CreateTransaction(CMSDataContext Db, Transaction t, decimal? amount)
        {
            var amtdue = t.Amtdue != null ? t.Amtdue - (amount ?? 0) : null;
            var ti = new Transaction
                     {
                         Name = t.Name,
                         First = t.First,
                         MiddleInitial = t.MiddleInitial,
                         Last = t.Last,
                         Suffix = t.Suffix,
                         Donate = t.Donate,
                         Amtdue = amtdue,
                         Amt = amount,
                         Emails = Util.FirstAddress(t.Emails).Address,
                         Testing = t.Testing,
                         Description = t.Description,
                         OrgId = t.OrgId,
                         Url = t.Url,
                         Address = t.Address,
                         TransactionGateway = OnlineRegModel.GetTransactionGateway(),
                         City = t.City,
                         State = t.State,
                         Zip = t.Zip,
                         DatumId = t.DatumId,
                         Phone = t.Phone,
                         OriginalId = t.OriginalId ?? t.Id,
                         Financeonly = t.Financeonly,
                         TransactionDate = DateTime.Now,
                         PaymentType = t.PaymentType,
                         LastFourCC = t.LastFourCC,
                         LastFourACH = t.LastFourACH
                     };
            Db.Transactions.InsertOnSubmit(ti);
            Db.SubmitChanges();
            return ti;
        }

        public object Autocomplete(bool small = false)
        {
            if (small)
                return new
                {
                    AUTOCOMPLETE = AutocompleteOnOff,
                    @class = "short"
                };
            return new
            {
                AUTOCOMPLETE = AutocompleteOnOff,
            };
        }

        public string AutocompleteOnOff
        {
            get
            {
#if DEBUG
                return "on";
#else
    			return "off";
#endif
            }
        }
    }
}

