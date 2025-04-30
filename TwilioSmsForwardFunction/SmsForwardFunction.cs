using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using Twilio.Security;

namespace TwilioSmsForwardFunction {
    public class SmsForwardFunction {
        /// <summary>
        /// The <see cref="ILogger"/> for the <see cref="SmsForwardFunction"/>.
        /// </summary>
        private readonly ILogger<SmsForwardFunction> _logger;

        /// <summary>
        /// The <see cref="HttpClient"/> for the <see cref="ByondChangelogFunction"/>.
        /// </summary>
        private readonly HttpClient _apiClient;

        /// <summary>
        /// The <see cref="string"/> URL we are sending discord webhooks to.
        /// </summary>
        private readonly string _discordWebhook;

        /// <summary>
        /// Our instance of the Twilio <see cref="RequestValidator"/> to ensure requests are valid.
        /// </summary>
        private readonly RequestValidator _validator;

        /// <summary>
        /// A constant <see cref="string"/> reference for the header name that contains the validation signature.
        /// </summary>
        const string TWILIO_HEADER_AUTH_KEY = "X-Twilio-Signature";

        /// <summary>
        /// A constant <see cref="string"/> reference for the form key that contains where the SMS came from.
        /// </summary>
        const string TWILIO_FORM_FROM_KEY = "From";

        /// <summary>
        /// A constant <see cref="string"/> reference for the form key that contains the SMS content.
        /// </summary>
        const string TWILIO_FORM_BODY_KEY = "Body";

        /// <summary>
        /// Creates the <see cref="SmsForwardFunction"/> handler.
        /// Automatically invoked by the functions runtime - do not manually instance.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> for the <see cref="SmsForwardFunction"/>.</param>
        public SmsForwardFunction(ILogger<SmsForwardFunction> logger) {
            // Assign passed data
            _logger = logger;

            // Initial setup checks
            string? twilio_auth_token = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");
            string? discord_webhook = Environment.GetEnvironmentVariable("DISCORD_WEBHOOK");

            if (string.IsNullOrWhiteSpace(twilio_auth_token) || string.IsNullOrWhiteSpace(discord_webhook)) {
                throw new Exception("Missing env vars");
            }

            // Assign this
            _discordWebhook = discord_webhook;

            // Create our signature validator
            _validator = new(twilio_auth_token);

            // Create our HTTP client options.
            // If we dont set this, we can run out of TCP states and requests dont go through.
            // Dont ask me how I know this.
            HttpClientHandler options = new();
            options.MaxConnectionsPerServer = 256;

            // Now create the client itself with our options above
            _apiClient = new(options);
        }



        [Function("ForwardSms")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req) {
            // Now try decode our signature header
            if (!req.Headers.ContainsKey(TWILIO_HEADER_AUTH_KEY)) {
                return new BadRequestResult();
            }

            // Validate the body first because that is cheaper than signature validator, I think
            if (!req.Form.ContainsKey(TWILIO_FORM_FROM_KEY) || !req.Form.ContainsKey(TWILIO_FORM_BODY_KEY)) {
                return new BadRequestResult();
            }

            // Get the parameters ou the body
            string? sms_sender = req.Form[TWILIO_FORM_FROM_KEY];
            string? sms_content = req.Form[TWILIO_FORM_BODY_KEY];

            if (string.IsNullOrWhiteSpace(sms_sender) || string.IsNullOrWhiteSpace(sms_content)) {
                return new BadRequestResult();
            }

            // Validate our signature
            bool is_valid = await IsValidTwilioSignature(req);

            // Not a valid signature - bail
            if (!is_valid) {
                return new UnauthorizedResult();
            }

            // Create our webhook to send data to
            BarebonesDiscordWebhookModel webhook_model = new() {
                Username = sms_sender,
                Content = sms_content
            };

            // Now we are here we can send the webhook
            await _apiClient.PostAsync(_discordWebhook, JsonContent.Create(webhook_model));

            // And return code 200 back to Twilio
            return new OkResult();
        }



        /// <summary>
        /// Checks if the supplied <see cref="HttpRequest"/> came from Twilio.
        /// </summary>
        /// <param name="req">The <see cref="HttpRequest"/> to check.</param>
        /// <returns>A true or false <see cref="bool"/> depending on whether we could validate that a request came from Twilio.</returns>
        private async Task<bool> IsValidTwilioSignature(HttpRequest req) {
            // Reconstruct our URL that Twilio needs
            string request_url = $"https://{req.Host}{req.Path}";
            
            // Get our signature out the headers
            string? twilio_sig = req.Headers[TWILIO_HEADER_AUTH_KEY];
            if (string.IsNullOrWhiteSpace(twilio_sig)) {
                return false;
            }

            // Take the form keys
            IFormCollection form = await req.ReadFormAsync(req.HttpContext.RequestAborted).ConfigureAwait(false);
            Dictionary<string, string> form_keys = form.ToDictionary(p => p.Key, p => p.Value.ToString());

            // Then check against the library.
            return _validator.Validate(request_url, form_keys, twilio_sig);
        }
    }
}
