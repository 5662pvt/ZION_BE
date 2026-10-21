using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using ZIONShop.Contracts.Orders;

namespace ZIONShop.Notifications.Application.Templates;

/// <summary>Plain string-building templates (no view engine in this module). Every dynamic value is HTML-encoded before interpolation.</summary>
public static class OrderEmailTemplates
{
    private static readonly CultureInfo ViVn = CultureInfo.GetCultureInfo("vi-VN");
    private static readonly HtmlEncoder VietnameseSafeEncoder = HtmlEncoder.Create(UnicodeRanges.All);

    public static string ConfirmedSubject(string code) => $"[ZIONShop] Đơn hàng {code} đã được xác nhận";
    public static string ShippedSubject(string code) => $"[ZIONShop] Đơn hàng {code} đang được giao";
    public static string CancelledSubject(string code) => $"[ZIONShop] Đơn hàng {code} đã bị huỷ";

    public static string BuildConfirmedBody(string? recipientDisplayName, OrderDetails order, string storefrontBaseUrl)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Arial,sans-serif;font-size:14px;color:#1f2937;\">");
        sb.Append($"<p>Xin chào {Encode(recipientDisplayName ?? order.RecipientName)},</p>");
        sb.Append($"<p>Đơn hàng <strong>{Encode(order.Code)}</strong> của bạn đã được xác nhận.</p>");
        sb.Append(BuildLinesTable(order.Lines));
        sb.Append("<table style=\"width:100%;margin-top:12px;\">");
        sb.Append(SummaryRow("Tạm tính", FormatMoney(order.Subtotal)));
        if (order.DiscountAmount > 0)
        {
            var label = BuildDiscountLabel(order.CouponCode, order.DiscountTarget);
            sb.Append(SummaryRow(label, "-" + FormatMoney(order.DiscountAmount)));
        }
        sb.Append(SummaryRow("Phí vận chuyển", FormatMoney(order.ShippingFee)));
        sb.Append(SummaryRow("Tổng cộng", FormatMoney(order.Total), emphasize: true));
        sb.Append("</table>");
        sb.Append($"<p>Phương thức thanh toán: {PaymentMethodLabel(order.PaymentMethod)}</p>");
        sb.Append(BuildAddressBlock(order));
        sb.Append(BuildButton(storefrontBaseUrl, order.OrderId));
        sb.Append("</div>");
        return WrapHtmlDocument(sb.ToString());
    }

    public static string BuildShippedBody(string? recipientDisplayName, OrderDetails order, string storefrontBaseUrl)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Arial,sans-serif;font-size:14px;color:#1f2937;\">");
        sb.Append($"<p>Xin chào {Encode(recipientDisplayName ?? order.RecipientName)},</p>");
        sb.Append($"<p>Đơn hàng <strong>{Encode(order.Code)}</strong> của bạn đang được giao.</p>");
        sb.Append(BuildAddressBlock(order));
        sb.Append(BuildButton(storefrontBaseUrl, order.OrderId));
        sb.Append("</div>");
        return WrapHtmlDocument(sb.ToString());
    }

    public static string BuildCancelledBody(string? recipientDisplayName, OrderDetails order, string storefrontBaseUrl)
    {
        var sb = new StringBuilder();
        sb.Append("<div style=\"font-family:Arial,sans-serif;font-size:14px;color:#1f2937;\">");
        sb.Append($"<p>Xin chào {Encode(recipientDisplayName ?? order.RecipientName)},</p>");
        sb.Append($"<p>Đơn hàng <strong>{Encode(order.Code)}</strong> của bạn đã bị huỷ.</p>");
        if (!string.IsNullOrWhiteSpace(order.CancelReason))
            sb.Append($"<p>Lý do: {Encode(order.CancelReason)}</p>");
        sb.Append(BuildButton(storefrontBaseUrl, order.OrderId));
        sb.Append("</div>");
        return WrapHtmlDocument(sb.ToString());
    }

    private static string BuildLinesTable(IReadOnlyList<OrderDetailsLine> lines)
    {
        var sb = new StringBuilder();
        sb.Append("<table style=\"width:100%;border-collapse:collapse;margin-top:12px;\">");
        sb.Append("<tr>")
          .Append("<th style=\"text-align:left;border-bottom:1px solid #e5e7eb;padding:6px;\">Tên</th>")
          .Append("<th style=\"text-align:right;border-bottom:1px solid #e5e7eb;padding:6px;\">SL</th>")
          .Append("<th style=\"text-align:right;border-bottom:1px solid #e5e7eb;padding:6px;\">Đơn giá</th>")
          .Append("<th style=\"text-align:right;border-bottom:1px solid #e5e7eb;padding:6px;\">Thành tiền</th>")
          .Append("</tr>");
        foreach (var line in lines)
        {
            var baseName = string.IsNullOrEmpty(line.VariantLabel) ? line.ProductName : $"{line.ProductName} ({line.VariantLabel})";
            var name = line.IsGift ? $"{baseName} (Quà tặng)" : baseName;
            var unitPriceCell = line.IsGift ? "Miễn phí" : FormatMoney(line.UnitPrice);
            var lineTotalCell = line.IsGift ? "Miễn phí" : FormatMoney(line.LineTotal);
            sb.Append("<tr>")
              .Append($"<td style=\"padding:6px;\">{Encode(name)}</td>")
              .Append($"<td style=\"padding:6px;text-align:right;\">{line.Quantity}</td>")
              .Append($"<td style=\"padding:6px;text-align:right;\">{unitPriceCell}</td>")
              .Append($"<td style=\"padding:6px;text-align:right;\">{lineTotalCell}</td>")
              .Append("</tr>");
        }
        sb.Append("</table>");
        return sb.ToString();
    }

    private static string SummaryRow(string label, string value, bool emphasize = false)
    {
        var labelContent = emphasize ? $"<strong>{label}</strong>" : label;
        var valueContent = emphasize ? $"<strong>{value}</strong>" : value;
        return $"<tr><td style=\"padding:4px 6px;\">{labelContent}</td><td style=\"padding:4px 6px;text-align:right;\">{valueContent}</td></tr>";
    }

    private static string BuildDiscountLabel(string? couponCode, string? discountTarget)
    {
        var prefix = discountTarget == "Shipping" ? "Giảm phí vận chuyển" : "Giảm giá";
        return string.IsNullOrWhiteSpace(couponCode) ? prefix : $"{prefix} ({Encode(couponCode)})";
    }

    private static string BuildAddressBlock(OrderDetails order) =>
        $"<p>Địa chỉ nhận hàng: {Encode(order.RecipientName)} - {Encode(order.PhoneNumber)}<br/>{Encode(order.AddressLine)}</p>";

    private static string BuildButton(string storefrontBaseUrl, Guid orderId)
    {
        var url = $"{storefrontBaseUrl.TrimEnd('/')}/orders/{orderId}";
        return $"<p><a href=\"{Encode(url)}\" style=\"display:inline-block;padding:10px 16px;background:#111827;color:#ffffff;text-decoration:none;border-radius:6px;\">Xem đơn hàng</a></p>";
    }

    private static string PaymentMethodLabel(string paymentMethod) =>
        string.Equals(paymentMethod, "COD", StringComparison.OrdinalIgnoreCase)
            ? "Thanh toán khi nhận hàng"
            : "Đã thanh toán online";

    private static string FormatMoney(decimal amount) => $"{amount.ToString("N0", ViVn)} ₫";

    private static string Encode(string? value) => VietnameseSafeEncoder.Encode(value ?? string.Empty);

    private static string WrapHtmlDocument(string bodyContent) =>
        $"<html><head><meta charset=\"utf-8\"></head><body>{bodyContent}</body></html>";
}
