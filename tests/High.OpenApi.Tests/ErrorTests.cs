// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json.Nodes;
using High.OpenApi;
using Xunit;

namespace High.OpenApi.Tests;

public class ErrorTests
{
    [Fact]
    public void FromResponse_reads_code_requestId_and_a_single_message()
    {
        var body = JsonNode.Parse("""{"requestId":"a1b2c3","code":"ORDER_REJECTED","message":"Insufficient margin"}""");
        var error = HighApiError.FromResponse(422, body);

        Assert.Equal(422, error.Status);
        Assert.Equal("ORDER_REJECTED", error.Code);
        Assert.Equal("a1b2c3", error.RequestId);
        Assert.Equal(new[] { "Insufficient margin" }, error.Messages);
        Assert.Equal("Insufficient margin", error.Message);
    }

    [Fact]
    public void FromResponse_keeps_every_message_when_validation_returns_several()
    {
        var body = JsonNode.Parse("""{"code":"VALIDATION_ERROR","message":["quantity is required","price must be positive"]}""");
        var error = HighApiError.FromResponse(400, body);

        Assert.Equal(new[] { "quantity is required", "price must be positive" }, error.Messages);
        Assert.Equal("quantity is required; price must be positive", error.Message);
    }

    [Fact]
    public void FromResponse_surfaces_a_non_JSON_body_with_the_real_status()
    {
        var error = HighApiError.FromResponse(502, null, "<html>Bad Gateway</html>");
        Assert.Equal(502, error.Status);
        Assert.Contains("502", error.Message);
        Assert.Contains("Bad Gateway", error.Message);
        Assert.IsType<string>(error.Body);
        Assert.Equal("<html>Bad Gateway</html>", error.Body);
    }

    [Fact]
    public void FromResponse_falls_back_to_a_bare_status_with_no_body_at_all()
    {
        var error = HighApiError.FromResponse(500, null, null);
        Assert.Equal("HTTP 500", error.Message);
    }

    [Fact]
    public void ErrorCodes_All_contains_every_documented_constant()
    {
        Assert.Contains(ErrorCodes.OrderRejected, ErrorCodes.All);
        Assert.Contains(ErrorCodes.ValidationError, ErrorCodes.All);
        Assert.Equal(29, ErrorCodes.All.Count);
    }
}
