// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi;

/// <summary>
/// The documented error catalogue, from the spec's <c>Error.code['x-error-codes']</c>.
///
/// This is a list of constants, not a closed enum, on purpose: the gateway
/// defines far more codes than the catalogue documents and passes most through
/// unchanged, so a closed enum would make <see cref="HighApiError.Code"/>
/// unassignable for a code that is perfectly real. Compare against these
/// constants; expect others. A conformance test asserts this list still
/// matches the vendored spec.
/// </summary>
public static class ErrorCodes
{
    public const string InvalidApiKey = "INVALID_API_KEY";
    public const string InvalidConsent = "INVALID_CONSENT";
    public const string InvalidDate = "INVALID_DATE";
    public const string InvalidScrip = "INVALID_SCRIP";
    public const string InvalidToken = "INVALID_TOKEN";
    public const string InvalidTokenId = "INVALID_TOKEN_ID";
    public const string InvalidTotp = "INVALID_TOTP";
    public const string OrderDetailsInvalid = "ORDER_DETAILS_INVALID";
    public const string OrderNotCancellable = "ORDER_NOT_CANCELLABLE";
    public const string OrderNotFound = "ORDER_NOT_FOUND";
    public const string OrderRejected = "ORDER_REJECTED";
    public const string OrderStatusUnknown = "ORDER_STATUS_UNKNOWN";
    public const string OwnerMismatch = "OWNER_MISMATCH";
    public const string PositionConvertFailed = "POSITION_CONVERT_FAILED";
    public const string PositionNotExitable = "POSITION_NOT_EXITABLE";
    public const string PositionSquareOffFailed = "POSITION_SQUARE_OFF_FAILED";
    public const string QuantityNotInLots = "QUANTITY_NOT_IN_LOTS";
    public const string RedirectUrlNotConfigured = "REDIRECT_URL_NOT_CONFIGURED";
    public const string SandboxTokenNotAllowed = "SANDBOX_TOKEN_NOT_ALLOWED";
    public const string ScripNotFound = "SCRIP_NOT_FOUND";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
    public const string SessionExpired = "SESSION_EXPIRED";
    public const string SessionNotGenerated = "SESSION_NOT_GENERATED";
    public const string StaticIpMismatch = "STATIC_IP_MISMATCH";
    public const string StaticIpMissing = "STATIC_IP_MISSING";
    public const string TotpNotEnabled = "TOTP_NOT_ENABLED";
    public const string UnhandledError = "UNHANDLED_ERROR";
    public const string ValidationError = "VALIDATION_ERROR";
    public const string WrongTokenAudience = "WRONG_TOKEN_AUDIENCE";

    /// <summary>Every documented code, for the conformance test that diffs this against the spec.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        InvalidApiKey, InvalidConsent, InvalidDate, InvalidScrip, InvalidToken, InvalidTokenId,
        InvalidTotp, OrderDetailsInvalid, OrderNotCancellable, OrderNotFound, OrderRejected,
        OrderStatusUnknown, OwnerMismatch, PositionConvertFailed, PositionNotExitable,
        PositionSquareOffFailed, QuantityNotInLots, RedirectUrlNotConfigured, SandboxTokenNotAllowed,
        ScripNotFound, ServiceUnavailable, SessionExpired, SessionNotGenerated, StaticIpMismatch,
        StaticIpMissing, TotpNotEnabled, UnhandledError, ValidationError, WrongTokenAudience,
    };
}
