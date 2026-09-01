using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EDM.ControlPlane.Api.Services
{
    public record PaymentCheckoutRequest(
        Guid InstallationId,
        Guid? UserId,
        string PlanCode,
        string CountryCode,
        string Currency,
        decimal Amount,
        string SuccessUrl,
        string CancelUrl,
        Dictionary<string, string>? CustomMetadata = null);

    public record PaymentCheckoutResult(
        bool Success,
        string? CheckoutUrl,
        string? SessionId,
        string? ErrorMessage,
        bool IsLiveProvider = false);

    public record PaymentVerificationResult(
        bool IsVerified,
        string? TransactionId,
        string? PlanCode,
        string? PayerEmail,
        decimal Amount,
        string Currency,
        DateTime? PaidAtUtc,
        string? ErrorMessage);

    public record WebhookProcessingResult(
        bool Handled,
        string EventType,
        string? TransactionId,
        Guid? InstallationId,
        Guid? UserId,
        string? PlanCode,
        bool ShouldActivateSubscription,
        string? Message);

    public interface IPaymentProvider
    {
        string ProviderName { get; }
        bool IsLiveConnected { get; }
        Task<PaymentCheckoutResult> CreateCheckoutAsync(PaymentCheckoutRequest request);
        Task<PaymentVerificationResult> VerifyPaymentAsync(string transactionId);
        Task<WebhookProcessingResult> ProcessWebhookAsync(string payload, string signatureHeader);
    }

    public class NullPaymentProvider : IPaymentProvider
    {
        public string ProviderName => "None";
        public bool IsLiveConnected => false;

        public Task<PaymentCheckoutResult> CreateCheckoutAsync(PaymentCheckoutRequest request)
        {
            return Task.FromResult(new PaymentCheckoutResult(
                Success: false,
                CheckoutUrl: null,
                SessionId: null,
                ErrorMessage: "PAYMENT PROCESSOR NOT CONNECTED — architecture prepared, live payment not verified.",
                IsLiveProvider: false));
        }

        public Task<PaymentVerificationResult> VerifyPaymentAsync(string transactionId)
        {
            return Task.FromResult(new PaymentVerificationResult(
                IsVerified: false,
                TransactionId: transactionId,
                PlanCode: null,
                PayerEmail: null,
                Amount: 0m,
                Currency: "USD",
                PaidAtUtc: null,
                ErrorMessage: "PAYMENT PROCESSOR NOT CONNECTED — architecture prepared, live payment not verified."));
        }

        public Task<WebhookProcessingResult> ProcessWebhookAsync(string payload, string signatureHeader)
        {
            return Task.FromResult(new WebhookProcessingResult(
                Handled: false,
                EventType: "NONE",
                TransactionId: null,
                InstallationId: null,
                UserId: null,
                PlanCode: null,
                ShouldActivateSubscription: false,
                Message: "PAYMENT PROCESSOR NOT CONNECTED — architecture prepared, live payment not verified."));
        }
    }

    public class StripePaymentProvider : IPaymentProvider
    {
        public string ProviderName => "Stripe";
        public bool IsLiveConnected => false;

        public Task<PaymentCheckoutResult> CreateCheckoutAsync(PaymentCheckoutRequest request)
        {
            return Task.FromResult(new PaymentCheckoutResult(
                Success: false,
                CheckoutUrl: null,
                SessionId: null,
                ErrorMessage: "Stripe live keys not configured. Architecture prepared for future integration.",
                IsLiveProvider: false));
        }

        public Task<PaymentVerificationResult> VerifyPaymentAsync(string transactionId)
        {
            return Task.FromResult(new PaymentVerificationResult(
                IsVerified: false,
                TransactionId: transactionId,
                PlanCode: null,
                PayerEmail: null,
                Amount: 0m,
                Currency: "USD",
                PaidAtUtc: null,
                ErrorMessage: "Stripe live keys not configured. Architecture prepared for future integration."));
        }

        public Task<WebhookProcessingResult> ProcessWebhookAsync(string payload, string signatureHeader)
        {
            return Task.FromResult(new WebhookProcessingResult(
                Handled: false,
                EventType: "stripe.webhook.stub",
                TransactionId: null,
                InstallationId: null,
                UserId: null,
                PlanCode: null,
                ShouldActivateSubscription: false,
                Message: "Stripe live keys not configured. Architecture prepared for future integration."));
        }
    }

    public class PaddlePaymentProvider : IPaymentProvider
    {
        public string ProviderName => "Paddle";
        public bool IsLiveConnected => false;

        public Task<PaymentCheckoutResult> CreateCheckoutAsync(PaymentCheckoutRequest request)
        {
            return Task.FromResult(new PaymentCheckoutResult(
                Success: false,
                CheckoutUrl: null,
                SessionId: null,
                ErrorMessage: "Paddle live keys not configured. Architecture prepared for future integration.",
                IsLiveProvider: false));
        }

        public Task<PaymentVerificationResult> VerifyPaymentAsync(string transactionId)
        {
            return Task.FromResult(new PaymentVerificationResult(
                IsVerified: false,
                TransactionId: transactionId,
                PlanCode: null,
                PayerEmail: null,
                Amount: 0m,
                Currency: "USD",
                PaidAtUtc: null,
                ErrorMessage: "Paddle live keys not configured. Architecture prepared for future integration."));
        }

        public Task<WebhookProcessingResult> ProcessWebhookAsync(string payload, string signatureHeader)
        {
            return Task.FromResult(new WebhookProcessingResult(
                Handled: false,
                EventType: "paddle.webhook.stub",
                TransactionId: null,
                InstallationId: null,
                UserId: null,
                PlanCode: null,
                ShouldActivateSubscription: false,
                Message: "Paddle live keys not configured. Architecture prepared for future integration."));
        }
    }

    public class BkashPaymentProvider : IPaymentProvider
    {
        public string ProviderName => "bKash";
        public bool IsLiveConnected => false;

        public Task<PaymentCheckoutResult> CreateCheckoutAsync(PaymentCheckoutRequest request)
        {
            return Task.FromResult(new PaymentCheckoutResult(
                Success: false,
                CheckoutUrl: null,
                SessionId: null,
                ErrorMessage: "bKash merchant credentials not configured. Architecture prepared for future integration.",
                IsLiveProvider: false));
        }

        public Task<PaymentVerificationResult> VerifyPaymentAsync(string transactionId)
        {
            return Task.FromResult(new PaymentVerificationResult(
                IsVerified: false,
                TransactionId: transactionId,
                PlanCode: null,
                PayerEmail: null,
                Amount: 0m,
                Currency: "BDT",
                PaidAtUtc: null,
                ErrorMessage: "bKash merchant credentials not configured. Architecture prepared for future integration."));
        }

        public Task<WebhookProcessingResult> ProcessWebhookAsync(string payload, string signatureHeader)
        {
            return Task.FromResult(new WebhookProcessingResult(
                Handled: false,
                EventType: "bkash.webhook.stub",
                TransactionId: null,
                InstallationId: null,
                UserId: null,
                PlanCode: null,
                ShouldActivateSubscription: false,
                Message: "bKash merchant credentials not configured. Architecture prepared for future integration."));
        }
    }

    public interface IPaymentProviderFactory
    {
        IPaymentProvider GetProvider(string? providerName);
    }

    public class PaymentProviderFactory : IPaymentProviderFactory
    {
        private readonly Dictionary<string, IPaymentProvider> _providers;

        public PaymentProviderFactory()
        {
            _providers = new Dictionary<string, IPaymentProvider>(StringComparer.OrdinalIgnoreCase)
            {
                ["None"] = new NullPaymentProvider(),
                ["Stripe"] = new StripePaymentProvider(),
                ["Paddle"] = new PaddlePaymentProvider(),
                ["bKash"] = new BkashPaymentProvider()
            };
        }

        public IPaymentProvider GetProvider(string? providerName)
        {
            if (string.IsNullOrWhiteSpace(providerName) || !_providers.TryGetValue(providerName.Trim(), out var provider))
            {
                return _providers["None"];
            }
            return provider;
        }
    }
}
