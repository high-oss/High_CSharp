// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json.Serialization;

namespace High.OpenApi.Models;

/// <summary>
/// The account holder's profile: identity, demat and bank details. PAN, Aadhaar
/// and bank account numbers are masked to their last four characters.
/// </summary>
public sealed record Profile
{
    [JsonPropertyName("clientId")] public required string ClientId { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("email")] public required string Email { get; init; }
    [JsonPropertyName("mobile")] public required string Mobile { get; init; }

    /// <summary>Masked to the last four characters.</summary>
    [JsonPropertyName("pan")] public required string Pan { get; init; }

    /// <summary>Masked to the last four characters.</summary>
    [JsonPropertyName("aadhaar")] public required string Aadhaar { get; init; }

    [JsonPropertyName("depository")] public required string Depository { get; init; }
    [JsonPropertyName("boCode")] public required string BoCode { get; init; }
    [JsonPropertyName("dpCode")] public required string DpCode { get; init; }
    [JsonPropertyName("isPOAApproved")] public required bool IsPoaApproved { get; init; }
    [JsonPropertyName("isNRI")] public required bool IsNri { get; init; }
    [JsonPropertyName("isMTFEnabled")] public required bool IsMtfEnabled { get; init; }
    [JsonPropertyName("segmentsAllowed")] public required IReadOnlyList<string> SegmentsAllowed { get; init; }
    [JsonPropertyName("address")] public required ProfileAddress Address { get; init; }
    [JsonPropertyName("bankAccounts")] public required IReadOnlyList<ProfileBankAccount> BankAccounts { get; init; }
}

public sealed record ProfileAddress
{
    [JsonPropertyName("address")] public required string Address { get; init; }
    [JsonPropertyName("city")] public required string City { get; init; }
    [JsonPropertyName("state")] public required string State { get; init; }
    [JsonPropertyName("pinCode")] public required string PinCode { get; init; }
}

public sealed record ProfileBankAccount
{
    [JsonPropertyName("bankName")] public required string BankName { get; init; }

    /// <summary>Masked to the last four characters.</summary>
    [JsonPropertyName("accountNumber")] public required string AccountNumber { get; init; }

    [JsonPropertyName("ifscCode")] public required string IfscCode { get; init; }
    [JsonPropertyName("branch")] public required string Branch { get; init; }
    [JsonPropertyName("isPrimary")] public required bool IsPrimary { get; init; }
}
