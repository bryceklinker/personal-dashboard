using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Microsoft.AspNetCore.Components;

namespace Personal.Dashboard.Web.Host.Tests.Support;

public record FindByRoleOptions(
    string? Text = null, 
    Regex? TextRegex = null,
    string? Label = null,
    Regex? LabelRegex = null)
{
    public bool Matches(IElement element)
    {
        return MatchesText(element)
               && MatchesTextRegex(element)
               && MatchesLabel(element)
               && MatchesLabelRegex(element);;
    }

    private bool MatchesText(IElement element)
    {
        if (string.IsNullOrWhiteSpace(Text))
            return true;

        return element.TextContent.Contains(Text) || element.Children.Any(MatchesText); 
    }

    private bool MatchesTextRegex(IElement element)
    {
        if (TextRegex == null)
            return true;
        
        return TextRegex.IsMatch(element.TextContent) || element.Children.Any(MatchesTextRegex);
    }

    private bool MatchesLabel(IElement element)
    {
        if (string.IsNullOrWhiteSpace(Label))
            return true;
        
        var attribute = element.Attributes.GetNamedItem("aria-label");
        return (attribute != null && attribute.Value == Label) || element.Children.Any(MatchesLabel);
    }
    
    private bool MatchesLabelRegex(IElement element)
    {
        if (LabelRegex == null)
            return true;
        
        var attribute = element.Attributes.GetNamedItem("aria-label");
        return (attribute != null && LabelRegex.IsMatch(attribute.Value)) || element.Children.Any(MatchesLabelRegex);
    }
};

public static class RenderedComponentExtensions
{
    public static IElement FindByRole(this IRenderedComponent<IComponent> rendered, string role, FindByRoleOptions options)
    {
        var elements = rendered.FindAll($"[role='{role}']");
        var element = elements.SingleOrDefault(options.Matches);
        return element ?? throw new InvalidOperationException($"Could not find element with role: {role} and {options}");
    }
}