namespace Api.Services
{
    public class TokenSettings
    {
        public string SigningKey { get; set; } = string.Empty; // symmetric key
        public string Issuer { get; set; } = "SafeVault";
        public string Audience { get; set; } = "SafeVaultClients";
        public int ExpiryMinutes { get; set; } = 60;
    }
}
