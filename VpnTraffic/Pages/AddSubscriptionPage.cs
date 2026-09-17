using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using VpnTraffic.Localization;
using VpnTraffic.Services;

namespace VpnTraffic.Pages;

internal sealed class AddSubscriptionForm : FormContent
{
    private readonly VpnTrafficCommandsProvider _provider;

    public AddSubscriptionForm(VpnTrafficCommandsProvider provider)
    {
        _provider = provider;
        TemplateJson = """
{
  "$schema": "http://adaptivecards.io/schemas/adaptive-card.json",
  "type": "AdaptiveCard",
  "version": "1.6",
  "body": [
    {
      "type": "TextBlock",
      "size": "medium",
      "weight": "bolder",
      "text": "Add subscription"
    },
    {
      "type": "Input.Text",
      "label": "Name",
      "id": "Name",
      "style": "text",
      "placeholder": "Airport A",
      "isRequired": false
    },
    {
      "type": "Input.Text",
      "label": "Subscription URL",
      "id": "Url",
      "style": "text",
      "placeholder": "https://example.com/sub?token=...",
      "isRequired": true,
      "errorMessage": "URL is required"
    }
  ],
  "actions": [
    {
      "type": "Action.Submit",
      "title": "Save",
      "data": { "id": "add-sub" }
    }
  ]
}
""";
    }

    public override CommandResult SubmitForm(string payload)
    {
        try
        {
            var node = JsonNode.Parse(payload)?.AsObject();
            var name = node?["Name"]?.ToString() ?? string.Empty;
            var url = node?["Url"]?.ToString() ?? string.Empty;
            var ok = _provider.TryAddSubscription(name, url, out var error);
            var msg = ok ? Localizer.Saved : error;
            return CommandResult.ShowToast(msg);
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast(ex.Message);
        }
    }
}

internal sealed class AddSubscriptionPage : ContentPage
{
    private readonly AddSubscriptionForm _form;

    public AddSubscriptionPage(VpnTrafficCommandsProvider provider)
    {
        Name = Localizer.AddSubscription;
        Title = Localizer.AddSubscription;
        Icon = new IconInfo("\uE968");
        _form = new AddSubscriptionForm(provider);
    }

    public override IContent[] GetContent() => [_form];
}
