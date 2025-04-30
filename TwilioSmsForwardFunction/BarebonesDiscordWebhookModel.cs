using System.Text.Json.Serialization;

namespace TwilioSmsForwardFunction {
    /// <summary>
    /// Represents a discord webhook.
    /// See https://discord.com/developers/docs/resources/webhook#execute-webhook
    /// </summary>
    internal class BarebonesDiscordWebhookModel {
        /// <summary>
        /// The user for the webhook.
        /// </summary>
        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The content for the webhook.
        /// </summary>
        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;
    }
}
