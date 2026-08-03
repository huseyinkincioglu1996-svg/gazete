using System.ComponentModel.DataAnnotations;

namespace GazeteDagitim.Web.Models.Enums;

public enum BusinessDay
{
    [Display(Name = "Pazartesi")]
    Monday = 0,

    [Display(Name = "Salı")]
    Tuesday = 1,

    [Display(Name = "Çarşamba")]
    Wednesday = 2,

    [Display(Name = "Perşembe")]
    Thursday = 3,

    [Display(Name = "Cuma")]
    Friday = 4,

    [Display(Name = "Cumartesi")]
    Saturday = 5,

    [Display(Name = "Pazar")]
    Sunday = 6
}

public enum NewspaperDay
{
    [Display(Name = "Pazartesi")]
    Monday = 0,

    [Display(Name = "Salı")]
    Tuesday = 1,

    [Display(Name = "Çarşamba")]
    Wednesday = 2,

    [Display(Name = "Perşembe")]
    Thursday = 3,

    [Display(Name = "Cuma")]
    Friday = 4,

    [Display(Name = "Cumartesi")]
    Saturday = 5,

    [Display(Name = "Pazar")]
    Sunday = 6,

    [Display(Name = "Pazar Pazartesi")]
    SundayMonday = 7
}

public enum DistributorZone
{
    [Display(Name = "Bölge 1")]
    Region1 = 1,

    [Display(Name = "Bölge 2")]
    Region2 = 2
}

public enum PaymentType
{
    [Display(Name = "Günlük")]
    Daily = 0,

    [Display(Name = "Haftalık")]
    Weekly = 1,

    [Display(Name = "Aylık")]
    Monthly = 2
}

public enum PaymentPeriodFrequency
{
    [Display(Name = "Aylık / dönemsel")]
    Monthly = 0,

    [Display(Name = "Günlük")]
    Daily = 1
}

public enum DeliveryStatus
{
    [Display(Name = "Beklemede")]
    Pending = 0,

    [Display(Name = "Tamamlandı")]
    Completed = 1,

    [Display(Name = "İptal")]
    Cancelled = 2
}

public enum PaymentStatus
{
    [Display(Name = "Beklemede")]
    Pending = 0,

    [Display(Name = "Ödendi")]
    Paid = 1
}

public enum CashHandoverStatus
{
    [Display(Name = "Taslak")]
    Draft = 0,

    [Display(Name = "Teslim Edildi")]
    Delivered = 1
}

public enum SubscriberPaymentMethod
{
    [Display(Name = "Nakit")]
    Cash = 0,

    [Display(Name = "Kart")]
    Card = 1,

    [Display(Name = "Havale/EFT")]
    Transfer = 2
}
