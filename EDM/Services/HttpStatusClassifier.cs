using System.Net;

namespace EDM.Services
{
    public enum HttpStatusCategory
    {
        Success,
        Retryable,
        AuthRequired,
        RateLimited,
        PermanentFailure,
        Unsupported,
        ServerFailure,
        NetworkTransient,
        RangeInvalid,
        Cancelled
    }

    public static class HttpStatusClassifier
    {
        public static HttpStatusCategory Classify(HttpStatusCode statusCode)
        {
            int code = (int)statusCode;

            if (code >= 200 && code <= 299) return HttpStatusCategory.Success;
            if (code == 401 || code == 403 || code == 407) return HttpStatusCategory.AuthRequired;
            if (code == 429) return HttpStatusCategory.RateLimited;
            if (code == 408) return HttpStatusCategory.Retryable;
            if (code == 416) return HttpStatusCategory.RangeInvalid;
            if (code == 400 || code == 404 || code == 405 || code == 406 || code == 410 || code == 415 || code == 422) return HttpStatusCategory.PermanentFailure;
            if (code == 501) return HttpStatusCategory.Unsupported;
            if (code >= 500 && code <= 599) return HttpStatusCategory.ServerFailure;

            return HttpStatusCategory.PermanentFailure;
        }

        public static bool IsRetryableCategory(HttpStatusCategory category)
        {
            return category == HttpStatusCategory.Retryable ||
                   category == HttpStatusCategory.RateLimited ||
                   category == HttpStatusCategory.ServerFailure ||
                   category == HttpStatusCategory.NetworkTransient;
        }
    }
}
