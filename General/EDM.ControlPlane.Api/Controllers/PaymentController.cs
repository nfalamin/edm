using EDM.ControlPlane.Api.Middleware;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EDM.ControlPlane.Api.Data;
using EDM.ControlPlane.Api.Models;
using EDM.ControlPlane.Api.Services;

namespace EDM.ControlPlane.Api.Controllers
{
    public record CreateCheckoutRequestDto(
        Guid InstallationId,
        Guid? UserId,
        string PlanCode,
        string? SuccessUrl,
        string? CancelUrl,
        string? CouponCode = null);

    public record RefundRequestDto(string Reason, decimal? Amount = null);

    public record ReconcileRequestDto(PaymentStatus TargetStatus, string Reason);

    [ApiController]
    [Route("api/v1/payments")]
    public class PaymentController : ControllerBase
    {
        private readonly ControlPlaneDbContext _dbContext;
        private readonly ISubscriptionEntitlementService _entitlementService;
        private readonly IGeoPricingService _geoPricingService;
        private readonly IPaymentProviderFactory _providerFactory;
        private readonly IAuditLoggingService _auditLogger;

        public PaymentController(
            ControlPlaneDbContext dbContext,
            ISubscriptionEntitlementService entitlementService,
            IGeoPricingService geoPricingService,
            IPaymentProviderFactory providerFactory,
            IAuditLoggingService auditLogger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _entitlementService = entitlementService ?? throw new ArgumentNullException(nameof(entitlementService));
            _geoPricingService = geoPricingService ?? throw new ArgumentNullException(nameof(geoPricingService));
            _providerFactory = providerFactory ?? throw new ArgumentNullException(nameof(providerFactory));
            _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger));
        }

        /// <summary>
        /// Create a Server-Authoritative Checkout Session
        /// </summary>
        [HttpPost("checkout")]
        public async Task<IActionResult> CreateCheckoutAsync([FromBody] CreateCheckoutRequestDto request)
        {
            if (request == null || request.InstallationId == Guid.Empty || string.IsNullOrWhiteSpace(request.PlanCode))
            {
                return BadRequest(new { error = "INVALID_INPUT", message = "InstallationId and PlanCode are required." });
            }

            var globalConfig = await _entitlementService.GetGlobalConfigAsync();
            if (!globalConfig.IsGlobalSubscriptionEnabled)
            {
                return BadRequest(new { error = "SUBSCRIPTION_SALES_DISABLED", message = "New subscription purchases are temporarily disabled globally." });
            }

            string clientIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
            var headers = Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
            string country = _geoPricingService.DetectCountryFromHeadersOrIp(clientIp, headers);
            string region = _geoPricingService.DetectRegionForCountry(country);
            var pricing = await _geoPricingService.GetPricingRuleForCountryAsync(country);

            if ((region == "Asia" || region == "South Asia") && !globalConfig.IsAsiaSubscriptionEnabled)
            {
                return BadRequest(new { error = "REGIONAL_SALES_DISABLED", message = "Subscription purchases are temporarily disabled in the Asia region." });
            }

            if (!pricing.IsSubscriptionEnabled)
            {
                return BadRequest(new { error = "COUNTRY_SALES_DISABLED", message = $"Subscription purchases are temporarily disabled in {pricing.CountryCode}." });
            }

            decimal baseAmount = request.PlanCode.Contains("year", StringComparison.OrdinalIgnoreCase) ? pricing.YearlyPrice : pricing.MonthlyPrice;
            decimal discountAmount = 0m;
            PromotionRecord? appliedPromo = null;

            if (!string.IsNullOrWhiteSpace(request.CouponCode))
            {
                string code = request.CouponCode.Trim().ToUpperInvariant();
                appliedPromo = await _dbContext.Promotions
                    .FirstOrDefaultAsync(p => p.PromoCode == code && p.IsEnabled);

                if (appliedPromo != null && (appliedPromo.EndsAtUtc == null || appliedPromo.EndsAtUtc > DateTime.UtcNow) &&
                    (!appliedPromo.MaxUses.HasValue || appliedPromo.CurrentUses < appliedPromo.MaxUses.Value))
                {
                    if (appliedPromo.DiscountPercent.HasValue && appliedPromo.DiscountPercent.Value > 0)
                    {
                        discountAmount = Math.Round(baseAmount * (appliedPromo.DiscountPercent.Value / 100m), 2);
                    }
                    else if (appliedPromo.DiscountAmount.HasValue && appliedPromo.DiscountAmount.Value > 0)
                    {
                        discountAmount = Math.Min(baseAmount, appliedPromo.DiscountAmount.Value);
                    }

                    // Increment coupon usage
                    appliedPromo.CurrentUses++;
                    _dbContext.CouponUsages.Add(new CouponUsageRecord
                    {
                        Id = Guid.NewGuid(),
                        PromotionId = appliedPromo.Id,
                        PromoCode = appliedPromo.PromoCode,
                        UserId = request.UserId,
                        InstallationId = request.InstallationId,
                        DiscountAmount = discountAmount,
                        Currency = pricing.Currency,
                        UsedAtUtc = DateTime.UtcNow
                    });
                }
            }

            decimal finalAmount = Math.Max(0m, baseAmount - discountAmount);

            var checkoutRecord = new PaymentRecord
            {
                Id = Guid.NewGuid(),
                InstallationId = request.InstallationId,
                UserId = request.UserId,
                PlanCode = request.PlanCode,
                Provider = globalConfig.PaymentProvider,
                Amount = finalAmount,
                Currency = pricing.Currency,
                CountryCode = country,
                Status = PaymentStatus.PENDING,
                IsTestMode = globalConfig.IsTestMode,
                ExpiresAtUtc = DateTime.UtcNow.AddMinutes(30)
            };

            var provider = _providerFactory.GetProvider(globalConfig.PaymentProvider);
            var checkoutResult = await provider.CreateCheckoutAsync(new PaymentCheckoutRequest(
                InstallationId: request.InstallationId,
                UserId: request.UserId,
                PlanCode: request.PlanCode,
                CountryCode: country,
                Currency: pricing.Currency,
                Amount: finalAmount,
                SuccessUrl: request.SuccessUrl ?? "https://edm-download.com/checkout/success",
                CancelUrl: request.CancelUrl ?? "https://edm-download.com/checkout/cancel"
            ));

            checkoutRecord.ProviderSessionId = checkoutResult.SessionId;
            _dbContext.Payments.Add(checkoutRecord);
            await _dbContext.SaveChangesAsync();

            await _auditLogger.LogActionAsync(
                request.UserId,
                request.UserId?.ToString() ?? "Anonymous",
                "CHECKOUT_CREATED",
                "PaymentRecord",
                checkoutRecord.Id.ToString(),
                $"Created checkout for {request.PlanCode} ({pricing.Currency} {finalAmount}) via {provider.ProviderName}",
                HttpContext.TraceIdentifier);

            return Ok(new
            {
                checkoutId = checkoutRecord.Id,
                provider = provider.ProviderName,
                isLiveProvider = checkoutResult.IsLiveProvider,
                amount = finalAmount,
                currency = pricing.Currency,
                formattedPrice = _geoPricingService.FormatPrice(finalAmount, pricing.Currency, pricing.CurrencySymbol),
                status = checkoutRecord.Status.ToString(),
                checkoutUrl = checkoutResult.CheckoutUrl,
                statusNotice = provider.IsLiveConnected ? "Ready for checkout" : "PAYMENT PROCESSOR NOT CONNECTED — architecture prepared, live payment not verified."
            });
        }

        /// <summary>
        /// Idempotent Webhook Processing for Payment Providers
        /// </summary>
        [HttpPost("webhook/{providerName}")]
        public async Task<IActionResult> ProcessWebhookAsync(string providerName, [FromHeader(Name = "X-Webhook-Signature")] string? signature)
        {
            using var reader = new System.IO.StreamReader(Request.Body);
            string payload = await reader.ReadToEndAsync();

            var provider = _providerFactory.GetProvider(providerName);
            var result = await provider.ProcessWebhookAsync(payload, signature ?? string.Empty);

            if (!result.Handled)
            {
                return Ok(new { status = "IGNORED_OR_NOT_CONNECTED", message = result.Message });
            }

            // Webhook Idempotency Check
            if (!string.IsNullOrWhiteSpace(result.TransactionId))
            {
                var existingEvent = await _dbContext.WebhookEvents
                    .FirstOrDefaultAsync(w => w.Provider == providerName && w.ProviderEventId == result.TransactionId);

                if (existingEvent != null)
                {
                    return Ok(new { status = "DUPLICATE_EVENT_ALREADY_PROCESSED" });
                }

                _dbContext.WebhookEvents.Add(new WebhookEventRecord
                {
                    Provider = providerName,
                    ProviderEventId = result.TransactionId,
                    EventType = result.EventType,
                    PayloadJson = payload.Length > 2000 ? payload[..2000] : payload,
                    IsProcessed = true,
                    ProcessingResult = "SUCCESS"
                });
            }

            // If verified payment, activate subscription
            if (result.ShouldActivateSubscription && result.InstallationId.HasValue)
            {
                var policy = await _dbContext.SubscriptionPolicies
                    .FirstOrDefaultAsync(p => p.InstallationId == result.InstallationId.Value);

                if (policy != null)
                {
                    policy.CurrentState = SubscriptionState.SUBSCRIBED;
                    policy.SubscriptionExpiresAtUtc = DateTime.UtcNow.AddDays(30);
                    policy.ActivePlanCode = result.PlanCode ?? "pro_monthly";
                    policy.UpdatedAtUtc = DateTime.UtcNow;
                }

                await _auditLogger.LogActionAsync(
                    result.UserId,
                    "WebhookService",
                    "SUBSCRIPTION_ACTIVATED",
                    "SubscriptionPolicy",
                    result.InstallationId.Value.ToString(),
                    $"Activated subscription via {providerName} webhook ({result.TransactionId})",
                    HttpContext.TraceIdentifier);
            }

            await _dbContext.SaveChangesAsync();
            return Ok(new { status = "PROCESSED", message = result.Message });
        }

        /// <summary>
        /// Admin Transaction Lookup with Server-Side Pagination
        /// </summary>
        [Authorize]
        [RequirePermission(Permissions.UsersRead)]
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactionsAsync(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 50;

            var query = _dbContext.Payments.Include(p => p.User).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLowerInvariant();
                query = query.Where(p => p.InstallationId.ToString().Contains(s) || p.PlanCode.ToLower().Contains(s) || (p.ProviderTransactionId != null && p.ProviderTransactionId.Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<PaymentStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(p => p.Status == parsedStatus);
            }

            var totalCount = await query.CountAsync();
            var list = await query
                .OrderByDescending(p => p.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    p.Id,
                    p.InstallationId,
                    p.UserId,
                    UserEmail = p.User != null ? p.User.Email : null,
                    p.PlanCode,
                    p.Provider,
                    p.ProviderTransactionId,
                    p.Amount,
                    p.Currency,
                    p.CountryCode,
                    Status = p.Status.ToString(),
                    p.IsTestMode,
                    p.CreatedAtUtc,
                    p.PaidAtUtc
                })
                .ToListAsync();

            return Ok(new { totalCount, page, pageSize, transactions = list });
        }

        /// <summary>
        /// Admin Refund Action
        /// </summary>
        [Authorize]
        [RequirePermission(Permissions.UsersManage)]
        [HttpPost("admin/{id}/refund")]
        public async Task<IActionResult> ProcessRefundAsync(Guid id, [FromBody] RefundRequestDto request)
        {
            var adminName = User.Identity?.Name ?? "SuperAdmin";
            var payment = await _dbContext.Payments.FindAsync(id);
            if (payment == null) return NotFound(new { error = "PAYMENT_NOT_FOUND", message = "Transaction record not found." });

            payment.Status = PaymentStatus.REFUNDED;
            payment.FailureReason = $"Refunded by {adminName}. Reason: {request.Reason}";
            payment.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditLogger.LogActionAsync(
                null,
                adminName,
                "PAYMENT_REFUNDED",
                "PaymentRecord",
                id.ToString(),
                $"Refund applied. Reason: {request.Reason}",
                HttpContext.TraceIdentifier);

            return Ok(new { success = true, status = payment.Status.ToString(), message = "Payment marked as refunded." });
        }

        /// <summary>
        /// Admin Manual Reconciliation
        /// </summary>
        [Authorize]
        [RequirePermission(Permissions.UsersManage)]
        [HttpPost("admin/{id}/reconcile")]
        public async Task<IActionResult> ReconcilePaymentAsync(Guid id, [FromBody] ReconcileRequestDto request)
        {
            var adminName = User.Identity?.Name ?? "SuperAdmin";
            var payment = await _dbContext.Payments.FindAsync(id);
            if (payment == null) return NotFound(new { error = "PAYMENT_NOT_FOUND", message = "Transaction record not found." });

            var prevStatus = payment.Status;
            payment.Status = request.TargetStatus;
            payment.UpdatedAtUtc = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();
            await _auditLogger.LogActionAsync(
                null,
                adminName,
                "PAYMENT_RECONCILED",
                "PaymentRecord",
                id.ToString(),
                $"Status changed from {prevStatus} to {request.TargetStatus}. Reason: {request.Reason}",
                HttpContext.TraceIdentifier);

            return Ok(new { success = true, previousStatus = prevStatus.ToString(), newStatus = payment.Status.ToString() });
        }
    }
}
