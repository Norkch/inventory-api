using System.ComponentModel.DataAnnotations;

namespace Inventory.Api.Configuration;

public class AuthOptions
{
    public const string SectionName = "Auth";

    [Required]
    public string MetadataAddress { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Required]
    public string SwaggerClientId { get; set; } = string.Empty;
}
