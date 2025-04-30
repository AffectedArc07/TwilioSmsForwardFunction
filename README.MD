# ByondChangelogAzureFunction

## About

A dotnet 8 (LTS) Azure Function to forward SMSs to a Discord webhook from Twilio.

## Deploying

This can be deployed on any Azure functions plan on both Windows and Linux, but you need to set up the following environment variables:

- `TWILIO_AUTH_TOKEN` - The auth token for your Twilio account, used to confirm signatures.
- `DISCORD_WEBHOOK` - The URL for the webhook where you want SMSs to be forwarded to.

## License

The project is licensed under MIT. You can find a copy in `LICENSE.MD`.
